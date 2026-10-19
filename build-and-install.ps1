param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,
    [string]$ProfileDir
)

$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameDir).Path
$installRoot = $game
if (-not [string]::IsNullOrWhiteSpace($ProfileDir)) {
    $installRoot = (Resolve-Path -LiteralPath $ProfileDir).Path
}

$managed = Join-Path $game 'StoneWards_Data\Managed'
$core = Join-Path $installRoot 'BepInEx\core'
if (-not (Test-Path -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll'))) {
    throw "Stonewards DLL not found: $managed"
}
if (-not (Test-Path -LiteralPath (Join-Path $core 'BepInEx.dll'))) {
    throw "BepInEx 5 not found in the active game/profile: $core"
}
if (-not (Test-Path -LiteralPath (Join-Path $core '0Harmony.dll'))) {
    throw "Harmony DLL not found in the active game/profile: $core"
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install .NET SDK before building this mod.'
}

$project = Join-Path $PSScriptRoot 'StonewardsDropFullStack.csproj'
Write-Host 'Building Stonewards Drop Full Stack 0.1.0...'
& dotnet build $project --configuration Release "-p:GameDir=$game" "-p:BepInExCoreDir=$core"
if ($LASTEXITCODE -ne 0) {
    throw 'Build failed. The mod was NOT installed.'
}

$built = Join-Path $PSScriptRoot 'bin\Release\netstandard2.1\StonewardsDropFullStack.dll'
$targetDir = Join-Path $installRoot 'BepInEx\plugins\StonewardsDropFullStack'
$targetFile = Join-Path $targetDir 'StonewardsDropFullStack.dll'
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
Copy-Item -LiteralPath $built -Destination $targetFile -Force

$buildHash = (Get-FileHash -LiteralPath $built -Algorithm SHA256).Hash
$installedHash = (Get-FileHash -LiteralPath $targetFile -Algorithm SHA256).Hash
if ($buildHash -ne $installedHash) {
    throw 'Installed DLL hash does not match the build.'
}
Write-Host "Installed: $targetFile" -ForegroundColor Green
Write-Host "SHA256: $installedHash"
Write-Host 'Start Stonewards using the same active r2modman profile to test.'

$duplicates = @(Get-ChildItem -LiteralPath (Join-Path $installRoot 'BepInEx\plugins') `
    -Filter 'StonewardsDropFullStack.dll' -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -ne $targetFile })
if ($duplicates.Count -gt 0) {
    Write-Warning ('Duplicate copies: ' + (($duplicates | ForEach-Object { $_.FullName }) -join '; '))
}
