#requires -Version 7.2
[CmdletBinding()]
param(
    [string] $PolicyModule = (Join-Path $PSScriptRoot '..\..\tools\CI\ReleasePolicy.psm1')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module $PolicyModule -Force
$script:passed = 0
$script:failed = 0
$source = 'a' * 40
$otherSource = 'b' * 40
$hash = 'A' * 64
$otherHash = 'B' * 64

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw [InvalidOperationException]::new($Message) }
}
function Assert-Rejected([scriptblock] $Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Assert-True $rejected 'An unsafe or invalid release operation was accepted.'
}
function Invoke-Check([string] $Name, [scriptblock] $Action) {
    try { & $Action; $script:passed++; Write-Output ('PASS: ' + $Name) }
    catch { $script:failed++; Write-Output ('FAIL: ' + $Name + ' [' + $_.Exception.GetType().Name + '] ' + $_.Exception.Message) }
}

foreach ($case in @(
    @{ Run = 1L; Expected = '0.1.1.0' },
    @{ Run = 2L; Expected = '0.1.1.1' },
    @{ Run = 65535L; Expected = '0.1.1.65534' }
)) {
    Invoke-Check ('Run number maps to a unique four-part version: ' + $case.Run) {
        $actual = Get-Frp1CiVersion -BaseVersion '0.1.1.0' -RunNumber $case.Run
        Assert-True ($actual -ceq $case.Expected) 'The run version does not follow the documented monotonic mapping.'
    }
}
foreach ($run in @(0L, -1L, 65536L, [long]::MaxValue)) {
    Invoke-Check ('Invalid run number is rejected: ' + $run) {
        Assert-Rejected { Get-Frp1CiVersion -BaseVersion '0.1.1.0' -RunNumber $run }
    }
}
foreach ($base in @('', '0.1.1', 'latest', '0.1.1.0-preview', '0.1.1.5', '65535.1.1.0')) {
    Invoke-Check ('Invalid base version is rejected: ' + $(if ($base.Length) { $base } else { '<empty>' })) {
        Assert-Rejected { Get-Frp1CiVersion -BaseVersion $base -RunNumber 1 }
    }
}

Invoke-Check 'Current source and a missing installer index permit initial publication' {
    $decision = Get-Frp1PublishDecision -SourceSha $source -CurrentHeadSha $source -CandidateVersion '0.1.1.0' -CurrentIndexVersion $null
    Assert-True ($decision -ceq 'Publish') 'Initial publication was not allowed for the current source.'
}
Invoke-Check 'Existing 0.1.0.0 is preserved as history while newer 0.1.1.0 can publish' {
    $decision = Get-Frp1PublishDecision -SourceSha $source -CurrentHeadSha $source -CandidateVersion '0.1.1.0' -CurrentIndexVersion '0.1.0.0'
    Assert-True ($decision -ceq 'Publish') 'The newer release did not advance the old installer index.'
}
Invoke-Check 'Stale master source never changes publication state' {
    $decision = Get-Frp1PublishDecision -SourceSha $source -CurrentHeadSha $otherSource -CandidateVersion '0.1.1.3' -CurrentIndexVersion '0.1.1.0'
    Assert-True ($decision -ceq 'SkipStale') 'A stale source revision could publish over current master.'
}
Invoke-Check 'Stale source is rejected even when its candidate version is newer' {
    $decision = Get-Frp1PublishDecision -SourceSha $source -CurrentHeadSha $otherSource -CandidateVersion '0.1.1.65534' -CurrentIndexVersion $null
    Assert-True ($decision -ceq 'SkipStale') 'A high version incorrectly overrode source freshness.'
}
Invoke-Check 'Newer installer index never rolls backward to an older run' {
    $decision = Get-Frp1PublishDecision -SourceSha $source -CurrentHeadSha $source -CandidateVersion '0.1.1.2' -CurrentIndexVersion '0.1.1.10'
    Assert-True ($decision -ceq 'SkipNewer') 'Installer versions were compared lexically or rolled backward.'
}
Invoke-Check 'Version comparison is numeric when revision has multiple digits' {
    $decision = Get-Frp1PublishDecision -SourceSha $source -CurrentHeadSha $source -CandidateVersion '0.1.1.10' -CurrentIndexVersion '0.1.1.2'
    Assert-True ($decision -ceq 'Publish') 'Numeric revision ordering is incorrect.'
}
Invoke-Check 'Equal index version requires verification rather than blind overwrite' {
    $decision = Get-Frp1PublishDecision -SourceSha $source -CurrentHeadSha $source -CandidateVersion '0.1.1.3' -CurrentIndexVersion '0.1.1.3'
    Assert-True ($decision -ceq 'VerifyExisting') 'An already indexed version was blindly overwritten.'
}
foreach ($invalidSha in @('', 'abc1234', ('g' * 40), ('a' * 39))) {
    Invoke-Check ('Invalid source hash is rejected, length ' + $invalidSha.Length) {
        Assert-Rejected { Get-Frp1PublishDecision -SourceSha $invalidSha -CurrentHeadSha $source -CandidateVersion '0.1.1.0' -CurrentIndexVersion $null }
    }
}

Invoke-Check 'Existing release tag must resolve to the exact expected source' {
    Assert-Frp1ReleaseTagSource -ExpectedSha $source -ActualSha $source
}
Invoke-Check 'A tag owned by another source is never reused or moved' {
    Assert-Rejected { Assert-Frp1ReleaseTagSource -ExpectedSha $source -ActualSha $otherSource }
}
Invoke-Check 'A missing asset may be uploaded explicitly' {
    $decision = Get-Frp1AssetDecision -ExpectedSha256 $hash -ActualSha256 $null -Exists $false
    Assert-True ($decision -ceq 'UploadMissing') 'A missing asset was not identified as missing.'
}
Invoke-Check 'An exact existing asset is reused without replacement' {
    $decision = Get-Frp1AssetDecision -ExpectedSha256 $hash -ActualSha256 $hash.ToLowerInvariant() -Exists $true
    Assert-True ($decision -ceq 'Reuse') 'An exact existing asset was not reused.'
}
Invoke-Check 'An asset hash mismatch is fatal instead of replacing release history' {
    Assert-Rejected { Get-Frp1AssetDecision -ExpectedSha256 $hash -ActualSha256 $otherHash -Exists $true }
}
Invoke-Check 'An existing asset without a verified hash cannot be reused' {
    Assert-Rejected { Get-Frp1AssetDecision -ExpectedSha256 $hash -ActualSha256 $null -Exists $true }
}
Invoke-Check 'An invalid expected asset hash cannot authorize a new upload' {
    Assert-Rejected { Get-Frp1AssetDecision -ExpectedSha256 'invalid' -ActualSha256 $null -Exists $false }
}
Invoke-Check 'Complete matching release reuses assets only after source and version verification' {
    $decision = Get-Frp1PublishDecision -SourceSha $source -CurrentHeadSha $source -CandidateVersion '0.1.1.3' -CurrentIndexVersion '0.1.1.3'
    Assert-True ($decision -ceq 'VerifyExisting') 'Equal release did not require verification.'
    Assert-Frp1ReleaseTagSource -ExpectedSha $source -ActualSha $source
    foreach ($assetHash in @($hash, $otherHash)) {
        Assert-True ((Get-Frp1AssetDecision -ExpectedSha256 $assetHash -ActualSha256 $assetHash -Exists $true) -ceq 'Reuse') 'Complete matching assets were not left untouched.'
    }
}
Invoke-Check 'An incomplete draft can add only demonstrably missing assets' {
    $zipDecision = Get-Frp1AssetDecision -ExpectedSha256 $hash -ActualSha256 $hash -Exists $true
    $sumsDecision = Get-Frp1AssetDecision -ExpectedSha256 $otherHash -ActualSha256 $null -Exists $false
    Assert-True ($zipDecision -ceq 'Reuse' -and $sumsDecision -ceq 'UploadMissing') 'Draft recovery would replace an existing asset.'
    Assert-Rejected { Get-Frp1AssetDecision -ExpectedSha256 $hash -ActualSha256 $otherHash -Exists $true }
}

Write-Output ('Release policy checks: ' + $script:passed + ' passed, ' + $script:failed + ' failed.')
if ($script:failed -ne 0) { throw ('Offline release policy verification failed: ' + $script:failed + ' checks.') }
