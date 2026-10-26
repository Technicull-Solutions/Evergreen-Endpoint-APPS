# Run against an isolated fixture; never mutate the checkout's version during a test.
$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$Fixture = Join-Path ([IO.Path]::GetTempPath()) "evergreen-version-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path "$Fixture/scripts", "$Fixture/src/UniGetUI.Core.Data", "$Fixture/src/UniGetUI.Avalonia", "$Fixture/src/UniGetUI.AgentPolicy.ElevatedHelper", "$Fixture/update" | Out-Null
foreach ($Relative in @('scripts/set-version.ps1', 'scripts/BuildNumber', 'src/SharedAssemblyInfo.cs', 'src/UniGetUI.Core.Data/CoreData.cs', 'src/UniGetUI.Avalonia/app.manifest', 'src/UniGetUI.AgentPolicy.ElevatedHelper/app.manifest', 'UniGetUI.iss', 'update/productinfo.json')) {
    Copy-Item -LiteralPath (Join-Path $Root $Relative) -Destination (Join-Path $Fixture $Relative)
}
$Script = Join-Path $Fixture 'scripts/set-version.ps1'
& $Script -Version '0.2.1-rc.1' -BuildNumber 5
$Metadata = Get-Content "$Fixture/update/productinfo.json" -Raw | ConvertFrom-Json
$Product = $Metadata.'TechnicullSolutions.EvergreenEndpointPackageManager'
if ($Product.Beta.Version -ne '0.2.1-rc.1' -or $Product.Beta.BuildNumber -ne 5) { throw 'Prerelease was not stamped into Beta.' }
if ($Product.Current.Version -eq '0.2.1-rc.1') { throw 'Prerelease contaminated the stable channel.' }
if ((Get-Content "$Fixture/src/SharedAssemblyInfo.cs" -Raw) -notmatch 'AssemblyFileVersion\("0.2.1.5"\)') { throw 'Windows file version omitted build.' }
foreach ($Manifest in @('UniGetUI.Avalonia', 'UniGetUI.AgentPolicy.ElevatedHelper')) {
    [xml]$Xml = Get-Content "$Fixture/src/$Manifest/app.manifest" -Raw
    if ($Xml.assembly.assemblyIdentity.version -ne '0.2.1.5') { throw "Manifest version mismatch: $Manifest" }
}
$Product.Beta.Files = @(@{ Url = 'https://example.invalid/previous.exe' })
$Metadata | ConvertTo-Json -Depth 12 | Set-Content "$Fixture/update/productinfo.json"
& $Script -Version '0.2.1-rc.2' -BuildNumber 6
$Product = (Get-Content "$Fixture/update/productinfo.json" -Raw | ConvertFrom-Json).'TechnicullSolutions.EvergreenEndpointPackageManager'
if ($Product.Beta.Files.Count -ne 0) { throw 'Stale prerelease assets retained.' }
& $Script -Version '0.2.1' -BuildNumber 7
$Product = (Get-Content "$Fixture/update/productinfo.json" -Raw | ConvertFrom-Json).'TechnicullSolutions.EvergreenEndpointPackageManager'
if ($Product.Current.Version -ne '0.2.1' -or $Product.Beta.Version -ne '0.2.1-rc.2') { throw 'Stable stamping changed the wrong channel.' }
$Before = Get-Content "$Fixture/scripts/BuildNumber" -Raw
foreach ($Invalid in @('65536.0.0', '01.2.3', '1.2.3-rc..1', '1.2.3-rc.01')) {
    $Rejected = $false
    try { & $Script -Version $Invalid -BuildNumber 8 } catch { $Rejected = $true }
    if (-not $Rejected) { throw "Accepted invalid version $Invalid" }
    if ((Get-Content "$Fixture/scripts/BuildNumber" -Raw) -ne $Before) { throw 'Invalid version mutated the fixture.' }
}
Write-Host "Version stamping tests passed. Fixture retained at $Fixture"
