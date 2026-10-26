[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Archive,
    [Parameter(Mandatory)][ValidateSet('Admin', 'Apps')][string] $Kind
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Archive).Path)
try {
    $Names = @($Zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    $Symbols = @($Names | Where-Object { $_ -like '*.pdb' })
    if ($Symbols.Count) { throw "Device package contains debug symbols: $($Symbols -join ', ')" }
    if ($Names -contains 'uniget.exe' -or $Names -contains 'Assets/Utilities/unigetui_ps_operation.ps1') {
        throw 'Device package contains retired Evergreen launcher names.'
    }
    if ($Kind -eq 'Admin') {
        foreach ($Required in @('Evergreen Endpoint Admin.exe', 'README.md', 'LICENSE', 'UPSTREAM.json', 'GITEA_CATALOG_TESTING.md', 'SoftwareCatalog.example.json', 'Assets/Licenses/Lato-OFL.txt', 'CATALOG_SOURCE_PROFILES.md', 'catalog-source.example.json', 'Brand/EvergreenEndpointAdmin.ico', 'Brand/admin-logo-light.png', 'Brand/admin-logo-dark.png', 'Brand/evergreen-endpoint-admin-logo-light.svg', 'Brand/evergreen-endpoint-admin-logo-dark.svg')) {
            if ($Names -notcontains $Required) { throw "Administrator package is missing $Required." }
        }
        $Executables = @($Names | Where-Object { $_ -like '*.exe' })
        if ($Executables.Count -ne 1 -or $Executables[0] -ne 'Evergreen Endpoint Admin.exe') {
            throw 'Administrator package must contain only its own executable.'
        }
        if ($Names | Where-Object { $_ -match '^(AdminData|self-test)/' }) {
            throw 'Administrator package contains local workspace data.'
        }
    } else {
        foreach ($Required in @('Evergreen Endpoint Package Manager.exe', 'evergreen.exe', 'Assets/Utilities/evergreen_ps_operation.ps1', 'IntegrityTree.json', 'EvergreenEndpoint.portable')) {
            if ($Names -notcontains $Required) { throw "Employee package is missing $Required." }
        }
    }
    Write-Host "Verified $Kind package manifest ($($Names.Count) entries)."
} finally {
    $Zip.Dispose()
}
