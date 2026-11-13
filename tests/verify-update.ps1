$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Web.Extensions
$root = Split-Path $PSScriptRoot -Parent
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'bin\Release\Panseo.exe'))
$updater = $assembly.GetType('Panseo.Helpers.AutoUpdater')
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$parse = $updater.GetMethod('ParseRelease', $flags)
$verify = $updater.GetMethod('VerifyFile', $flags)
$quote = $updater.GetMethod('QuoteExecutablePath', $flags)
if ($quote.Invoke($null, @("C:\O'Brien\a.exe")) -ne '"C:\O''Brien\a.exe"') { throw 'Path quoting failed' }
if ($assembly.GetManifestResourceNames() -match '\.ps1$') { throw 'Runtime script resource still embedded' }
$fixtures = Join-Path $PSScriptRoot ('update-fixtures\' + [Guid]::NewGuid().ToString('N'))
$oldFolder = Join-Path $fixtures 'old'
$newFolder = Join-Path $fixtures 'new'
[void](New-Item -ItemType Directory -Path $oldFolder,$newFolder -Force)
$target = Join-Path $oldFolder 'Panseo.exe'
$source = Join-Path $newFolder 'Panseo.exe'
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
$oldHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
$install = $updater.GetMethod('InstallVerifiedUpdate', $flags)
[void]$install.Invoke($null,@([string]$source,[string]$target,$info))
if ((Get-FileHash -LiteralPath $target).Hash -ne $hash -or (Get-FileHash -LiteralPath ($target+'.update-backup')).Hash -ne $oldHash) { throw 'Native atomic update/backup failed' }
[void]$install.Invoke($null,@([string]$source,[string]$target,$info))
if ((Get-FileHash -LiteralPath ($target+'.update-backup')).Hash -ne $oldHash) { throw 'Same-version install altered backup' }
Copy-Item -LiteralPath ($target+'.update-backup') -Destination $target -Force
$info.assets[0].digest = 'sha256:' + ('0'*64)
$rejected = $false
try { [void]$install.Invoke($null,@([string]$source,[string]$target,$info)) } catch { $rejected = $true }
if (-not $rejected -or (Get-FileHash -LiteralPath $target).Hash -ne $oldHash) { throw 'Integrity failure modified installed application' }
$entry = $updater.GetMethod('ApplyUpdateFromArguments',$flags)
$entryArgs = [object[]]::new(1)
$entryArgs[0] = [string[]]@('--apply-update','1',[string]$target)
$rejected = $false
try { [void]$entry.Invoke($null,$entryArgs) } catch { $rejected = $true }
if (-not $rejected) { throw 'Updater accepted execution outside verified update folder' }
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
'PASS: update validation, native atomic replacement/backup, bad hash rejection, update-entry validation, released-button drag guard.'
