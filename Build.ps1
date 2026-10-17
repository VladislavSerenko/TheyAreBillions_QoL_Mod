param([switch]$Package, [string[]]$OriginalExe)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
Push-Location -LiteralPath $projectRoot
try {
    & dotnet build 'src/TABQoL.Installer/TABQoL.Installer.csproj' -c Release --nologo -m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
    & dotnet build 'tests/TABQoL.Tests/TABQoL.Tests.csproj' -c Release --nologo -m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    $testArguments = @()
    foreach ($original in $OriginalExe) { $testArguments += (Resolve-Path -LiteralPath $original).Path }
    & './tests/TABQoL.Tests/bin/Release/net462/TABQoL.Tests.exe' @testArguments
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    if ($Package) {
        $release = Join-Path $projectRoot 'artifacts/TABQoL-0.3.0'
        New-Item -ItemType Directory -Path $release -Force | Out-Null
        $source = Join-Path $projectRoot 'src/TABQoL.Installer/bin/Release/net462'
        $binaryNames = @('TABQoL.Installer.exe','TABQoL.Installer.exe.config','TABQoL.Runtime.dll','0Harmony.dll','dnlib.dll')
        $documentNames = @('Install.ps1','Uninstall.ps1','INSTALLATION.md','README.md','README.ru.md','VALIDATION.md','LICENSE','THIRD-PARTY-NOTICES.md')
        foreach ($name in $binaryNames) {
            Copy-Item -LiteralPath (Join-Path $source $name) -Destination $release
        }
        foreach ($name in $documentNames) {
            Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $release
        }
        $packageFiles = @($binaryNames + $documentNames | ForEach-Object { Join-Path $release $_ })
        Compress-Archive -LiteralPath $packageFiles -DestinationPath (Join-Path $projectRoot 'artifacts/TABQoL-0.3.0.zip') -Force
        Get-FileHash -LiteralPath (Join-Path $projectRoot 'artifacts/TABQoL-0.3.0.zip') -Algorithm SHA256
    }
} finally { Pop-Location }
