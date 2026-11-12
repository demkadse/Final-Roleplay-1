#requires -Version 7.2
[CmdletBinding()]
param([string] $PinModule = (Join-Path $PSScriptRoot '..\..\tools\CI\DalamudPin.psm1'))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module $PinModule -Force
$script:passed = 0
$script:failed = 0
$workRoot = Join-Path $PSScriptRoot ('bin\sdk-pin-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($workRoot)

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw [InvalidOperationException]::new($Message) }
}
function Assert-Rejected([scriptblock] $Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Assert-True $rejected 'Unsafe SDK input was accepted.'
}
function Invoke-Check([string] $Name, [scriptblock] $Action) {
    try { & $Action; $script:passed++; Write-Output ('PASS: ' + $Name) }
    catch { $script:failed++; Write-Output ('FAIL: ' + $Name + ' [' + $_.Exception.GetType().Name + '] ' + $_.Exception.Message) }
}

try {
    $destination = Join-Path $workRoot 'sdk'
    foreach ($entry in @('Dalamud.dll', 'runtimes/win-x64/native/library.dll', 'runtimes\win-x64\native\library.dll', 'runtimes/win-x64/')) {
        Invoke-Check ('Valid SDK path stays inside its fresh destination: ' + $entry) {
            $actual = ConvertTo-Frp1SdkEntryPath -EntryName $entry -Destination $destination
            $relative = $entry.Replace('\', '/').TrimEnd('/').Replace('/', [IO.Path]::DirectorySeparatorChar)
            Assert-True ($actual -ceq [IO.Path]::GetFullPath((Join-Path $destination $relative))) 'SDK entry resolved to an unexpected path.'
            Assert-True (-not (Test-Path -LiteralPath $destination)) 'Path validation created files before archive verification.'
        }
    }
    foreach ($entry in @('../escape.dll', 'bin/../../escape.dll', '..\escape.dll', '/absolute.dll', '\absolute.dll',
        'C:/absolute.dll', 'C:relative.dll', '//server/share/file.dll', 'bin//file.dll', './file.dll', 'bin/./file.dll',
        'bin/file.dll:stream', 'bin/file?.dll', 'bin/file*.dll', 'bin/file<.dll', 'bin/file>.dll', 'bin/file|.dll',
        'bin/file".dll', 'bin/file.', 'bin/file ', 'CON', 'bin/NUL.dll', 'COM1.txt', 'LPT9.dll',
        ('file' + [char]0 + '.dll'), ('file' + [char]10 + '.dll'), ('a' * 241), '/')) {
        Invoke-Check ('Unsafe SDK path is rejected: fixture ' + ($script:passed + $script:failed)) {
            Assert-Rejected { ConvertTo-Frp1SdkEntryPath -EntryName $entry -Destination $destination }
        }
    }
    Invoke-Check 'Exact API 15 assembly and source metadata are accepted' {
        Assert-Frp1SdkMetadata -AssemblyVersion '15.0.4.0' -CommitHash '12e318c1936e449e5883faa61214eb2ced478349'
    }
    Invoke-Check 'Commit text accepts the archive trailing newline' {
        Assert-Frp1SdkMetadata -AssemblyVersion '15.0.4.0' -CommitHash "12e318c1936e449e5883faa61214eb2ced478349`n"
    }
    foreach ($version in @('15.0.3.0', '15.0.4', '16.0.0.0', '17.0.0.0', 'latest')) {
        Invoke-Check ('Different assembly/API metadata is rejected: ' + $version) {
            Assert-Rejected { Assert-Frp1SdkMetadata -AssemblyVersion $version -CommitHash '12e318c1936e449e5883faa61214eb2ced478349' }
        }
    }
    Invoke-Check 'A different SDK source commit is rejected despite the correct API version' {
        Assert-Rejected { Assert-Frp1SdkMetadata -AssemblyVersion '15.0.4.0' -CommitHash ('a' * 40) }
    }
    Invoke-Check 'A differently cased source commit is rejected by the exact pin' {
        Assert-Rejected { Assert-Frp1SdkMetadata -AssemblyVersion '15.0.4.0' -CommitHash '12E318C1936E449E5883FAA61214EB2CED478349' }
    }
    $archive = Join-Path $workRoot 'invalid-sdk.zip'
    Invoke-Check 'A truncated download is rejected before extraction' {
        [IO.File]::WriteAllText($archive, 'Synthetic offline SDK download fixture')
        Assert-Rejected { Assert-Frp1PinnedDownload -ArchivePath $archive }
        Assert-True (-not (Test-Path -LiteralPath $destination)) 'Rejected download created an SDK destination.'
    }
    Invoke-Check 'Correct archive size with wrong bytes is rejected by SHA-256' {
        $stream = [IO.File]::Open($archive, [IO.FileMode]::Create, [IO.FileAccess]::Write)
        try { $stream.SetLength(49840952) } finally { $stream.Dispose() }
        Assert-True ((Get-Item -LiteralPath $archive).Length -eq 49840952) 'Hash fixture does not reach the exact-size branch.'
        Assert-Rejected { Assert-Frp1PinnedDownload -ArchivePath $archive }
    }
    Invoke-Check 'Installer refuses an existing destination without downloading or changing it' {
        [void][IO.Directory]::CreateDirectory($destination)
        $marker = Join-Path $destination 'existing.txt'
        [IO.File]::WriteAllText($marker, 'Keep existing SDK untouched')
        $rejection = $null
        try { & (Join-Path $PSScriptRoot '..\..\tools\CI\Install-PinnedDalamud.ps1') -Destination $destination | Out-Null }
        catch { $rejection = $_.Exception.Message }
        Assert-True ($rejection -ceq 'Pinned SDK destination must be new.') 'Installer did not reject the existing target at its pre-download guard.'
        Assert-True ([IO.File]::ReadAllText($marker) -ceq 'Keep existing SDK untouched') 'Existing SDK content was changed.'
        Assert-True (@(Get-ChildItem -LiteralPath $destination -Force).Count -eq 1) 'Existing SDK acquired unexpected files.'
    }
}
finally {
    if (Test-Path -LiteralPath $workRoot) {
        $resolved = (Resolve-Path -LiteralPath $workRoot).Path
        $parent = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'bin')).TrimEnd('\') + '\'
        if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or
            -not [IO.Path]::GetFileName($resolved).StartsWith('sdk-pin-', [StringComparison]::Ordinal)) { throw 'Unsafe test cleanup target.' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
Write-Output ('Pinned SDK checks: ' + $script:passed + ' passed, ' + $script:failed + ' failed.')
if ($script:failed -ne 0) { throw ('Offline SDK pin verification failed: ' + $script:failed + ' checks.') }
