param(
    [string]$GamePath = '',
    [string]$BepInExCorePath = '',
    [string]$BepInEx6CorePath = ''
)
$ErrorActionPreference = 'Stop'
$env:MSBuildEnableWorkloadResolver = 'false'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'PortableMusicPlayer.Tests.csproj'
$properties = @()
if ($GamePath) { $properties += '-p:GamePath=' + $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($GamePath).TrimEnd('\') }
if ($BepInExCorePath) { $properties += '-p:BepInExCorePath=' + $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($BepInExCorePath).TrimEnd('\') }
if ($BepInEx6CorePath) { $properties += '-p:BepInEx6CorePath=' + $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($BepInEx6CorePath).TrimEnd('\') }
& (Join-Path $PSScriptRoot 'Build.ps1') -GamePath $GamePath -BepInExCorePath $BepInExCorePath -BepInEx6CorePath $BepInEx6CorePath
& dotnet build $project --configuration Release @properties --nologo
if ($LASTEXITCODE -ne 0) { throw "Test build failed with exit code $LASTEXITCODE." }
$resolved = (& dotnet msbuild $project -getProperty:GamePath -getProperty:BepInEx6CorePath @properties -nologo) -join "`n"
if ($LASTEXITCODE -ne 0) { throw "Could not resolve GamePath (exit code $LASTEXITCODE)." }
$paths = ($resolved | ConvertFrom-Json).Properties
& (Join-Path $root '.local\bin\PortableMusicPlayer.Tests\BepInEx5\Release\PortableMusicPlayer.Tests.exe') $paths.GamePath $paths.BepInEx6CorePath
if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE." }
