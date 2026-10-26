#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds Evergreen Endpoint Package Manager and packages its published output.

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release.

.PARAMETER Platform
    Target platform. Default: x64.

.PARAMETER OutputPath
    Directory for final packaged artifacts (zip, installer). Default: ./output

.PARAMETER SkipTests
    Skip running dotnet test before build.

.PARAMETER SkipInstaller
    Skip building the Inno Setup installer.

.PARAMETER Version
    Evergreen release version to stamp into the build (e.g. "0.1.0"). If not provided,
    the current version from SharedAssemblyInfo.cs is used.

.PARAMETER MaxInstallerCompression
    Use the strongest Inno Setup compression settings for the installer.
#>
[CmdletBinding()]
param(
    [string] $Configuration = "Release",
    [string] $Platform = "x64",
    [string] $OutputPath = (Join-Path $PSScriptRoot ".." "output"),
    [switch] $SkipTests,
    [switch] $SkipInstaller,
    [switch] $MaxInstallerCompression,
    [string] $Version
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$SrcDir = Join-Path $RepoRoot "src"
$WindowsSolution = Join-Path $SrcDir "UniGetUI.Windows.slnx"
$PublishProject = Join-Path $SrcDir "UniGetUI.Avalonia" "UniGetUI.Avalonia.csproj"
$BinDir = Join-Path $RepoRoot "unigetui_bin"
$BuildPropsPath = Join-Path $SrcDir "Directory.Build.props"
[xml] $BuildProps = Get-Content $BuildPropsPath
$PortableTargetFramework = @($BuildProps.Project.PropertyGroup | Where-Object { $_.PortableTargetFramework } | Select-Object -First 1).PortableTargetFramework
$WindowsTargetPlatformVersion = @($BuildProps.Project.PropertyGroup | Where-Object { $_.WindowsTargetPlatformVersion } | Select-Object -First 1).WindowsTargetPlatformVersion

if ([string]::IsNullOrWhiteSpace($PortableTargetFramework) -or [string]::IsNullOrWhiteSpace($WindowsTargetPlatformVersion)) {
    throw "Could not resolve the target framework from $BuildPropsPath"
}

$TargetFramework = "$PortableTargetFramework-windows$WindowsTargetPlatformVersion"
$PublishDir = Join-Path $SrcDir "UniGetUI.Avalonia" "bin" $Platform $Configuration $TargetFramework "win-$Platform" "publish"

# --- Version stamping ---
if ($Version) {
    Write-Host "Stamping version: $Version"
    & (Join-Path $PSScriptRoot "set-version.ps1") -Version $Version
}

# --- Read version from SharedAssemblyInfo.cs ---
$AssemblyInfoPath = Join-Path $SrcDir "SharedAssemblyInfo.cs"
$VersionMatch = Select-String -Path $AssemblyInfoPath -Pattern 'AssemblyInformationalVersion\("([^"]+)"\)'
$PackageVersion = if ($VersionMatch) { $VersionMatch.Matches[0].Groups[1].Value } else { "0.0.0" }
Write-Host "Building Evergreen Endpoint Package Manager version: $PackageVersion"

$StampedBuild = [int](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'BuildNumber') -Raw).Trim()
$ProductInfo = Get-Content -LiteralPath (Join-Path $RepoRoot 'update/productinfo.json') -Raw | ConvertFrom-Json
$ChannelName = if ($PackageVersion.Contains('-')) { 'Beta' } else { 'Current' }
$Channel = $ProductInfo.'TechnicullSolutions.EvergreenEndpointPackageManager'.$ChannelName
if ($Channel.Version -ne $PackageVersion -or $Channel.BuildNumber -ne $StampedBuild -or $Channel.BuildVersion -ne [string]$StampedBuild) {
    throw 'Release metadata is out of sync. Run scripts/set-version.ps1 with the intended version and build number.'
}

# --- Test ---
if (-not $SkipTests) {
    Write-Host "`n=== Running tests ===" -ForegroundColor Cyan
    dotnet test $WindowsSolution --verbosity minimal --nologo --blame-hang-timeout 5m --blame-hang-dump-type none --logger "console;verbosity=normal" --logger "trx;LogFilePrefix=tests" --results-directory (Join-Path $RepoRoot "artifacts/test-results") /p:RestoreIgnoreFailedSources=true /p:Platform=$Platform /p:UseSharedCompilation=false /m:1
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed with exit code $LASTEXITCODE"
    }
}

# --- Build / Publish ---
Write-Host "`n=== Publishing $Configuration|$Platform ===" -ForegroundColor Cyan
dotnet clean $WindowsSolution -v m --nologo /p:Platform=$Platform

dotnet publish $PublishProject /noLogo /m:1 /p:Configuration=$Configuration /p:Platform=$Platform -p:RuntimeIdentifier=win-$Platform /p:TrimmerSingleWarn=false /p:ILLinkTreatWarningsAsErrors=true /p:IlcTreatWarningsAsErrors=true --ignore-failed-sources -v m
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish Avalonia failed with exit code $LASTEXITCODE"
}

# --- Stage binaries ---
if (Test-Path $BinDir) { Remove-Item $BinDir -Recurse -Force }
New-Item $BinDir -ItemType Directory | Out-Null
# Move published output into unigetui_bin, then give the Windows app host its
# customer-facing product name without changing the internal assembly identity.
Get-ChildItem $PublishDir | Move-Item -Destination $BinDir -Force

$PublishedWindowsAppHostPath = Join-Path $BinDir "UniGetUI.exe"
$WindowsAppHostPath = Join-Path $BinDir "Evergreen Endpoint Package Manager.exe"
if (-not (Test-Path $PublishedWindowsAppHostPath)) {
    throw "Windows app host was not produced at $PublishedWindowsAppHostPath"
}
Move-Item -LiteralPath $PublishedWindowsAppHostPath -Destination $WindowsAppHostPath -Force
if (-not (Test-Path $WindowsAppHostPath)) {
    throw "Windows app host was not produced at $WindowsAppHostPath"
}

$CliLauncherPath = Join-Path $BinDir "evergreen.exe"
if (-not (Test-Path -LiteralPath $CliLauncherPath -PathType Leaf)) {
    throw "NativeAOT CLI launcher was not staged for win-$Platform at $CliLauncherPath"
}

# The elevated policy-write helper is authenticated by exact path at runtime, so a missing or
# misplaced helper must fail the build rather than silently ship an install that cannot elevate.
$PolicyElevatorPath = Join-Path $BinDir "Assets\Utilities\EEPM Policy Elevator.exe"
if (-not (Test-Path $PolicyElevatorPath)) {
    throw "Elevated policy helper was not staged at $PolicyElevatorPath"
}

& (Join-Path $PSScriptRoot 'remove-package-symbols.ps1') -Path $BinDir

# --- Package output ---
if (Test-Path $OutputPath) { Remove-Item $OutputPath -Recurse -Force }
New-Item $OutputPath -ItemType Directory | Out-Null

$BuildNumberPath = Join-Path $PSScriptRoot "BuildNumber"
$BuildNumber = (Get-Content -LiteralPath $BuildNumberPath -Raw).Trim()
$ArtifactStem = "EvergreenEndpointPackageManager.$PackageVersion-build.$BuildNumber.$Platform"
$ZipPath = Join-Path $OutputPath "$ArtifactStem.zip"
Write-Host "`n=== Refreshing integrity tree before zip packaging ===" -ForegroundColor Cyan
Copy-Item -LiteralPath (Join-Path $RepoRoot 'InstallerExtras\EvergreenEndpoint.portable') -Destination $BinDir
& (Join-Path $PSScriptRoot "refresh-integrity-tree.ps1") -Path $BinDir -FailOnUnexpectedFiles

Write-Host "`n=== Creating zip: $ZipPath ===" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $BinDir "*") -DestinationPath $ZipPath -CompressionLevel Optimal
& (Join-Path $PSScriptRoot 'verify-release-package.ps1') -Archive $ZipPath -Kind Apps
# The ZIP is portable; the installer task controls portable mode independently.
Remove-Item -LiteralPath (Join-Path $BinDir 'EvergreenEndpoint.portable')

# Package the ADMX/ADML files in the PolicyDefinitions layout expected by local and domain GPO stores.
$PolicyArtifactPath = Join-Path $OutputPath "EvergreenEndpointPackageManager.Policies.$PackageVersion-build.$BuildNumber.zip"
$PolicyStage = Join-Path $OutputPath ".policy-staging"
$PolicyDefinitionsStage = Join-Path $PolicyStage "PolicyDefinitions"
$PolicyLocaleStage = Join-Path $PolicyDefinitionsStage "en-US"
New-Item $PolicyLocaleStage -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $RepoRoot "package\Policies\EvergreenEndpointPackageManager.admx") $PolicyDefinitionsStage
Copy-Item (Join-Path $RepoRoot "package\Policies\en-US\EvergreenEndpointPackageManager.adml") $PolicyLocaleStage
Copy-Item (Join-Path $RepoRoot "package\Policies\README.md") $PolicyStage
Compress-Archive -Path (Join-Path $PolicyStage "*") -DestinationPath $PolicyArtifactPath -CompressionLevel Optimal
Remove-Item $PolicyStage -Recurse -Force

# --- Installer (Inno Setup) ---
if (-not $SkipInstaller) {
    $IsccPath = $null
    # Search common install locations
    foreach ($candidate in @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )) {
        if (Test-Path $candidate) { $IsccPath = $candidate; break }
    }

    if ($IsccPath) {
        Write-Host "`n=== Building installer ===" -ForegroundColor Cyan
        $InstallerBaseName = "$ArtifactStem.Setup"
        $IssPath = Join-Path $RepoRoot "UniGetUI.iss"
        $IssContent = Get-Content $IssPath -Raw

        Write-Host "`n=== Refreshing integrity tree before installer packaging ===" -ForegroundColor Cyan
        & (Join-Path $PSScriptRoot "refresh-integrity-tree.ps1") -Path $BinDir -FailOnUnexpectedFiles

        try {
            $IssContentNoSign = $IssContent -Replace '(?m)^SignTool=.*$', '; SignTool=azsign (disabled for local build)'
            $IssContentNoSign = $IssContentNoSign -Replace '(?m)^SignedUninstaller=yes', 'SignedUninstaller=no'
            Set-Content $IssPath $IssContentNoSign -NoNewline

            $IsccArgs = @(
                "/DMyBuildNumber=$BuildNumber",
                $IssPath,
                "/F$InstallerBaseName",
                "/O$OutputPath"
            )
            if ($MaxInstallerCompression) {
                Write-Host "Using lzma/ultra64 installer compression."
                $IsccArgs = @('/DInstallerCompression=lzma/ultra64') + $IsccArgs
            }

            & $IsccPath @IsccArgs
            if ($LASTEXITCODE -ne 0) {
                throw "Inno Setup failed with exit code $LASTEXITCODE"
            }
        }
        finally {
            Set-Content $IssPath $IssContent -NoNewline
        }
    } else {
        Write-Warning "Inno Setup 6 (ISCC.exe) not found — skipping installer build."
    }
}

# --- Checksums ---
& (Join-Path $PSScriptRoot 'package-admin.ps1') -Platform $Platform -OutputPath $OutputPath

Write-Host "`n=== Checksums ===" -ForegroundColor Cyan
$ChecksumFile = Join-Path $OutputPath "checksums.$Platform.txt"
Get-ChildItem $OutputPath -File | Where-Object { $_.Name -notlike "checksums.*.txt" } | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    "$hash  $($_.Name)" | Tee-Object -FilePath $ChecksumFile -Append
}

# --- Cleanup ---
if (Test-Path $BinDir) { Remove-Item $BinDir -Recurse -Force }

Write-Host "`n=== Build complete ===" -ForegroundColor Green
Write-Host "Artifacts in: $OutputPath"
