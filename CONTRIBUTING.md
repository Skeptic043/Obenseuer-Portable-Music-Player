# Contributing

Bug reports and focused pull requests are welcome. Open an issue before starting a large behavior or compatibility change so the intended scope is clear.

## Building and testing

See [BUILDING.md](BUILDING.md) for the required game, framework and loader references. Run both the test and package scripts before submitting a change:

```powershell
./scripts/Test.ps1
./scripts/Package.ps1
```

Do not commit game assemblies, extracted game assets, saves, logs, local paths, credentials or files from `.local/`. Keep tests focused on player-visible behavior or a concrete regression.

## Reports

Include the game version, loader, Modding Framework version, mod version, reproduction steps and relevant log excerpts. Logs can contain usernames and local paths, so redact private information before posting them.
