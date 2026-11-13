param(
    [int]$ProcessId,
    [Parameter(Mandatory=$true)][string]$SourcePath,
    [Parameter(Mandatory=$true)][string]$TargetPath,
    [Parameter(Mandatory=$true)][string]$ExpectedHash,
    [Parameter(Mandatory=$true)][string]$MetadataPath,
    [Parameter(Mandatory=$true)][string]$LogPath
)
$ErrorActionPreference = 'Stop'
try {
    if ($ProcessId -gt 0) {
        $running = $null
        try { $running = [Diagnostics.Process]::GetProcessById($ProcessId) } catch [ArgumentException] { }
        if ($null -ne $running) {
            try { if (-not $running.WaitForExit(120000)) { throw 'Program is still running; update deferred.' } }
            finally { $running.Dispose() }
        }
    }
    if ((Get-FileHash -LiteralPath $SourcePath -Algorithm SHA256).Hash -ne $ExpectedHash) { throw 'Update SHA-256 mismatch.' }
    $newVersion = [Reflection.AssemblyName]::GetAssemblyName($SourcePath).Version
    if ([Reflection.AssemblyName]::GetAssemblyName($TargetPath).Version -ge $newVersion) { return }
    # Copy onto the target volume before the atomic replace; never terminate the application.
    $replacement = $TargetPath + '.update-new'
    $backup = $TargetPath + '.update-backup'
    [IO.File]::Copy($SourcePath, $replacement, $true)
    if ((Get-FileHash -LiteralPath $replacement -Algorithm SHA256).Hash -ne $ExpectedHash) { throw 'Copied update SHA-256 mismatch.' }
    $replaced = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try { [IO.File]::Replace($replacement, $TargetPath, $backup, $true); $replaced = $true; break }
        catch [IO.IOException] { Start-Sleep -Milliseconds 200 }
    }
    if (-not $replaced) { throw 'Executable is locked; update deferred until a later exit.' }
    [IO.File]::Delete($SourcePath)
    [IO.File]::Delete($MetadataPath)
    Add-Content -LiteralPath $LogPath -Value ((Get-Date -Format s) + ' Updated to ' + $newVersion)
} catch {
    Add-Content -LiteralPath $LogPath -Value ((Get-Date -Format s) + ' ' + $_.Exception.Message)
    exit 1
}
