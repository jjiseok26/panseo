param([string]$Version)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    if ($Version) {
        if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be major.minor.patch' }
        $infoPath = Join-Path $root 'src\Properties\AssemblyInfo.cs'
        $info = [IO.File]::ReadAllText($infoPath) -replace '(Assembly(?:File)?Version\(")[^"]+', ('${1}' + $Version + '.0')
        [IO.File]::WriteAllText($infoPath, $info, [Text.UTF8Encoding]::new($true))
    }
    $builder = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($builder) { $builderPath = $builder.Source }
    else { $builderPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe' }
    & $builderPath Panseo.csproj /t:Rebuild /p:Configuration=Release /verbosity:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    $powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    foreach ($test in 'verify-ui.ps1', 'verify-capture.ps1', 'verify-update.ps1', 'verify-zoom.ps1', 'verify-numbering.ps1') {
        & $powershell -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $root "tests\$test")
        if ($LASTEXITCODE -ne 0) { throw "Failed: $test" }
    }
    [void](New-Item -ItemType Directory -Path dist -Force)
    Copy-Item -LiteralPath bin\Release\Panseo.exe -Destination dist\Panseo.exe -Force
    Copy-Item -LiteralPath bin\Release\Panseo.exe -Destination dist\panseo_portable.exe -Force
    Compress-Archive -LiteralPath dist\panseo_portable.exe,README.md -DestinationPath dist\panseo_portable.zip -Force
    Get-FileHash -LiteralPath dist\Panseo.exe -Algorithm SHA256
} finally { Pop-Location }
