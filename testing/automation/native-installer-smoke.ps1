# Run the real installer only on a disposable GitHub-hosted Windows runner.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Installer,
    [string]$PreviousInstaller
)

$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Installer smoke tests require a disposable GitHub-hosted runner.'
}

$Root = Join-Path $env:RUNNER_TEMP 'eepm-installer-smoke'
$null = New-Item -Path $Root -ItemType Directory -Force
$InstallDir = Join-Path $Root 'Installed'
$PortableDir = Join-Path $Root 'Portable'

# Seed an upstream installation's registrations, shortcut, and running process.
# None may be adopted, migrated, deleted, or terminated by Evergreen's installer.
$UpstreamKeys = @('Software\Classes\unigetui', 'Software\Classes\.ubundle', 'Software\Classes\UniGetUI.PackageBundle', 'Software\Devolutions\UniGetUI\InstallerPath')
foreach ($Key in $UpstreamKeys) {
    if (Test-Path "HKCU:\$Key") { throw "Runner already has an upstream registration: $Key" }
    $Registry = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($Key)
    $Registry.SetValue('', 'upstream-preservation-sentinel')
    $Registry.Dispose()
}
$Shortcut = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'WingetUI.lnk'
if (Test-Path -LiteralPath $Shortcut) { throw 'Runner already has an upstream shortcut.' }
Set-Content -LiteralPath $Shortcut -Value 'upstream-preservation-sentinel'
$UpstreamExecutable = Join-Path $Root 'UniGetUI.exe'
Copy-Item -LiteralPath (Join-Path $env:WINDIR 'System32\PING.EXE') -Destination $UpstreamExecutable
$UpstreamProcess = Start-Process -FilePath $UpstreamExecutable -ArgumentList @('-t', '127.0.0.1') -WindowStyle Hidden -PassThru

function Assert-UpstreamIntact {
    foreach ($Key in $UpstreamKeys) {
        $Registry = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($Key)
        try {
            if ($null -eq $Registry -or $Registry.GetValue('') -ne 'upstream-preservation-sentinel') {
                throw "Upstream registration changed: $Key"
            }
        } finally { if ($null -ne $Registry) { $Registry.Dispose() } }
    }
    if (-not (Test-Path -LiteralPath $Shortcut) -or
        (Get-Content -LiteralPath $Shortcut -Raw).Trim() -ne 'upstream-preservation-sentinel') {
        throw 'Upstream shortcut was changed.'
    }
    $UpstreamProcess.Refresh()
    if ($UpstreamProcess.HasExited) { throw 'Evergreen terminated the upstream process.' }
}

function Invoke-Installer([string]$Directory, [string]$Tasks, [string]$Label, [string]$InstallerPath = $Installer) {
    $Process = Start-Process -FilePath $InstallerPath -WindowStyle Hidden -PassThru -Wait -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/CURRENTUSER',
        '/NoAutoStart', '/NoRunOnStartup', '/NoVCRedist', '/NoEdgeWebView', '/NoDeployInstaller',
        "/DIR=`"$Directory`"", "/TASKS=`"$Tasks`"", "/LOG=`"$Root\$Label.log`""
    )
    if ($Process.ExitCode -ne 0) { throw "$Label failed: $($Process.ExitCode)" }
    $LauncherName = 'evergreen.exe'
    if ($InstallerPath -ne $Installer -and -not (Test-Path -LiteralPath (Join-Path $Directory $LauncherName))) {
        $LauncherName = 'uniget.exe'
    }
    & (Join-Path $Directory $LauncherName) tui --help
    if ($LASTEXITCODE -ne 0) { throw "$Label installed CLI failed." }
    Assert-UpstreamIntact
}

Invoke-Installer $InstallDir 'regularinstall' 'fresh-install'
foreach ($Marker in @('EvergreenEndpoint.portable', 'ForceUniGetUIPortable')) {
    if (Test-Path -LiteralPath (Join-Path $InstallDir $Marker)) { throw 'Regular installer leaked a portable marker.' }
}
$Protocol = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Classes\evergreen-endpoint\shell\open\command')
try {
    if ($null -eq $Protocol -or $Protocol.GetValue('') -ne "`"$InstallDir\Evergreen Endpoint Package Manager.exe`" `"%1`"") {
        throw 'Evergreen protocol registration does not target the installed application.'
    }
} finally { if ($null -ne $Protocol) { $Protocol.Dispose() } }
Invoke-Installer $InstallDir 'regularinstall' 'same-version-reinstall'
$CurrentAppHash = (Get-FileHash -LiteralPath (Join-Path $InstallDir 'Evergreen Endpoint Package Manager.exe')).Hash

$Desktop = Start-Process -FilePath (Join-Path $InstallDir 'Evergreen Endpoint Package Manager.exe') -WindowStyle Hidden -PassThru
try {
    $Deadline = [DateTime]::UtcNow.AddSeconds(60)
    do {
        Start-Sleep -Milliseconds 250
        $Desktop.Refresh()
        if ($Desktop.HasExited) { throw "Installed desktop exited during startup: $($Desktop.ExitCode)" }
    } while ($Desktop.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $Deadline)
    if ($Desktop.MainWindowHandle -eq 0) { throw 'Installed desktop did not create a window.' }
    Write-Host 'Verified installed NativeAOT desktop startup.'
    & (Join-Path $InstallDir 'evergreen.exe') 'evergreen-endpoint://show'
    if ($LASTEXITCODE -ne 0) { throw 'Evergreen protocol activation failed.' }
} finally {
    if (-not $Desktop.HasExited) {
        $null = $Desktop.CloseMainWindow()
        if (-not $Desktop.WaitForExit(10000)) { $Desktop.Kill(); $Desktop.WaitForExit() }
    }
}

function Invoke-Uninstall([string]$Label) {
    $Uninstallers = @(Get-ChildItem -LiteralPath $InstallDir -Filter 'unins*.exe')
    if ($Uninstallers.Count -ne 1) { throw 'Expected exactly one installed uninstaller.' }
    $Uninstaller = $Uninstallers[0]
    $Process = Start-Process -FilePath $Uninstaller.FullName -WindowStyle Hidden -PassThru -Wait -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$Root\$Label.log`""
    )
    if ($Process.ExitCode -ne 0) { throw "Uninstall failed: $($Process.ExitCode)" }
    if (Test-Path -LiteralPath (Join-Path $InstallDir 'Evergreen Endpoint Package Manager.exe')) {
        throw 'Uninstall left the application executable behind.'
    }
    Assert-UpstreamIntact
    if (Test-Path 'HKCU:\Software\Classes\evergreen-endpoint') {
        throw 'Uninstall left Evergreen protocol registration behind.'
    }
}

Invoke-Uninstall 'uninstall'
if ($PreviousInstaller) {
    $EnvironmentKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment')
    try {
        $OriginalUserPath = $EnvironmentKey.GetValue('Path', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        $OriginalPathKind = if ($null -ne $OriginalUserPath) { $EnvironmentKey.GetValueKind('Path') } else { $null }
    } finally { $EnvironmentKey.Dispose() }
    Invoke-Installer $InstallDir 'regularinstall,regularinstall\addtopath' 'previous-release-install' $PreviousInstaller
    Invoke-Installer $InstallDir 'regularinstall,regularinstall\addtopath' 'upgrade-from-previous-release'
    $OwnerKeyPath = 'Software\Technicull Solutions\Evergreen Endpoint\Package Manager\InstallerPath'
    $Owners = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($OwnerKeyPath)
    try {
        if ($null -eq $Owners -or -not @($Owners.GetValueNames() | Where-Object { $Owners.GetValue($_) -eq "O$InstallDir" }).Count) {
            throw 'Upgraded PATH entry does not have Evergreen-owned cleanup metadata.'
        }
    } finally { if ($null -ne $Owners) { $Owners.Dispose() } }
    $LegacyOwners = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Devolutions\UniGetUI\InstallerPath')
    try {
        if (@($LegacyOwners.GetValueNames() | Where-Object { $LegacyOwners.GetValue($_) -eq "O$InstallDir" }).Count) {
            throw 'Upgrade left Evergreen PATH ownership in the legacy product key.'
        }
    } finally { $LegacyOwners.Dispose() }
    foreach ($Retired in @('uniget.exe', 'Assets\Utilities\unigetui_ps_operation.ps1')) {
        if (Test-Path -LiteralPath (Join-Path $InstallDir $Retired)) { throw "Upgrade left a retired Evergreen file: $Retired" }
    }
    if ((Get-FileHash -LiteralPath (Join-Path $InstallDir 'Evergreen Endpoint Package Manager.exe')).Hash -ne $CurrentAppHash) {
        throw 'Upgrade did not replace the previous application with the tested current executable.'
    }
    Invoke-Uninstall 'uninstall-after-upgrade'
    $EnvironmentKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment')
    try {
        $AfterUserPath = $EnvironmentKey.GetValue('Path', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        if ($null -ne $OriginalUserPath) {
            if ($AfterUserPath -cne $OriginalUserPath -or $EnvironmentKey.GetValueKind('Path') -ne $OriginalPathKind) {
                throw 'Uninstall did not restore the original user PATH and registry type.'
            }
        } elseif (-not [string]::IsNullOrEmpty($AfterUserPath)) {
            throw 'Uninstall left an added user PATH entry.'
        }
    } finally { $EnvironmentKey.Dispose() }
    Write-Host 'Verified upgrade from the previous published release and subsequent uninstall.'
}

Invoke-Installer $PortableDir 'portableinstall' 'portable-install'
if (-not (Test-Path -LiteralPath (Join-Path $PortableDir 'EvergreenEndpoint.portable'))) {
    throw 'Portable installation marker was not created.'
}
if (Get-ChildItem -LiteralPath $PortableDir -Filter 'unins*.exe') {
    throw 'Portable installation unexpectedly registered an uninstaller.'
}
if ($PreviousInstaller) {
    Invoke-Installer $PortableDir 'portableinstall' 'previous-portable-install' $PreviousInstaller
    $SettingsDirectory = Join-Path $PortableDir 'Settings'
    New-Item -ItemType Directory -Path $SettingsDirectory -Force | Out-Null
    $PortableSentinel = Join-Path $SettingsDirectory 'upgrade-preservation.txt'
    Set-Content -LiteralPath $PortableSentinel -Value 'portable-settings-preservation'
    Invoke-Installer $PortableDir 'portableinstall' 'portable-upgrade'
    if (-not (Test-Path -LiteralPath (Join-Path $PortableDir 'EvergreenEndpoint.portable')) -or
        (Test-Path -LiteralPath (Join-Path $PortableDir 'ForceUniGetUIPortable'))) {
        throw 'Portable upgrade did not transition to the Evergreen marker.'
    }
    if ((Get-Content -LiteralPath $PortableSentinel -Raw).Trim() -ne 'portable-settings-preservation') {
        throw 'Portable upgrade changed existing settings.'
    }
}
Write-Host 'Verified fresh install, same-version reinstall, desktop startup, uninstall, and portable install.'
$UpstreamProcess.Kill()
$UpstreamProcess.WaitForExit()
Write-Host 'Verified upstream registrations, shortcut, and running process survived every installer operation.'
