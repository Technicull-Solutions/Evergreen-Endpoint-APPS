[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Path
)

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path -LiteralPath $Path).Path
if (-not (Test-Path -LiteralPath $Root -PathType Container)) { throw 'Package root must be a directory.' }
# Debug symbols belong in build artifacts, never in the device installation package.
$Symbols = @(Get-ChildItem -LiteralPath $Root -Filter '*.pdb' -File -Recurse)
foreach ($Symbol in $Symbols) {
    Remove-Item -LiteralPath $Symbol.FullName -Force
}
if (Get-ChildItem -LiteralPath $Root -Filter '*.pdb' -File -Recurse) {
    throw 'Debug symbols remain in the package.'
}
Write-Host "Excluded $($Symbols.Count) debug symbol files from the package."
