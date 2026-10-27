# Building

Requirements:

- .NET SDK capable of targeting .NET Framework 4.7.2
- An Obenseuer installation
- `ObenseuerCore.BepInEx5.dll` and `ObenseuerCore.dll` from the same Obenseuer Modding Framework release
- A BepInEx 5 `core` folder containing `BepInEx.dll`, `0Harmony.dll` and `Mono.Cecil.dll`
- A BepInEx 6 Unity Mono `core` folder containing `BepInEx.Core.dll`, `BepInEx.Unity.Mono.dll` and `0Harmony.dll`

Set `GamePath`, `BepInExCorePath`, `BepInEx6CorePath`, `FrameworkDll5` and `FrameworkDll6` in ignored
`.local/LocalPaths.props`. The framework paths default to the installed BepInEx 5 copy and
`.local/dependencies/ObenseuerCore/ObenseuerCore.dll`. The three directory paths can also be passed to the scripts.
Then run:

```powershell
./scripts/Build.ps1
./scripts/Test.ps1
./scripts/Package.ps1
```

Each script builds both loader variants. The tests read the installed game's `Assembly-CSharp.dll` and
`StorageSpawnCategory.json`, so they need `GamePath`.

The package script creates and freshly extracts a local ZIP under `dist/`, verifies its explicit inventory, and
prints SHA-256 hashes. It does not install the mod or change any save.

The item models and icons in `assets/` are generated. To change one, edit `tools/make_assets.py` and run
`python -I tools/make_assets.py` (needs Pillow and NumPy). Add `--preview <folder>` for front, side and top renders.
