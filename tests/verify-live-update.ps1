$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'bin\Release\Panseo.exe'))
$type = $assembly.GetType('Panseo.Helpers.AutoUpdater')
$folder = Join-Path $PSScriptRoot ('update-fixtures\live-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $folder -Force)
$target = Join-Path $folder 'installed.exe'
Copy-Item -LiteralPath (Join-Path $root 'bin\Release\Panseo.exe') -Destination $target
$instance = [Activator]::CreateInstance($type, [object[]]@([string]$target))
$task = $type.GetMethod('CheckAsync').Invoke($instance,@())
$status = $task.GetAwaiter().GetResult()
$status
$updateFolder = $type.GetField('_folder',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($instance)
@{ target = $target; folder = $updateFolder } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'update-fixtures\live-result.json') -Encoding UTF8
[void]$type.GetMethod('ApplyOnExit').Invoke($instance,@())
'Verified on-disk updater launched; installation waits for this test process to exit.'
