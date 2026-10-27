# Portable Music Player

Listen to your music on the go in Obenseuer. Portable Music Player adds 2 music player types and 3 sets of headphones, capable of playing your local music files.

## Install

### Manual installation

1. Install either [BepInEx 5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) or [BepInEx 6 Unity Mono](https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.Mono-win-x64-6.0.0-be.788%2B5b766a3.zip) in your Obenseuer game folder.
2. Install the [Obenseuer Modding Framework](https://github.com/NomadsReach/Obenseuer-Modding-Framework/releases/latest).
3. Download [Portable Music Player](https://github.com/Skeptic043/Obenseuer-Portable-Music-Player/releases/latest), then extract the ZIP into the Obenseuer game folder. The same ZIP supports both BepInEx versions.
4. Launch Obenseuer normally.

## How it works

The music players and headphones can be found in desks, lockers, nightstands and suitcases, or bought from select traders.

- Pocket Music Player: Cheap, brown, and runs on an AA battery.
- Rechargeable Music Player: Teal, with a built-in rechargeable battery.
- Budget Earbuds: Worst sound quality, thin/quiet.
- Work Headphones: Hearing protection with speakers inside, sound quality is between the other two sets.
- Hi-Fi Headphones: Full and clean sound without hearing protection.

Turn a player on from your inventory, then use the `Open` button to open the music player's display. Press `MENU` on the player to browse music collections or the game's radio music and pick a song to play it. Folders with more than 100 songs are split into pages. The corner of the player can be dragged to resize the player. Music only plays while the music player is powered and you are wearing headphones. Taking headphones off, powering the player off, or dropping the player will all pause music playback. A full AA battery lasts 12 in-game hours of playing, while a full Rechargeable Music Player lasts 24. Neither drains while the music is paused, while you aren't wearing headphones, or while the player is off. The game's Master and Music volume sliders apply to the player.

Put your music in `BepInEx/plugins/PortableMusicPlayer/Music`. Subfolders become collections. New songs show up the next time you turn on a player.

MP3, OGG, WAV and FLAC files are supported, with M4A and AAC being included when Windows Media Foundation is available.

If [Obentunes](https://github.com/Bdopz/Obentunes) is installed, its library of game and custom music replaces the Game Radio collection.

## Configuration

Playback settings are in the Obenseuer Modding Framework's `Mods` menu, on the main menu or in the pause menu, and in `BepInEx/config/com.skeptic043.obenseuer.portablemusicplayer.cfg`. Controls are in the Obenseuer Modding Framework's `Mod Keybinds` manager.

| Setting | Default | Options |
| --- | --- | --- |
| `AdditionalFolders` | empty | Extra music folders, separated with `\|`, for example `D:\Music \| E:\Game Music`. Scans all subfolders. |
| `PlayWhilePassingTime` | `true` | `true` keeps playing while time skips ahead: sleeping, waiting, reading, working a shift or watching TV. |
| `PassingTimeDrain` | `50` | Battery drain while playing as time skips ahead, as a percentage of the normal rate: `0`, `25`, `50`, `75` or `100`. |
| `PlayThroughLoading` | `true` | `true` keeps the song going through doors and level loads. `false` pauses it until the load finishes. |
| Toggle power | none | Any key or key combination. |
| Open player | none | Any key or key combination. |
| Previous track | `[` | Any key or key combination. |
| Play / pause | `\` | Any key or key combination. |
| Next track | `]` | Any key or key combination. |

## Saves and removal

Portable Music Player can be freely added or removed from existing saves. If the mod is removed, its items safely turn into placeholders.

## Compatibility

Portable Music Player needs the Obenseuer Modding Framework and supports BepInEx 5 and BepInEx 6 Unity Mono on Windows.

## Licenses and credits

Portable Music Player includes [NLayer 1.16.0](https://www.nuget.org/packages/NLayer/1.16.0) by Mark Heath, Andrew Ward and contributors under the MIT License. Complete notices are included in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and the release download. The mod is built on the [Obenseuer Modding Framework](https://github.com/NomadsReach/Obenseuer-Modding-Framework) by NomadsReach and integrates with [Obentunes](https://github.com/Bdopz/Obentunes) by Bdopz.

The source code, documentation, models and textures are available under the MIT License.

## AI Use

AI was used to write all of the code in this project. The original design direction, testing, debugging, and release decisions are my own. If you prefer not to use mods developed with AI assistance, I understand and respect that choice.

## Issues and links

[Report an issue](https://github.com/Skeptic043/Obenseuer-Portable-Music-Player/issues).

[Download](https://github.com/Skeptic043/Obenseuer-Portable-Music-Player/releases/latest) · [Source code](https://github.com/Skeptic043/Obenseuer-Portable-Music-Player) · [Changelog](https://github.com/Skeptic043/Obenseuer-Portable-Music-Player/blob/main/CHANGELOG.md) · [Build instructions](https://github.com/Skeptic043/Obenseuer-Portable-Music-Player/blob/main/BUILDING.md) · [MIT License](https://github.com/Skeptic043/Obenseuer-Portable-Music-Player/blob/main/LICENSE) · [Support on Ko-fi](https://ko-fi.com/skeptic043) · skeptic043
