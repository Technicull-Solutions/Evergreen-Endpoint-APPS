$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$Fixture = Join-Path ([IO.Path]::GetTempPath()) "evergreen-integrity-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $Fixture, (Join-Path $Fixture 'nested') | Out-Null
Set-Content -LiteralPath (Join-Path $Fixture 'payload.exe') -Value 'inert integrity fixture'
Set-Content -LiteralPath (Join-Path $Fixture 'nested/EvergreenEndpoint.portable') -Value 'nested files remain hashed'
New-Item -ItemType File -Path (Join-Path $Fixture 'EvergreenEndpoint.portable') | Out-Null
& (Join-Path $Repo 'scripts/refresh-integrity-tree.ps1') -Path $Fixture -FailOnUnexpectedFiles
$Tree = Get-Content -LiteralPath (Join-Path $Fixture 'IntegrityTree.json') -Raw | ConvertFrom-Json -AsHashtable
if ($Tree.ContainsKey('EvergreenEndpoint.portable') -or -not $Tree.ContainsKey('nested/EvergreenEndpoint.portable')) {
    throw 'The optional marker exclusion escaped its root-only scope.'
}
Remove-Item -LiteralPath (Join-Path $Fixture 'EvergreenEndpoint.portable')
& (Join-Path $Repo 'scripts/verify-integrity-tree.ps1') -Path $Fixture -FailOnUnexpectedFiles
Set-Content -LiteralPath (Join-Path $Fixture 'unexpected.dll') -Value 'must be rejected'
$Rejected = $false
try { & (Join-Path $Repo 'scripts/verify-integrity-tree.ps1') -Path $Fixture -FailOnUnexpectedFiles } catch { $Rejected = $true }
if (-not $Rejected) { throw 'Unexpected executable payload was accepted.' }
Remove-Item -LiteralPath (Join-Path $Fixture 'unexpected.dll')
Set-Content -LiteralPath (Join-Path $Fixture 'payload.exe') -Value 'changed fixture'
$Rejected = $false
try { & (Join-Path $Repo 'scripts/verify-integrity-tree.ps1') -Path $Fixture -FailOnUnexpectedFiles } catch { $Rejected = $true }
if (-not $Rejected) { throw 'A modified executable passed integrity verification.' }
Write-Host "Portable integrity tests passed. Fixture retained at $Fixture"
