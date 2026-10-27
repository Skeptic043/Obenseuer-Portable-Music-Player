param(
    [string]$GamePath = '',
    [string]$BepInExCorePath = '',
    [string]$BepInEx6CorePath = '',
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$env:MSBuildEnableWorkloadResolver = 'false'
$project = Join-Path $PSScriptRoot '..\PortableMusicPlayer.csproj'
$properties = @()
foreach ($name in 'GamePath','BepInExCorePath','BepInEx6CorePath') {
    $value = Get-Variable -Name $name -ValueOnly
    if ($value) { $properties += "-p:$name=" + $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($value).TrimEnd('\') }
}
foreach ($loader in 'BepInEx5','BepInEx6') {
    & dotnet build $project --configuration $Configuration "-p:Loader=$loader" @properties --nologo
    if ($LASTEXITCODE -ne 0) { throw "$loader build failed with exit code $LASTEXITCODE. Do not use existing output." }
}
