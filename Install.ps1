param([Parameter(Mandatory = $true)][string]$GameDirectory)
$ErrorActionPreference = 'Stop'
$installer = Join-Path $PSScriptRoot 'TABQoL.Installer.exe'
if (!(Test-Path -LiteralPath $installer)) { $installer = Join-Path $PSScriptRoot 'src/TABQoL.Installer/bin/Release/net462/TABQoL.Installer.exe' }
if (!(Test-Path -LiteralPath $installer)) { throw 'Build the project or download the release ZIP first.' }
& $installer install $GameDirectory
if ($LASTEXITCODE -ne 0) { throw 'Installation failed. See the error above.' }
