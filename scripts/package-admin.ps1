[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')][string] $Platform = 'x64',
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\output')
)
$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Build = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'BuildNumber') -Raw).Trim()
$Info = Get-Content -LiteralPath (Join-Path $RepoRoot 'src\SharedAssemblyInfo.cs') -Raw
$Version = [regex]::Match($Info, 'AssemblyInformationalVersion\("([^"]+)"\)').Groups[1].Value
if (-not $Version -or $Build -notmatch '^\d+$') { throw 'Invalid release version.' }
$Stage = Join-Path $RepoRoot "artifacts\admin-$Platform-$Build-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $Stage -Force | Out-Null
$Project = Join-Path $RepoRoot 'src\Evergreen.Admin\Evergreen.Admin.csproj'
dotnet publish $Project --configuration Release --runtime "win-$Platform" --self-contained true --nologo /p:Platform=$Platform /p:PublishDir="$Stage\" /p:TrimmerSingleWarn=false /p:ILLinkTreatWarningsAsErrors=true /p:IlcTreatWarningsAsErrors=true
if ($LASTEXITCODE -ne 0) { throw 'Administrator NativeAOT publish failed.' }
$Executable = Join-Path $Stage 'Evergreen Endpoint Admin.exe'
$NativeExecutable = Join-Path $Stage 'Evergreen.Admin.exe'
if (-not (Test-Path -LiteralPath $NativeExecutable -PathType Leaf)) { throw 'Administrator NativeAOT executable was not published.' }
# Keep the resource assembly URI safe while giving the portable program its product filename.
Move-Item -LiteralPath $NativeExecutable -Destination $Executable
if (-not (Test-Path -LiteralPath $Executable)) { throw 'Administrator executable was not published.' }
if ($Platform -eq 'x64' -and [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq 'X64') {
    $Workspace = Join-Path $Stage 'self-test'
    New-Item -ItemType Directory -Path $Workspace | Out-Null
    $Process = Start-Process -FilePath $Executable -ArgumentList @('--self-test', "`"$Workspace`"") -WindowStyle Hidden -PassThru
    if (-not $Process.WaitForExit(30000)) { $Process.Kill(); throw 'Administrator self-test timed out.' }
    if ($Process.ExitCode -ne 0) { throw "Published administrator self-test failed: $($Process.ExitCode)." }
    # The tested workspace remains in artifacts, outside the packaged application.
    Move-Item -LiteralPath $Workspace -Destination "$Stage-self-test"
} else {
    Write-Warning 'Published administrator execution was not tested on this host architecture.'
}
Copy-Item -LiteralPath (Join-Path $RepoRoot 'docs\ADMIN_CLIENT.md') -Destination (Join-Path $Stage 'README.md')
Copy-Item -LiteralPath (Join-Path $RepoRoot 'docs\WINDOWS_RELEASE_TRUST.md') -Destination $Stage
Copy-Item -LiteralPath (Join-Path $RepoRoot 'docs\GITEA_CATALOG_TESTING.md') -Destination $Stage
Copy-Item -LiteralPath (Join-Path $RepoRoot 'docs\CATALOG_SOURCE_PROFILES.md') -Destination $Stage
Copy-Item -LiteralPath (Join-Path $RepoRoot 'package\Catalog\catalog-source.example.json') -Destination $Stage
Copy-Item -LiteralPath (Join-Path $RepoRoot 'package\Catalog\SoftwareCatalog.example.json') -Destination $Stage
$PackagedGiteaGuide = Join-Path $Stage 'GITEA_CATALOG_TESTING.md'
(Get-Content -LiteralPath $PackagedGiteaGuide -Raw).Replace('(ADMIN_CLIENT.md)', '(README.md)') |
    Set-Content -LiteralPath $PackagedGiteaGuide -Encoding utf8
$PackagedSourceGuide = Join-Path $Stage 'CATALOG_SOURCE_PROFILES.md'
(Get-Content -LiteralPath $PackagedSourceGuide -Raw).Replace('(ADMIN_CLIENT.md)', '(README.md)') |
    Set-Content -LiteralPath $PackagedSourceGuide -Encoding utf8
$PackagedReadme = Join-Path $Stage 'README.md'
(Get-Content -LiteralPath $PackagedReadme -Raw).Replace('(../package/Policies/README.md)', '(Policies/README.md)') |
    Set-Content -LiteralPath $PackagedReadme -Encoding utf8
Copy-Item -LiteralPath (Join-Path $RepoRoot 'LICENSE') -Destination $Stage
Copy-Item -LiteralPath (Join-Path $RepoRoot 'UPSTREAM.json') -Destination $Stage
Copy-Item -LiteralPath (Join-Path $RepoRoot 'package\Policies') -Destination $Stage -Recurse
& (Join-Path $PSScriptRoot 'remove-package-symbols.ps1') -Path $Stage
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
$Archive = Join-Path $OutputPath "EvergreenEndpointAdmin.$Version-build.$Build.$Platform.zip"
Compress-Archive -Path (Join-Path $Stage '*') -DestinationPath $Archive -CompressionLevel Optimal -Force
& (Join-Path $PSScriptRoot 'verify-release-package.ps1') -Archive $Archive -Kind Admin
Write-Host "Portable administrator package: $Archive"
