[CmdletBinding()]
param([Parameter(Mandatory)][string] $Executable)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$Workspace = Join-Path $env:TEMP "evergreen-admin-smoke-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $Workspace | Out-Null
$SelfTest = Start-Process -FilePath $Executable -ArgumentList @('--self-test', "`"$Workspace`"") -WindowStyle Hidden -PassThru
if (-not $SelfTest.WaitForExit(30000)) { $SelfTest.Kill(); throw 'Administrator document self-test timed out.' }
if ($SelfTest.ExitCode -ne 0) { throw "Administrator document self-test failed: $($SelfTest.ExitCode)." }
$Library = Get-Content -LiteralPath (Join-Path $Workspace 'SoftwareCatalog.json') -Raw | ConvertFrom-Json
if ($Library.version -ne 1 -or $Library.catalogs[0].packages[0].Id -ne 'Example.App') { throw 'Administrator library round-trip failed.' }
$Managers = Get-Content -LiteralPath (Join-Path $Workspace 'package-managers.json') -Raw | ConvertFrom-Json
if ($Managers.managers.WinGet.allowedSources -notcontains 'msstore') { throw 'Administrator manager export lost the Store source.' }
# Exercise compiled theme resources, embedded icon/font loading, and preference restoration
# in the actual published executable. This does not replace visual/accessibility review.
$AppearancePath = Join-Path (Split-Path $Executable) 'AdminData\appearance.txt'
New-Item -ItemType Directory -Path (Split-Path $AppearancePath) -Force | Out-Null
foreach ($Appearance in @('System', 'Light', 'Dark')) {
    Set-Content -LiteralPath $AppearancePath -Value $Appearance -NoNewline
    $Desktop = Start-Process -FilePath $Executable -WindowStyle Hidden -PassThru
    try {
        $Deadline = [DateTime]::UtcNow.AddSeconds(30)
        do {
            if ($Desktop.HasExited) { throw "Administrator exited during startup: $($Desktop.ExitCode)" }
            Start-Sleep -Milliseconds 250
            $Desktop.Refresh()
        } while ($Desktop.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $Deadline)
        if ($Desktop.MainWindowHandle -eq 0) { throw 'Administrator did not create a window.' }
        if ($Desktop.MainWindowTitle -ne 'Evergreen Endpoint | ADMIN') { throw "Unexpected administrator window: $($Desktop.MainWindowTitle)" }
        Start-Sleep -Milliseconds 500
        if ($Desktop.HasExited) { throw "Administrator failed while rendering $Appearance appearance: $($Desktop.ExitCode)" }
        if (-not $Desktop.CloseMainWindow()) { throw 'Administrator window could not be closed.' }
        if (-not $Desktop.WaitForExit(10000)) { throw 'Administrator did not exit after closing its clean workspace.' }
        if ($Desktop.ExitCode -ne 0) { throw "Administrator desktop failed: $($Desktop.ExitCode)" }
        if (-not (Test-Path -LiteralPath (Join-Path (Split-Path $Executable) 'AdminData\Configuration') -PathType Container)) {
            throw 'Administrator settings did not remain in its private portable data directory.'
        }
        Write-Host "Verified published administrator desktop startup and clean exit with $Appearance appearance."
    } finally {
        if (-not $Desktop.HasExited) { $Desktop.Kill(); $Desktop.WaitForExit() }
    }

}
Set-Content -LiteralPath $AppearancePath -Value 'System' -NoNewline

# Simulate an unusable portable data directory without modifying ACLs or real user state.
$FailureRoot = Join-Path $Workspace 'blocked-application'
New-Item -ItemType Directory -Path $FailureRoot | Out-Null
Get-ChildItem -LiteralPath (Split-Path $Executable) | Where-Object { $_.Name -ne 'AdminData' } |
    Copy-Item -Destination $FailureRoot -Recurse
New-Item -ItemType File -Path (Join-Path $FailureRoot 'AdminData') | Out-Null
$Blocked = Start-Process -FilePath (Join-Path $FailureRoot 'Evergreen Endpoint Admin.exe') -WindowStyle Hidden -PassThru
try {
    $Deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        if ($Blocked.HasExited) { throw 'Blocked portable startup exited without an actionable window.' }
        Start-Sleep -Milliseconds 250
        $Blocked.Refresh()
    } while ($Blocked.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $Deadline)
    if ($Blocked.MainWindowHandle -eq 0 -or $Blocked.MainWindowTitle -ne 'Evergreen Endpoint Admin') {
        throw 'Blocked portable startup did not show the bootstrap error window.'
    }
    $null = $Blocked.CloseMainWindow()
    if (-not $Blocked.WaitForExit(10000)) { throw 'Blocked portable startup did not close.' }
    if (-not (Test-Path -LiteralPath (Join-Path $FailureRoot 'AdminData') -PathType Leaf)) {
        throw 'Blocked portable startup changed its unusable data location.'
    }
    Write-Host 'Verified an unusable portable data folder reports an error instead of opening the administrator workspace.'
} finally {
    if (-not $Blocked.HasExited) { $Blocked.Kill(); $Blocked.WaitForExit() }
}
