Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Frp1CiVersion([string]$Version) {
    if ($Version -notmatch '^(0|[1-9]\d{0,4})\.(0|[1-9]\d{0,4})\.(0|[1-9]\d{0,4})\.(0|[1-9]\d{0,4})$' -or
        @($Version.Split('.') | Where-Object { [int]$_ -gt 65534 }).Count) { throw 'Invalid canonical four-component version.' }
}

function Get-Frp1CiVersion {
    param([Parameter(Mandatory)][string]$BaseVersion, [Parameter(Mandatory)][long]$RunNumber)
    Assert-Frp1CiVersion $BaseVersion
    if ($RunNumber -lt 1 -or $RunNumber -gt 65535) { throw 'Workflow run number exceeds the assembly revision range; advance the version base and workflow series.' }
    $parts = $BaseVersion.Split('.')
    if ($parts[3] -ne '0') { throw 'The release version base must have revision zero.' }
    return "$($parts[0]).$($parts[1]).$($parts[2]).$($RunNumber - 1)"
}

function Assert-Frp1ReleaseTagSource {
    param([Parameter(Mandatory)][string]$ExpectedSha, [Parameter(Mandatory)][string]$ActualSha)
    if ($ExpectedSha -cnotmatch '^[a-f0-9]{40}$' -or $ActualSha -cnotmatch '^[a-f0-9]{40}$' -or $ExpectedSha -cne $ActualSha) {
        throw 'Release tag points to a different or invalid source commit.'
    }
}

function Get-Frp1PublishDecision {
    param([Parameter(Mandatory)][string]$SourceSha, [Parameter(Mandatory)][string]$CurrentHeadSha,
        [Parameter(Mandatory)][string]$CandidateVersion, [AllowNull()][string]$CurrentIndexVersion)
    Assert-Frp1CiVersion $CandidateVersion
    if ($SourceSha -cnotmatch '^[a-f0-9]{40}$' -or $CurrentHeadSha -cnotmatch '^[a-f0-9]{40}$') { throw 'Invalid source commit.' }
    if ($CurrentIndexVersion) {
        Assert-Frp1CiVersion $CurrentIndexVersion
        if ([version]$CurrentIndexVersion -gt [version]$CandidateVersion) { return 'SkipNewer' }
    }
    if ($SourceSha -cne $CurrentHeadSha) { return 'SkipStale' }
    if ($CurrentIndexVersion -and [version]$CurrentIndexVersion -eq [version]$CandidateVersion) { return 'VerifyExisting' }
    return 'Publish'
}

function Get-Frp1AssetDecision {
    param([Parameter(Mandatory)][string]$ExpectedSha256, [AllowNull()][string]$ActualSha256, [bool]$Exists)
    if ($ExpectedSha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid expected asset digest.' }
    if (-not $Exists) { return 'UploadMissing' }
    if ($ActualSha256 -notmatch '^[a-fA-F0-9]{64}$' -or $ExpectedSha256 -ine $ActualSha256) { throw 'Existing release asset differs; refusing replacement.' }
    return 'Reuse'
}

Export-ModuleMember -Function Get-Frp1CiVersion,Get-Frp1PublishDecision,Assert-Frp1ReleaseTagSource,Get-Frp1AssetDecision
