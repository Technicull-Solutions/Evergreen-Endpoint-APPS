#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Stamps version information into all required source files.
    CI-friendly version stamping script for local builds and CI.

.PARAMETER Version
    Evergreen release version, e.g. "0.1.0" or a manually selected dated release version.
    A four-part version (X.X.X.X) is derived automatically for assembly info.

.PARAMETER BuildNumber
    Explicit build number. When omitted, increments scripts/BuildNumber.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string] $Version,

    [ValidateRange(1, 65535)]
    [int] $BuildNumber
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

# Validate before changing any files. Windows version components are unsigned 16-bit values.
$CleanVersion = ($Version -Split '-')[0]   # strip prerelease tag
$Parts = $CleanVersion -Split '\.'
if ($Parts | Where-Object { [long]$_ -gt 65535 }) { throw 'Windows version components must not exceed 65535.' }
if ($Parts | Where-Object { $_ -match '^0\d' }) { throw 'Version components must not have leading zeros.' }
if ($Version.Contains('-')) {
    $Labels = ($Version -split '-', 2)[1] -split '\.'
    if (@($Labels | Where-Object { -not $_ -or $_ -match '^0\d+$' }).Count -gt 0) { throw 'Invalid SemVer prerelease identifier.' }
}

# --- Bump build number ---
$BuildNumberFile = Join-Path $PSScriptRoot "BuildNumber"
if (-not $PSBoundParameters.ContainsKey('BuildNumber')) {
    $BuildNumber = 0
    if (Test-Path $BuildNumberFile) {
        $BuildNumber = [int](Get-Content $BuildNumberFile -Raw).Trim()
    }
    $BuildNumber++
}
if ($BuildNumber -lt 1 -or $BuildNumber -gt 65535) { throw 'Build number must be between 1 and 65535.' }
$FourPartVersion = "$CleanVersion.$BuildNumber"
Write-Host "Version name  : $Version"
Write-Host "Windows ver   : $FourPartVersion"
Set-Content $BuildNumberFile $BuildNumber
Write-Host "Build number  : $BuildNumber"

# --- Helper: replace lines in a file by prefix match ---
function Set-LinesByPrefix {
    param(
        [string] $FilePath,
        [hashtable] $Replacements,
        [string] $Encoding = 'utf8BOM'
    )

    if (-not (Test-Path $FilePath)) {
        Write-Warning "File not found, skipping: $FilePath"
        return
    }

    $lines = Get-Content $FilePath -Encoding $Encoding
    $output = foreach ($line in $lines) {
        $matched = $false
        foreach ($prefix in $Replacements.Keys) {
            if ($line.TrimStart().StartsWith($prefix)) {
                $Replacements[$prefix]
                $matched = $true
                break
            }
        }
        if (-not $matched) { $line }
    }
    $output | Set-Content $FilePath -Encoding $Encoding
}

# --- CoreData.cs ---
Set-LinesByPrefix -FilePath (Join-Path $RepoRoot "src" "UniGetUI.Core.Data" "CoreData.cs") -Replacements @{
    'public const string VersionName =' = "        public const string VersionName = `"$Version`"; // Do not modify this line, use file scripts/set-version.ps1"
    'public const int BuildNumber ='    = "        public const int BuildNumber = $BuildNumber; // Do not modify this line, use file scripts/set-version.ps1"
}

# --- SharedAssemblyInfo.cs ---
Set-LinesByPrefix -FilePath (Join-Path $RepoRoot "src" "SharedAssemblyInfo.cs") -Replacements @{
    '[assembly: AssemblyVersion("'              = "[assembly: AssemblyVersion(`"$FourPartVersion`")]"
    '[assembly: AssemblyFileVersion("'          = "[assembly: AssemblyFileVersion(`"$FourPartVersion`")]"
    '[assembly: AssemblyInformationalVersion("' = "[assembly: AssemblyInformationalVersion(`"$Version`")]"
}

# --- UniGetUI.iss ---
Set-LinesByPrefix -FilePath (Join-Path $RepoRoot "UniGetUI.iss") -Replacements @{
    '#define MyAppVersion'   = "#define MyAppVersion `"$Version`""
    'VersionInfoVersion='    = "VersionInfoVersion=$FourPartVersion"
    'VersionInfoProductVersion=' = "VersionInfoProductVersion=$FourPartVersion"
}

# --- app manifests (only the assemblyIdentity version, not manifestVersion) ---
$ManifestPaths = @(
    (Join-Path $RepoRoot "src" "UniGetUI.Avalonia" "app.manifest"),
    (Join-Path $RepoRoot "src" "UniGetUI.AgentPolicy.ElevatedHelper" "app.manifest")
)
foreach ($ManifestPath in $ManifestPaths) {
    if (Test-Path $ManifestPath) {
        $content = Get-Content $ManifestPath -Raw -Encoding utf8BOM
        $content = $content -Replace '(?s)(<assemblyIdentity\b.*?\bversion\s*=\s*")[^"]*(")', "`${1}$FourPartVersion`${2}"
        Set-Content $ManifestPath $content -Encoding utf8BOM -NoNewline
    }
}

# Keep the owned update metadata in the same build-number series. Never advertise
# a previous build's installer as a new version; release assets are attached separately.
$ProductInfoPath = Join-Path $RepoRoot 'update/productinfo.json'
$ProductInfo = Get-Content -LiteralPath $ProductInfoPath -Raw | ConvertFrom-Json
$Product = $ProductInfo.'TechnicullSolutions.EvergreenEndpointPackageManager'
$ChannelName = if ($Version.Contains('-')) { 'Beta' } else { 'Current' }
if ($null -eq $Product.$ChannelName) {
    $Product.$ChannelName = [pscustomobject]@{ Version = ''; BuildVersion = ''; BuildNumber = 0; Files = @() }
}
$Channel = $Product.$ChannelName
if ($Channel.Version -ne $Version -or $Channel.BuildNumber -ne $BuildNumber) {
    $Channel.Files = @()
}
$Channel.Version = $Version
$Channel.BuildVersion = [string]$BuildNumber
$Channel.BuildNumber = $BuildNumber
$ProductInfo | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $ProductInfoPath

Write-Host "Version stamped successfully."
