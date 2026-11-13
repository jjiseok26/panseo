$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Web.Extensions
$root = Split-Path $PSScriptRoot -Parent
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'bin\Release\Panseo.exe'))
$updater = $assembly.GetType('Panseo.Helpers.AutoUpdater')
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$parse = $updater.GetMethod('ParseRelease', $flags)
$verify = $updater.GetMethod('VerifyFile', $flags)
$quote = $updater.GetMethod('Quote', $flags)
if ($quote.Invoke($null, @("C:\O'Brien\a.exe")) -ne "'C:\O''Brien\a.exe'") { throw 'Path quoting failed' }
$fixtures = Join-Path $PSScriptRoot ('update-fixtures\' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $fixtures -Force)
$target = Join-Path $fixtures 'installed.exe'
$source = Join-Path $fixtures 'download.exe'
Add-Type -TypeDefinition 'using System.Reflection; [assembly:AssemblyVersion("1.0.0.0")] public class OldApp { public static void Main() {} }' -OutputAssembly $target -OutputType WindowsApplication
Add-Type -TypeDefinition 'using System.Reflection; [assembly:AssemblyVersion("1.1.0.0")] public class NewApp { public static void Main() {} }' -OutputAssembly $source -OutputType WindowsApplication
$hash = [string](Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$release = @{
    tag_name = 'v1.1.0'; draft = $false; prerelease = $false
    assets = @(@{ name = 'Panseo.exe'; browser_download_url = 'https://github.com/jjiseok26/panseo/releases/download/v1.1.0/Panseo.exe'; digest = 'sha256:' + $hash.ToLowerInvariant(); size = (Get-Item -LiteralPath $source).Length })
}
$json = $release | ConvertTo-Json -Depth 6
$info = $parse.Invoke($null, @([string]$json, [version]'1.0.0.0'))
if ($null -eq $info -or -not $verify.Invoke($null,@([string]$source,$info))) { throw 'Valid update rejected' }
if ($null -ne $parse.Invoke($null,@([string]$json,[version]'1.1.0.0'))) { throw 'Same version accepted' }
if ($verify.Invoke($null,@([string]$target,$info))) { throw 'Incorrect payload accepted' }
$release.prerelease = $true
if ($null -ne $parse.Invoke($null,@([string]($release | ConvertTo-Json -Depth 6),[version]'1.0.0.0'))) { throw 'Prerelease accepted' }
$release.prerelease = $false
foreach ($badUrl in 'https://example.com/Panseo.exe', 'http://github.com/jjiseok26/panseo/releases/download/v1.1.0/Panseo.exe') {
    $release.assets[0].browser_download_url = $badUrl
    $rejected = $false
    try { [void]$parse.Invoke($null,@([string]($release | ConvertTo-Json -Depth 6),[version]'1.0.0.0')) } catch { $rejected = $true }
    if (-not $rejected) { throw 'Untrusted update URL accepted' }
}
$release.assets[0].browser_download_url = 'https://github.com/jjiseok26/panseo/releases/download/v1.1.0/Panseo.exe'
$release.assets[0].digest = $null
$rejected = $false
try { [void]$parse.Invoke($null,@([string]($release | ConvertTo-Json -Depth 6),[version]'1.0.0.0')) } catch { $rejected = $true }
if (-not $rejected) { throw 'Missing SHA-256 accepted' }
$metadata = Join-Path $fixtures 'release.json'
$log = Join-Path $fixtures 'update.log'
[IO.File]::WriteAllText($metadata,$json)
$oldHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
$powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$script = Join-Path $root 'src\Helpers\ApplyUpdate.ps1'
& $powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $script -ProcessId 0 -SourcePath $source -TargetPath $target -ExpectedHash $hash -MetadataPath $metadata -LogPath $log
if ($LASTEXITCODE -ne 0 -or (Get-FileHash -LiteralPath $target).Hash -ne $hash -or (Get-FileHash -LiteralPath ($target+'.update-backup')).Hash -ne $oldHash) { throw 'Atomic update/backup failed' }
Copy-Item -LiteralPath $target -Destination $source
Copy-Item -LiteralPath ($target+'.update-backup') -Destination $target -Force
[IO.File]::WriteAllText($metadata,$json)
& $powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $script -ProcessId 0 -SourcePath $source -TargetPath $target -ExpectedHash ('0'*64) -MetadataPath $metadata -LogPath $log
if ($LASTEXITCODE -eq 0 -or (Get-FileHash -LiteralPath $target).Hash -ne $oldHash) { throw 'Integrity failure modified installed application' }
# The released-button race must neither call WPF DragMove nor show an error dialog.
if ([Windows.Input.Mouse]::LeftButton -eq [Windows.Input.MouseButtonState]::Released) {
    $window = [Panseo.Views.FloatingToolbarWindow]::new($null)
    [Panseo.Native.Win32]::TryDragMove($window)
    $instanceFlags = [Reflection.BindingFlags]'Instance,NonPublic'
    $field = $window.GetType().GetField('_isMouseDownOnCircle',$instanceFlags)
    $field.SetValue($window,$true)
    $args = @($window.FindName('FloatingCircle'),[Windows.Input.MouseEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0))
    [void]$window.GetType().GetMethod('FloatingCircle_MouseMove',$instanceFlags).Invoke($window,$args)
    if ($field.GetValue($window)) { throw 'Released drag state not reset' }
}
'PASS: update validation, hash mismatch rejection, atomic replacement/backup, rollback safety, released-button drag guard.'
