#requires -Version 7.2
[CmdletBinding()]
param([string] $FlowModule = (Join-Path $PSScriptRoot '..\..\tools\CI\PublishFlow.psm1'))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module $FlowModule -Force
$script:passed = 0
$script:failed = 0
$workRoot = Join-Path $PSScriptRoot ('bin\publication-flow-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($workRoot)

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw [InvalidOperationException]::new($Message) }
}
function Assert-Rejected([scriptblock] $Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Assert-True $rejected 'The unsafe publication flow was accepted.'
}
function Invoke-Check([string] $Name, [scriptblock] $Action) {
    try { & $Action; $script:passed++; Write-Output ('PASS: ' + $Name) }
    catch { $script:failed++; Write-Output ('FAIL: ' + $Name + ' [' + $_.Exception.GetType().Name + '] ' + $_.Exception.Message) }
}
function Get-Digest([byte[]] $Bytes) { return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)) }
function New-Scenario([string] $Name) {
    $directory = Join-Path $workRoot $Name
    [void][IO.Directory]::CreateDirectory($directory)
    $version = '0.1.1.3'
    $zipName = "FRP1-$version.zip"
    $repoBytes = [Text.Encoding]::UTF8.GetBytes('[{"InternalName":"FRP1.Plugin","AssemblyVersion":"0.1.1.3"}]')
    $zipBytes = [Text.Encoding]::UTF8.GetBytes('Synthetic offline ZIP fixture, not a published package')
    $sumsBytes = [Text.Encoding]::UTF8.GetBytes((Get-Digest $zipBytes) + "  $zipName`n" + (Get-Digest $repoBytes) + "  repo.json`n")
    [IO.File]::WriteAllBytes((Join-Path $directory 'repo.json'), $repoBytes)
    [IO.File]::WriteAllBytes((Join-Path $directory $zipName), $zipBytes)
    [IO.File]::WriteAllBytes((Join-Path $directory 'SHA256SUMS'), $sumsBytes)
    $metadata = @{ SourceSha = ('a' * 40); Version = $version; Tag = "v$version"; RunId = '12345'; LastUpdateUnix = 1794427356L
        ZipSha256 = (Get-Digest $zipBytes); RepoSha256 = (Get-Digest $repoBytes); SumsSha256 = (Get-Digest $sumsBytes) }
    $state = @{
        Metadata = $metadata; Directory = $directory; RepoBytes = $repoBytes
        Heads = @($metadata.SourceSha); HeadCalls = 0; CurrentHead = $metadata.SourceSha
        Index = $null; Floor = $null; TagSource = $null; Release = $null
        Calls = [Collections.Generic.List[object]]::new(); NextAssetId = 100
        AssetBytes = @{}; ExpectedBytes = @{ $zipName = $zipBytes; 'SHA256SUMS' = $sumsBytes }
        CorruptPublic = $null; FailPublic = $null; CommitResult = $null; Commits = [Collections.Generic.List[object]]::new()
    }
    $transport = {
        param([string] $Operation, [hashtable] $Arguments)
        $captured = @{}
        foreach ($key in $Arguments.Keys) { $captured[$key] = $Arguments[$key] }
        $state.Calls.Add([pscustomobject]@{ Operation = $Operation; Arguments = $captured })
        switch ($Operation) {
            'GetHead' {
                $index = [Math]::Min($state.HeadCalls, $state.Heads.Count - 1)
                $state.HeadCalls++
                $state.CurrentHead = $state.Heads[$index]
                return $state.CurrentHead
            }
            'GetIndex' { return $state.Index }
            'GetVersionFloor' { return $state.Floor }
            'GetTag' { return $state.TagSource }
            'CreateDraft' {
                if ($null -ne $state.Release) { throw 'Fixture refuses replacing an existing release.' }
                $state.Release = @{ Id = 77; Draft = $true; SourceSha = $Arguments.SourceSha; Body = $Arguments.Body; Assets = @() }
                return [pscustomobject]$state.Release
            }
            'GetRelease' {
                if ($null -eq $state.Release) { return $null }
                return [pscustomobject]@{ Id = $state.Release.Id; Draft = $state.Release.Draft; SourceSha = $state.Release.SourceSha
                    Body = $state.Release.Body; Assets = @($state.Release.Assets | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Id = $_.Id; State = $_.State } }) }
            }
            'UploadAsset' {
                if (-not $state.Release.Draft -or @($state.Release.Assets | Where-Object Name -CEQ $Arguments.Name).Count) {
                    throw 'Fixture refuses replacing an existing or published asset.'
                }
                $state.NextAssetId++
                $id = [string]$state.NextAssetId
                $state.AssetBytes[$id] = [IO.File]::ReadAllBytes($Arguments.Path)
                $state.Release.Assets += @{ Name = $Arguments.Name; Id = $id; State = 'uploaded' }
                return $null
            }
            'DownloadAsset' { return ,([byte[]]$state.AssetBytes[[string]$Arguments.AssetId]) }
            'PublishRelease' {
                $state.Release.Draft = $false
                $state.TagSource = $state.Metadata.SourceSha
                return $null
            }
            'DownloadPublic' {
                if ($state.Release.Draft) { throw 'Fixture refuses downloading a non-public release.' }
                if ($state.FailPublic -eq $Arguments.Name) { throw 'Synthetic anonymous HTTP failure.' }
                if ($state.CorruptPublic -eq $Arguments.Name) { return ,([Text.Encoding]::UTF8.GetBytes('Synthetic corrupt public asset')) }
                return ,([byte[]]$state.ExpectedBytes[$Arguments.Name])
            }
            'CommitIndex' {
                if ($Arguments.ExpectedSha -cne $state.CurrentHead) { throw 'Fixture refuses overwriting a newer source head.' }
                if ($Arguments.ContainsKey('Force') -and $Arguments.Force) { throw 'Fixture refuses a forced ref update.' }
                if ($state.CommitResult -ceq 'SkipStale') { return 'SkipStale' }
                $state.Commits.Add($captured)
                return $null
            }
            default { throw ('Unexpected network operation in offline fixture: ' + $Operation) }
        }
    }.GetNewClosure()
    return [pscustomobject]@{ State = $state; Metadata = $metadata; Directory = $directory; Transport = $transport }
}
function Set-ExistingRelease($Scenario, [bool] $Draft, [string[]] $Names) {
    $state = $Scenario.State
    $state.TagSource = $Scenario.Metadata.SourceSha
    $body = "FRP1 automated release`nSource: $($Scenario.Metadata.SourceSha)`nRun: $($Scenario.Metadata.RunId)"
    $state.Release = @{ Id = 77; Draft = $Draft; SourceSha = $Scenario.Metadata.SourceSha; Body = $body; Assets = @() }
    foreach ($name in $Names) {
        $state.NextAssetId++
        $id = [string]$state.NextAssetId
        $state.Release.Assets += @{ Name = $name; Id = $id; State = 'uploaded' }
        $state.AssetBytes[$id] = $state.ExpectedBytes[$name]
    }
}
function Invoke-Scenario($Scenario) {
    Invoke-Frp1Publication -Metadata $Scenario.Metadata -CandidateDirectory $Scenario.Directory -Transport $Scenario.Transport
}
function Assert-NoWrites($Scenario) {
    $writes = @($Scenario.State.Calls | Where-Object Operation -In @('CreateDraft', 'UploadAsset', 'PublishRelease', 'CommitIndex'))
    Assert-True ($writes.Count -eq 0) 'A skip/no-op performed a publication write.'
}
function Assert-NoInstallerCommit($Scenario) {
    Assert-True ($Scenario.State.Commits.Count -eq 0) 'Installer was committed before all safety checks passed.'
}

try {
    Invoke-Check 'Fresh release verifies authenticated and anonymous bytes before committing installer' {
        $s = New-Scenario 'fresh'
        Assert-True ((Invoke-Scenario $s) -ceq 'Published') 'Fresh publication failed.'
        $operations = @($s.State.Calls.Operation)
        Assert-True (@($operations | Where-Object { $_ -eq 'UploadAsset' }).Count -eq 2) 'Fresh draft did not upload both missing assets.'
        $publicIndexes = @(for ($i = 0; $i -lt $operations.Count; $i++) { if ($operations[$i] -eq 'DownloadPublic') { $i } })
        $authenticatedIndexes = @(for ($i = 0; $i -lt $operations.Count; $i++) { if ($operations[$i] -eq 'DownloadAsset') { $i } })
        $publishIndex = [Array]::IndexOf($operations, 'PublishRelease')
        $commitIndex = [Array]::IndexOf($operations, 'CommitIndex')
        Assert-True ($authenticatedIndexes.Count -eq 2 -and ($authenticatedIndexes | Measure-Object -Maximum).Maximum -lt $publishIndex) 'Draft became public before authenticated asset verification.'
        Assert-True ($publicIndexes.Count -eq 2 -and ($publicIndexes | Measure-Object -Maximum).Maximum -lt $commitIndex) 'Installer precedes anonymous asset verification.'
        Assert-True ($s.State.Commits.Count -eq 1) 'Installer was not committed exactly once.'
        Assert-True ($s.State.Commits[0].ExpectedSha -ceq $s.Metadata.SourceSha) 'Installer update lacks the exact expected source parent.'
        Assert-True ((Get-Digest $s.State.Commits[0].Bytes) -ceq $s.Metadata.RepoSha256) 'Installer commit includes different bytes.'
    }
    Invoke-Check 'Stale master skips without creating or changing a release' {
        $s = New-Scenario 'stale'
        $s.State.Heads = @('b' * 40)
        Assert-True ((Invoke-Scenario $s) -ceq 'SkipStale') 'Stale source was not skipped.'
        Assert-NoWrites $s
    }
    Invoke-Check 'Newer installer index skips without publication writes' {
        $s = New-Scenario 'newer-index'
        $s.State.Index = @{ Version = '0.1.1.9'; Bytes = [Text.Encoding]::UTF8.GetBytes('Synthetic newer index') }
        Assert-True ((Invoke-Scenario $s) -ceq 'SkipNewer') 'Newer installer index was overwritten.'
        Assert-NoWrites $s
    }
    Invoke-Check 'Newer remote tag floor prevents rollback even with a missing index' {
        $s = New-Scenario 'newer-floor'
        $s.State.Floor = '0.1.1.9'
        Assert-True ((Invoke-Scenario $s) -ceq 'SkipNewer') 'Remote release version floor was ignored.'
        Assert-NoWrites $s
    }
    Invoke-Check 'Complete matching release and index are a verified no-op even after the index commit' {
        $s = New-Scenario 'complete-noop'
        Set-ExistingRelease $s $false @("FRP1-$($s.Metadata.Version).zip", 'SHA256SUMS')
        $s.State.Index = @{ Version = $s.Metadata.Version; Bytes = $s.State.RepoBytes }
        $s.State.Heads = @('b' * 40)
        Assert-True ((Invoke-Scenario $s) -ceq 'NoOp') 'A matching completed release was not reused.'
        Assert-True (@($s.State.Calls | Where-Object Operation -EQ 'DownloadPublic').Count -eq 2) 'No-op did not verify anonymous bytes.'
        Assert-NoWrites $s
    }
    Invoke-Check 'Same version with different installer bytes is rejected without writes' {
        $s = New-Scenario 'same-version-different-index'
        $s.State.Index = @{ Version = $s.Metadata.Version; Bytes = [Text.Encoding]::UTF8.GetBytes('Different index at same version') }
        Assert-Rejected { Invoke-Scenario $s }
        Assert-NoWrites $s
    }
    Invoke-Check 'Tag from another source is never moved or reused' {
        $s = New-Scenario 'foreign-tag'
        $s.State.TagSource = 'b' * 40
        Assert-Rejected { Invoke-Scenario $s }
        Assert-NoWrites $s
    }
    Invoke-Check 'Unowned draft is never modified' {
        $s = New-Scenario 'foreign-draft'
        Set-ExistingRelease $s $true @()
        $s.State.Release.Body = 'Synthetic draft owned by a different run'
        Assert-Rejected { Invoke-Scenario $s }
        Assert-NoWrites $s
    }
    Invoke-Check 'Owned incomplete draft uploads only the demonstrably missing asset' {
        $s = New-Scenario 'partial-draft'
        $zipName = "FRP1-$($s.Metadata.Version).zip"
        Set-ExistingRelease $s $true @($zipName)
        $oldId = $s.State.Release.Assets[0].Id
        Assert-True ((Invoke-Scenario $s) -ceq 'Published') 'Owned partial draft could not finish.'
        $uploads = @($s.State.Calls | Where-Object Operation -EQ 'UploadAsset')
        Assert-True ($uploads.Count -eq 1 -and $uploads[0].Arguments.Name -ceq 'SHA256SUMS') 'An already existing draft asset was uploaded again.'
        Assert-True ($s.State.Release.Assets[0].Id -ceq $oldId) 'Existing asset identity changed.'
    }
    Invoke-Check 'Published incomplete release is refused without uploading replacement assets' {
        $s = New-Scenario 'partial-public'
        Set-ExistingRelease $s $false @("FRP1-$($s.Metadata.Version).zip")
        Assert-Rejected { Invoke-Scenario $s }
        Assert-NoWrites $s
    }
    Invoke-Check 'Existing asset hash mismatch never reaches publication or installer commit' {
        $s = New-Scenario 'wrong-authenticated-hash'
        Set-ExistingRelease $s $true @("FRP1-$($s.Metadata.Version).zip", 'SHA256SUMS')
        $s.State.AssetBytes[[string]$s.State.Release.Assets[0].Id] = [Text.Encoding]::UTF8.GetBytes('Corrupt existing asset')
        Assert-Rejected { Invoke-Scenario $s }
        Assert-NoWrites $s
    }
    Invoke-Check 'Wrong anonymous asset bytes never enter the installer index' {
        $s = New-Scenario 'wrong-public-hash'
        $s.State.CorruptPublic = "FRP1-$($s.Metadata.Version).zip"
        Assert-Rejected { Invoke-Scenario $s }
        Assert-NoInstallerCommit $s
    }
    Invoke-Check 'Anonymous download failure never enters the installer index' {
        $s = New-Scenario 'public-unavailable'
        $s.State.FailPublic = 'SHA256SUMS'
        Assert-Rejected { Invoke-Scenario $s }
        Assert-NoInstallerCommit $s
    }
    Invoke-Check 'New master before publishing draft prevents public release and index changes' {
        $s = New-Scenario 'head-changed-before-publish'
        $s.State.Heads = @($s.Metadata.SourceSha, ('b' * 40))
        Assert-True ((Invoke-Scenario $s) -ceq 'SkipStale') 'Changed head did not stop draft publication.'
        Assert-True ($s.State.Release.Draft) 'The stale source release became public.'
        Assert-NoInstallerCommit $s
    }
    Invoke-Check 'New master after public verification is not overwritten by installer commit' {
        $s = New-Scenario 'head-changed-before-index'
        $s.State.Heads = @($s.Metadata.SourceSha, $s.Metadata.SourceSha, ('b' * 40))
        Assert-True ((Invoke-Scenario $s) -ceq 'SkipStale') 'Changed head did not stop index commit.'
        Assert-NoInstallerCommit $s
    }
    Invoke-Check 'Index cannot point to an unfinished draft on a rerun' {
        $s = New-Scenario 'indexed-draft'
        Set-ExistingRelease $s $true @("FRP1-$($s.Metadata.Version).zip", 'SHA256SUMS')
        $s.State.Index = @{ Version = $s.Metadata.Version; Bytes = $s.State.RepoBytes }
        Assert-Rejected { Invoke-Scenario $s }
        Assert-NoWrites $s
    }
    Invoke-Check 'Unexpected duplicate assets prevent modifying an existing draft' {
        $s = New-Scenario 'duplicate-assets'
        $zipName = "FRP1-$($s.Metadata.Version).zip"
        Set-ExistingRelease $s $true @($zipName, $zipName)
        Assert-Rejected { Invoke-Scenario $s }
        Assert-NoWrites $s
    }
    Invoke-Check 'Changed local installer is rejected before any transport call' {
        $s = New-Scenario 'changed-local-index'
        [IO.File]::WriteAllText((Join-Path $s.Directory 'repo.json'), 'Changed local index')
        Assert-Rejected { Invoke-Scenario $s }
        Assert-True ($s.State.Calls.Count -eq 0) 'An invalid local installer reached the transport.'
    }
    Invoke-Check 'Partial draft with a corrupt existing asset adds no missing asset' {
        $s = New-Scenario 'corrupt-partial-draft'
        Set-ExistingRelease $s $true @('SHA256SUMS')
        $s.State.AssetBytes[[string]$s.State.Release.Assets[0].Id] = [Text.Encoding]::UTF8.GetBytes('Changed existing checksum file')
        Assert-Rejected { Invoke-Scenario $s }
        Assert-NoWrites $s
    }
    Invoke-Check 'Unexpected assets prevent modifying an existing draft' {
        $s = New-Scenario 'unexpected-asset'
        Set-ExistingRelease $s $true @()
        $s.State.Release.Assets += @{ Name = 'unrecognized.txt'; Id = '999'; State = 'uploaded' }
        Assert-Rejected { Invoke-Scenario $s }
        Assert-NoWrites $s
    }
    Invoke-Check 'Incomplete uploaded asset is never overwritten' {
        $s = New-Scenario 'incomplete-asset'
        Set-ExistingRelease $s $true @('SHA256SUMS')
        $s.State.Release.Assets[0].State = 'starter'
        Assert-Rejected { Invoke-Scenario $s }
        Assert-NoWrites $s
    }
    Invoke-Check 'A matching draft marker cannot authorize a draft with another source' {
        $s = New-Scenario 'draft-wrong-source'
        Set-ExistingRelease $s $true @()
        $s.State.Release.SourceSha = 'b' * 40
        Assert-Rejected { Invoke-Scenario $s }
        Assert-NoWrites $s
    }
    Invoke-Check 'Changed local ZIP is rejected before any transport call' {
        $s = New-Scenario 'changed-local-zip'
        [IO.File]::WriteAllText((Join-Path $s.Directory "FRP1-$($s.Metadata.Version).zip"), 'Changed local ZIP')
        Assert-Rejected { Invoke-Scenario $s }
        Assert-True ($s.State.Calls.Count -eq 0) 'An invalid local ZIP reached the transport.'
    }
    Invoke-Check 'A final compare-and-swap conflict reports stale rather than publication success' {
        $s = New-Scenario 'index-cas-conflict'
        $s.State.CommitResult = 'SkipStale'
        Assert-True ((Invoke-Scenario $s) -ceq 'SkipStale') 'Final branch conflict was reported as successful publication.'
        Assert-NoInstallerCommit $s
        Assert-True (@($s.State.Calls | Where-Object Operation -EQ 'CommitIndex').Count -eq 1) 'Conflict did not exercise the final conditional commit.'
    }
}
finally {
    if (Test-Path -LiteralPath $workRoot) {
        $resolved = (Resolve-Path -LiteralPath $workRoot).Path
        $parent = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'bin')).TrimEnd('\') + '\'
        if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or
            -not [IO.Path]::GetFileName($resolved).StartsWith('publication-flow-', [StringComparison]::Ordinal)) { throw 'Unsafe test cleanup target.' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
Write-Output ('Publication flow checks: ' + $script:passed + ' passed, ' + $script:failed + ' failed.')
if ($script:failed -ne 0) { throw ('Offline publication flow verification failed: ' + $script:failed + ' checks.') }
