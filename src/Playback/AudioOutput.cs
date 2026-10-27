using System;
using System.IO;
using System.Threading;
using ObenseuerCore;
using PortableMusicPlayer.Audio;
using PortableMusicPlayer.Core;
using UnityEngine;

namespace PortableMusicPlayer.Playback
{
    // The one private, non-positional source. It lives outside the scenes, so doors and level loads never
    // destroy it. A track starts its clip at the saved frame and holding is a plain pause, so playback
    // never depends on seeking a streamed clip after Play. Repeat One loops the clip itself, so the song
    // wraps without a gap or a reload.
    internal sealed class AudioOutput : IDisposable
    {
        private readonly GameObject owner;
        private readonly AudioSource source;
        private readonly AudioHighPassFilter highPass;
        private readonly AudioLowPassFilter lowPass;
        private StreamedAudio stream;
        private StreamedAudio.Reader reader;
        private AudioClip clip;
        private bool directClip;
        private double requestedSeconds;
        private bool started;
        private bool holding;
        private bool played;
        private int startWaitFrames;
        // The playhead at the previous tick, so damage counts only once playback actually reaches it.
        private int lastFrame = -1;

        internal AudioOutput()
        {
            owner = new GameObject("Portable Music Player Audio");
            UnityEngine.Object.DontDestroyOnLoad(owner);
            source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.bypassReverbZones = true;
            source.reverbZoneMix = 0f;
            // Loads and the pause menu pause the listener; the gate decides when this source holds instead.
            source.ignoreListenerPause = true;
            highPass = owner.AddComponent<AudioHighPassFilter>();
            lowPass = owner.AddComponent<AudioLowPassFilter>();
        }

        internal string Path { get; private set; }
        internal string Failure { get; private set; }
        internal bool Loop { get; set; }
        // Why the track ended early: the decoder failed after audio had already played.
        internal Exception Damage { get; private set; }
        internal bool Advancing => started && !holding && source.isPlaying;
        internal int Sample => started ? source.timeSamples : -1;

        internal double PositionSeconds => clip == null || clip.frequency <= 0 ? requestedSeconds :
            directClip ? Math.Max(0, source.timeSamples) / (double)clip.frequency :
            reader == null ? requestedSeconds : (reader.Start + Math.Max(0, source.timeSamples)) / (double)clip.frequency;
        // A streamed clip that resumed mid-track covers only the rest of it, so the stream gives the whole length.
        internal double DurationSeconds => directClip ? (clip == null ? 0d : Math.Max(0d, clip.length)) :
            stream != null && stream.IsOpen && stream.SampleRate > 0 ? stream.Frames / (double)stream.SampleRate : 0d;

        // The track played to its end on its own, or to where its damage begins.
        internal bool Finished => started && played && !holding && !source.isPlaying &&
            (directClip || Damage != null || reader != null && reader.Cursor >= reader.Frames - 1);

        internal void Start(string path, double seconds)
        {
            Stop();
            Path = path;
            requestedSeconds = seconds;
            try
            {
                if (!File.Exists(path)) throw new FileNotFoundException("The music file is missing");
                if (!PcmDecoders.Supports(path)) throw new IOException(PcmDecoders.UnsupportedMessage(path) ?? PcmDecoders.ChooseFormatMessage);
                stream = StreamedAudio.Start(path, seconds);
            }
            catch (Exception error) { Fail(error.Message); }
        }

        // A track decoded through its own opener, such as an Obentunes track.
        internal void Start(string path, Func<CancellationToken, IPcmDecoder> open, double seconds)
        {
            Stop();
            Path = path;
            requestedSeconds = seconds;
            stream = StreamedAudio.Start(open, seconds);
        }

        // A native game clip. It belongs to the game and is never destroyed here.
        internal void Start(AudioClip gameClip, string path, double seconds)
        {
            Stop();
            Path = path;
            requestedSeconds = seconds;
            if (gameClip == null) { Fail("The game radio track is unavailable"); return; }
            clip = gameClip;
            directClip = true;
            source.clip = clip;
            if (clip.samples > 0 && clip.frequency > 0)
                source.timeSamples = (int)Math.Max(0, Math.Min(clip.samples - 1L, Math.Floor(Math.Max(0d, seconds) * clip.frequency)));
        }

        internal void Tick(bool play)
        {
            // Master, outside the Load group that loads and the pause menu silence, and outside the earmuff
            // muffle. The Music slider is applied in Shape. The game's mixer exists once a scene has loaded.
            if (source.outputAudioMixerGroup == null) source.outputAudioMixerGroup = Sounds.Find("Master");
            if (directClip && Path != null && clip == null) { Fail("The game radio track was unloaded"); return; }
            if (stream == null && clip == null) return;
            if (stream != null && stream.Fault != null) { Fail(stream.Fault.Message); return; }
            if (stream != null) stream.Loop = Loop;
            if (stream != null && clip == null && !Attach()) return;
            // A streamed clip that began mid-track can only loop back to where it began. It plays to its end and
            // Restart then builds a whole-track clip from frames the look-ahead already wrapped to.
            source.loop = Loop && (directClip || reader != null && reader.Start == 0);
            if (play && !started) { source.Play(); started = true; holding = false; }
            else if (play && holding) { source.UnPause(); holding = false; }
            else if (!play && started && !holding) { source.Pause(); holding = true; }
            if (started && !holding)
            {
                if (source.isPlaying) { played = true; startWaitFrames = 0; }
                else if (play && !played && ++startWaitFrames >= 30)
                {
                    Fail("The audio clip did not start");
                    return;
                }
            }
            if (started && stream != null)
            {
                int frame = reader.Start + source.timeSamples, damageAt = stream.DamageFrame;
                // Everything from the damage on is silence, so the track ends there instead of playing it out.
                // Damage found while decoding past a loop wrap lies behind the playhead until the loop comes round.
                bool reached = frame >= damageAt && (lastFrame < damageAt || frame < lastFrame);
                lastFrame = frame;
                if (Damage == null && reached) { Damage = stream.Damage; played = true; source.Stop(); return; }
                stream.Request(frame);
            }
        }

        internal void Shape(OutputProfile profile, float volume)
        {
            highPass.cutoffFrequency = profile.HighPassHz;
            lowPass.cutoffFrequency = profile.LowPassHz;
            float musicSlider = Mathf.Clamp01(Mathf.Pow(10f, Sounds.MusicVolume / 20f));
            source.volume = Mathf.Clamp01(volume) * profile.MaximumVolume * musicSlider;
        }

        // Back to the first frame of the same track without reopening it.
        internal void Restart()
        {
            requestedSeconds = 0d;
            if (Path == null || Failure != null) return;
            source.Stop();
            if (directClip) source.timeSamples = 0;
            else
            {
                // A streamed clip begins at its reader's frame, so a new clip from frame 0 replaces it.
                source.clip = null;
                reader = null;
                if (clip != null) UnityEngine.Object.Destroy(clip);
                clip = null;
            }
            started = false;
            holding = false;
            played = false;
            startWaitFrames = 0;
            Damage = null;
            lastFrame = -1;
        }

        internal void Stop()
        {
            source.Stop();
            source.clip = null;
            reader = null;
            // Streamed clips are created here; direct clips belong to the game.
            if (clip != null && !directClip) UnityEngine.Object.Destroy(clip);
            clip = null;
            directClip = false;
            stream?.Dispose();
            stream = null;
            Path = null;
            Failure = null;
            Damage = null;
            lastFrame = -1;
            started = false;
            holding = false;
            played = false;
            startWaitFrames = 0;
        }

        // Creates the clip once the saved position is decoded.
        private bool Attach()
        {
            if (!stream.IsOpen) return false;
            int frame = (int)Math.Max(0, Math.Min(stream.Frames - 1L, Math.Floor(Math.Max(0d, requestedSeconds) * stream.SampleRate)));
            stream.Request(frame);
            if (!stream.Ready(frame)) return false;
            try
            {
                reader = stream.CreateReader(frame);
                clip = stream.CreateClip(reader);
                if (clip == null) throw new IOException("Unity could not create the streamed clip");
                source.clip = clip;
                return true;
            }
            catch (Exception error) { Fail(error.Message); return false; }
        }

        private void Fail(string reason)
        {
            string path = Path;
            Stop();
            Path = path;
            Failure = reason;
        }

        public void Dispose()
        {
            Stop();
            if (owner != null) UnityEngine.Object.Destroy(owner);
        }
    }
}
