param(
    [string]$GamePath = '',
    [string]$BepInExCorePath = '',
    [string]$BepInEx6CorePath = ''
)
$ErrorActionPreference = 'Stop'
$env:MSBuildEnableWorkloadResolver = 'false'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& (Join-Path $PSScriptRoot 'Test.ps1') -GamePath $GamePath -BepInExCorePath $BepInExCorePath -BepInEx6CorePath $BepInEx6CorePath

$properties = @()
if ($GamePath) { $properties += '-p:GamePath=' + $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($GamePath).TrimEnd('\') }
if ($BepInExCorePath) { $properties += '-p:BepInExCorePath=' + $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($BepInExCorePath).TrimEnd('\') }
if ($BepInEx6CorePath) { $properties += '-p:BepInEx6CorePath=' + $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($BepInEx6CorePath).TrimEnd('\') }
$resolved = (& dotnet msbuild (Join-Path $root 'PortableMusicPlayer.csproj') -getProperty:GamePath -getProperty:BepInExCorePath -getProperty:BepInEx6CorePath @properties -nologo) -join "`n"
if ($LASTEXITCODE -ne 0) { throw "Could not resolve build paths (exit code $LASTEXITCODE)." }
$paths = ($resolved | ConvertFrom-Json).Properties
Add-Type -Path (Join-Path $paths.BepInExCorePath 'Mono.Cecil.dll')

$output5 = Join-Path $root '.local\bin\PortableMusicPlayer\BepInEx5\Release'
$output6 = Join-Path $root '.local\bin\PortableMusicPlayer\BepInEx6\Release'
$dll5 = Join-Path $output5 'PortableMusicPlayer.BepInEx5.dll'
$dll6 = Join-Path $output6 'PortableMusicPlayer.dll'
$nlayer = Join-Path $output6 'NLayer.dll'
if (!(Test-Path -LiteralPath $dll5) -or !(Test-Path -LiteralPath $dll6) -or !(Test-Path -LiteralPath $nlayer)) { throw 'Verified dual-loader build output is incomplete.' }
$version = ([Reflection.AssemblyName]::GetAssemblyName($dll6).Version).ToString(3)
foreach ($dll in $dll5,$dll6) {
    $resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
    $resolver.AddSearchDirectory($(if ($dll -eq $dll5) { $paths.BepInExCorePath } else { $paths.BepInEx6CorePath }))
    $resolver.AddSearchDirectory([IO.Path]::GetDirectoryName($dll))
    $resolver.AddSearchDirectory((Join-Path $paths.GamePath 'Obenseuer_Data\Managed'))
    $reader = [Mono.Cecil.ReaderParameters]::new()
    $reader.AssemblyResolver = $resolver
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll, $reader)
    try {
        $pluginTypes = @($assembly.MainModule.Types | Where-Object {
            @($_.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInPlugin' }).Count -gt 0
        })
        if ($pluginTypes.Count -ne 1) { throw "$([IO.Path]::GetFileName($dll)) must contain exactly one BepInPlugin." }
        $plugin = $pluginTypes[0]
        $pluginAttribute = @($plugin.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInPlugin' })[0]
        $pluginArguments = @($pluginAttribute.ConstructorArguments)
        if ([string]$pluginArguments[0].Value -cne 'com.skeptic043.obenseuer.portablemusicplayer') { throw 'BepInPlugin carries the wrong mod ID.' }
        if (@($plugin.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'ObenseuerCore.ModInfoAttribute' }).Count -ne 0) { throw 'The early plugin must not also load as a framework mod.' }
        $dependency = @($plugin.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInDependency' })
        $dependencyArguments = if ($dependency.Count -eq 1) { @($dependency[0].ConstructorArguments) } else { @() }
        if ($dependency.Count -ne 1 -or [string]$dependencyArguments[0].Value -cne 'obenseuer.moddingframework' -or
            ($dependencyArguments.Count -gt 1 -and [int]$dependencyArguments[1].Value -ne 1)) {
            throw 'The plugin must carry one hard framework dependency.'
        }
        if (@($dependency[0].Constructor.Parameters | Where-Object { $_.ParameterType.FullName -eq 'System.Version' }).Count -ne 0 -or
            @($dependencyArguments | Select-Object -Skip 1 | Where-Object { $_.Type.FullName -eq 'System.String' }).Count -ne 0) {
            throw 'The framework dependency must not be pinned to a version.'
        }
        $references = @($assembly.MainModule.AssemblyReferences | ForEach-Object { $_.Name })
        foreach ($required in 'ObenseuerCore','NLayer') { if ($references -notcontains $required) { throw "The mod does not reference $required." } }
        $loot = $assembly.MainModule.GetType('PortableMusicPlayer.Items.LootSetup')
        $apply = @($loot.Methods | Where-Object { $_.Name -eq 'Apply' })[0]
        $guardCalls = @($apply.Body.Instructions | ForEach-Object { $_.Operand } | Where-Object {
            $_ -is [Mono.Cecil.MethodReference] -and $_.DeclaringType.FullName -eq 'PortableMusicPlayer.Items.ItemSetup'
        } | ForEach-Object { $_.Name })
        if ($guardCalls -notcontains 'TryIdOf' -or $guardCalls -notcontains 'IsUsableId') {
            throw 'Loot registration is not guarded against missing or invalid item IDs.'
        }
    }
    finally { $assembly.Dispose(); $resolver.Dispose() }
}

$dist = Join-Path $root 'dist'
$stage = Join-Path $root ('.local\package-checks\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage,$dist -Force | Out-Null
$zip = Join-Path $dist "PortableMusicPlayer-$version.zip"
$folder = 'BepInEx/plugins/PortableMusicPlayer/'
$packageEntries = [ordered]@{
    ($folder + 'LICENSE') = Join-Path $root 'LICENSE'
    ($folder + 'licenses/NLayer.txt') = Join-Path $root 'licenses\NLayer.txt'
    ($folder + 'NLayer.dll') = $nlayer
    ($folder + 'PortableMusicPlayer.BepInEx5.dll') = $dll5
    ($folder + 'PortableMusicPlayer.dll') = $dll6
    ($folder + 'README.md') = Join-Path $root 'README.md'
    ($folder + 'THIRD-PARTY-NOTICES.md') = Join-Path $root 'THIRD-PARTY-NOTICES.md'
}
foreach ($asset in Get-ChildItem -LiteralPath (Join-Path $root 'assets') -File | Sort-Object Name) {
    $packageEntries[$folder + 'assets/' + $asset.Name] = $asset.FullName
}
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in @($packageEntries.Keys | Sort-Object)) {
        $source = $packageEntries[$name]
        $entry = $archive.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset](Get-Item -LiteralPath $source).LastWriteTime
        $target = $entry.Open()
        try {
            $input = [IO.File]::OpenRead($source)
            try { $input.CopyTo($target) } finally { $input.Dispose() }
        }
        finally { $target.Dispose() }
    }
}
finally { $archive.Dispose() }

$extract = Join-Path $stage 'extracted'
Expand-Archive -LiteralPath $zip -DestinationPath $extract
$relative = @(Get-ChildItem -LiteralPath $extract -Recurse -File | ForEach-Object {
    $_.FullName.Substring($extract.Length + 1).Replace('\','/')
} | Sort-Object)
$expected = @($packageEntries.Keys | Sort-Object)
if (($relative -join "`n") -cne ($expected -join "`n")) { throw "Package inventory differs from the explicit allowlist: $($relative -join ', ')" }
foreach ($dll in $dll5,$dll6) {
    $installedName = [IO.Path]::GetFileName($dll)
    if ((Get-FileHash -LiteralPath (Join-Path $extract "BepInEx\plugins\PortableMusicPlayer\$installedName")).Hash -ne (Get-FileHash -LiteralPath $dll).Hash) {
        throw "Extracted $installedName differs from the verified build."
    }
}
function Get-TopFolder([string]$path) {
    $full = [IO.Path]::GetFullPath($path)
    $drive = [IO.Path]::GetPathRoot($full)
    $first = $full.Substring($drive.Length).Split('\')[0]
    if ($first) { return (Join-Path $drive $first) }
}
$markers = @(Get-TopFolder $root; Get-TopFolder $paths.GamePath; $env:USERNAME) |
    Where-Object { $_ -and $_.Length -ge 3 } | Sort-Object -Unique
$latin1 = [Text.Encoding]::GetEncoding(28591)
foreach ($file in Get-ChildItem -LiteralPath $extract -Recurse -File) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    foreach ($text in @($latin1.GetString($bytes), [Text.Encoding]::Unicode.GetString($bytes))) {
        foreach ($marker in $markers) {
            if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "Personal path marker found in packaged file $($file.Name)."
            }
        }
    }
}
Write-Output "Candidate: $zip"
Write-Output "ZIP SHA256: $((Get-FileHash -LiteralPath $zip).Hash)"
Write-Output "BepInEx 5 DLL SHA256: $((Get-FileHash -LiteralPath $dll5).Hash)"
Write-Output "BepInEx 6 DLL SHA256: $((Get-FileHash -LiteralPath $dll6).Hash)"
Write-Output "Extracted inventory: $($relative.Count) files"
Write-Output "Personal path scan clean ($($markers.Count) markers)."
# The extracted check copy is only needed while checking; a failed run keeps it for inspection.
Remove-Item -LiteralPath $stage -Recurse -Force
