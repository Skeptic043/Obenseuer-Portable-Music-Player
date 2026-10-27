# Third-party notices

Portable Music Player includes NLayer 1.16.0 for MP3 decoding. NLayer is licensed under the MIT License.
Its full copyright and permission notice is retained in `licenses/NLayer.txt`.

The audio decoder, streaming, scanner, and metadata code was adapted from Obenseuer Radio revision
`88473f9c656fd42ef6161620a723a275465d906c`, which adapted it from Sailwind Radio revision
`3416b636d9eb365436b09c7e34a6ec9bb31fd9e9`. Both are owned by skeptic043 and licensed under the MIT License. The
source correspondence is `src/Audio/{AudioFiles,ChannelDownmix,FlacDecoder,IPcmDecoder,MediaFoundation,
MediaFoundationDecoder,Mp3PcmDecoder,OggReader,PcmDecoders,StreamedAudio,VorbisCodec,VorbisDecoder,
WavPcmDecoder}.cs` and `src/Library/{FlacMetadata,LibraryScanner,LibrarySnapshot,MetadataCache,Mp4Metadata,
PathRules,QueuePlanner,TrackMetadata}.cs`.
