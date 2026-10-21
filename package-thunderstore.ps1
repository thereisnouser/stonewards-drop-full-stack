[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)] [string]$GameDir,
    [string]$ProfileDir = '',
    [string]$OutputDir = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$game = (Resolve-Path -LiteralPath $GameDir).Path
$install = $game
if (-not [string]::IsNullOrWhiteSpace($ProfileDir)) {
    $install = (Resolve-Path -LiteralPath $ProfileDir).Path
}
$core = Join-Path $install 'BepInEx\core'
$managed = Join-Path $game 'StoneWards_Data\Managed'
$store = Join-Path $PSScriptRoot 'thunderstore'
$project = Join-Path $PSScriptRoot 'StonewardsDropFullStack.csproj'
$plugin = Join-Path $PSScriptRoot 'src\DropFullStackPlugin.cs'
$dll = Join-Path $PSScriptRoot 'bin\Release\netstandard2.1\StonewardsDropFullStack.dll'
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $PSScriptRoot 'dist'
}
foreach ($p in @(
    (Join-Path $store 'icon.png'), (Join-Path $store 'manifest.json'),
    (Join-Path $store 'README.md'), (Join-Path $store 'CHANGELOG.md'),
    $project, $plugin,
    (Join-Path $managed 'Assembly-CSharp.dll'),
    (Join-Path $core 'BepInEx.dll'),
    (Join-Path $core '0Harmony.dll')
)) {
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) {
        throw "Release file missing: $p"
    }
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install .NET SDK before packaging.'
}

$manifest = Get-Content -LiteralPath (Join-Path $store 'manifest.json') -Raw | ConvertFrom-Json
$version = [string]$manifest.version_number
if ($manifest.name -cne 'DropFullStack' -or $version -ne '0.1.1') {
    throw 'Unexpected manifest name or version.'
}
$code = Get-Content -LiteralPath $plugin -Raw
$projectCode = Get-Content -LiteralPath $project -Raw
if ($code -notmatch ('PluginVersion\s*=\s*"' + [regex]::Escape($version) + '"') -or
    $projectCode -notmatch ('<Version>' + [regex]::Escape($version) + '</Version>')) {
    throw 'Source and manifest version mismatch.'
}
if ($manifest.description.Length -gt 250) {
    throw 'Thunderstore description is too long.'
}
$png = [IO.File]::ReadAllBytes((Join-Path $store 'icon.png'))
$signature = [byte[]](137,80,78,71,13,10,26,10)
if ($png.Length -lt 24) { throw 'Invalid PNG.' }
for ($i=0; $i -lt 8; $i++) {
    if ($png[$i] -ne $signature[$i]) { throw 'Invalid PNG signature.' }
}
$width = [BitConverter]::ToInt32([byte[]]@($png[19],$png[18],$png[17],$png[16]),0)
$height = [BitConverter]::ToInt32([byte[]]@($png[23],$png[22],$png[21],$png[20]),0)
if ($width -ne 256 -or $height -ne 256) { throw 'Thunderstore icon must be 256x256.' }

& dotnet build $project -c Release "-p:GameDir=$game" "-p:BepInExCoreDir=$core" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed; no release ZIP produced.' }
if (-not (Test-Path -LiteralPath $dll)) { throw "Missing DLL: $dll" }
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()
if ($assemblyVersion -notmatch ('^' + [regex]::Escape($version) + '\.\d+$')) {
    throw "Wrong DLL assembly version: $assemblyVersion"
}

$temp = Join-Path ([IO.Path]::GetTempPath()) ('drop-full-stack-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $temp 'plugins') -Force | Out-Null
try {
    foreach ($name in @('manifest.json','README.md','CHANGELOG.md','icon.png')) {
        Copy-Item -LiteralPath (Join-Path $store $name) -Destination (Join-Path $temp $name)
    }
    Copy-Item -LiteralPath $dll -Destination (Join-Path $temp 'plugins\StonewardsDropFullStack.dll')
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    $out = Join-Path $OutputDir ("DropFullStack-" + $version + ".zip")
    if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Force }
    Compress-Archive -Path (Join-Path $temp '*') -DestinationPath $out -CompressionLevel Optimal
    $zip = [IO.Compression.ZipFile]::OpenRead($out)
    try {
        $all = @($zip.Entries | ForEach-Object { $_.FullName.Replace([char]92,[char]47) })
        foreach ($required in @('manifest.json','README.md','CHANGELOG.md','icon.png',
                'plugins/StonewardsDropFullStack.dll')) {
            if ($all -notcontains $required) { throw "ZIP missing: $required" }
        }
        $reader = New-Object IO.StreamReader($zip.GetEntry('README.md').Open())
        try {
            if ($reader.ReadToEnd() -cne [IO.File]::ReadAllText((Join-Path $store 'README.md'))) {
                throw 'Wrong README packaged.'
            }
        } finally { $reader.Dispose() }
    } finally { $zip.Dispose() }
    Write-Host "Thunderstore package ready: $out" -ForegroundColor Green
    Write-Host "SHA256: $((Get-FileHash -LiteralPath $out -Algorithm SHA256).Hash)"
} finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
