Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleasePolicy.psm1') -Force

# Transport is the single I/O seam. Offline tests supply recorded GitHub responses.
function Invoke-Frp1Publication {
    [CmdletBinding()]
    param([Parameter(Mandatory)][hashtable]$Metadata, [Parameter(Mandatory)][string]$CandidateDirectory,
        [Parameter(Mandatory)][scriptblock]$Transport)
    $zipName = "FRP1-$($Metadata.Version).zip"
    $assetHashes = @{ $zipName = $Metadata.ZipSha256; 'SHA256SUMS' = $Metadata.SumsSha256 }
    $repoBytes = [IO.File]::ReadAllBytes((Join-Path $CandidateDirectory 'repo.json'))
    $hash = { param([byte[]]$Bytes) [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)) }
    if ((& $hash $repoBytes) -ine $Metadata.RepoSha256) { throw 'Installer candidate changed.' }
    foreach ($name in $assetHashes.Keys) {
        if ((& $hash ([IO.File]::ReadAllBytes((Join-Path $CandidateDirectory $name)))) -ine $assetHashes[$name]) { throw 'Local release asset changed.' }
    }
    $head = & $Transport 'GetHead' @{}
    $index = & $Transport 'GetIndex' @{ Head = $head }
    $sameIndex = $null -ne $index -and (& $hash $index.Bytes) -ieq $Metadata.RepoSha256
    $floor = & $Transport 'GetVersionFloor' @{}
    if ($index -and (-not $floor -or [version]$index.Version -gt [version]$floor)) { $floor = $index.Version }
    $decision = Get-Frp1PublishDecision -SourceSha $Metadata.SourceSha -CurrentHeadSha $head -CandidateVersion $Metadata.Version -CurrentIndexVersion $floor
    if ($decision -eq 'SkipNewer' -or ($decision -eq 'SkipStale' -and -not $sameIndex)) { return $decision }
    if ($index -and [version]$index.Version -eq [version]$Metadata.Version -and -not $sameIndex) {
        throw 'Existing installer has this version with different bytes.'
    }
    $tagSource = & $Transport 'GetTag' @{ Tag = $Metadata.Tag }
    if ($tagSource) { Assert-Frp1ReleaseTagSource $Metadata.SourceSha $tagSource }
    $release = & $Transport 'GetRelease' @{ Tag = $Metadata.Tag }
    $marker = "FRP1 automated release`nSource: $($Metadata.SourceSha)`nRun: $($Metadata.RunId)"
    if ($sameIndex -and (-not $release -or $release.Draft)) { throw 'Installer points to an unfinished release.' }
    if (-not $release) {
        $release = & $Transport 'CreateDraft' @{ Tag = $Metadata.Tag; SourceSha = $Metadata.SourceSha; Body = $marker }
    }
    if ($release.Draft -and ($release.SourceSha -cne $Metadata.SourceSha -or $release.Body -cne $marker)) {
        throw 'Refusing to modify a draft not owned by this source and workflow run.'
    }
    $assets = @($release.Assets)
    if (@($assets | Group-Object Name | Where-Object Count -gt 1).Count -or @($assets | Where-Object Name -CNotIn @($assetHashes.Keys)).Count) {
        throw 'Unexpected or duplicate release assets; refusing modification.'
    }
    $missing = @()
    foreach ($name in ($assetHashes.Keys | Sort-Object)) {
        $asset = @($assets | Where-Object Name -CEQ $name)
        if ($asset.Count -eq 0) {
            if (-not $release.Draft) { throw 'Published release is missing an expected asset.' }
            $missing += $name
        }
        elseif ($asset[0].State -cne 'uploaded') { throw 'Existing asset is incomplete; refusing replacement.' }
        else {
            [byte[]]$bytes = & $Transport 'DownloadAsset' @{ AssetId = $asset[0].Id }
            $null = Get-Frp1AssetDecision -ExpectedSha256 $assetHashes[$name] -ActualSha256 (& $hash $bytes) -Exists $true
        }
    }
    foreach ($name in $missing) {
        $null = & $Transport 'UploadAsset' @{ ReleaseId = $release.Id; Name = $name; Path = (Join-Path $CandidateDirectory $name) }
    }
    # Re-fetch IDs after a partial-draft upload, then verify authenticated bytes.
    $release = & $Transport 'GetRelease' @{ Tag = $Metadata.Tag }
    foreach ($name in ($assetHashes.Keys | Sort-Object)) {
        $asset = @($release.Assets | Where-Object Name -CEQ $name)
        if ($asset.Count -ne 1 -or $asset[0].State -cne 'uploaded') { throw 'Uploaded release asset is missing.' }
        [byte[]]$bytes = & $Transport 'DownloadAsset' @{ AssetId = $asset[0].Id }
        $null = Get-Frp1AssetDecision -ExpectedSha256 $assetHashes[$name] -ActualSha256 (& $hash $bytes) -Exists $true
    }
    if ($release.Draft) {
        if ((& $Transport 'GetHead' @{}) -cne $Metadata.SourceSha) { return 'SkipStale' }
        $null = & $Transport 'PublishRelease' @{ ReleaseId = $release.Id }
    }
    $tagSource = & $Transport 'GetTag' @{ Tag = $Metadata.Tag }
    Assert-Frp1ReleaseTagSource $Metadata.SourceSha $tagSource
    # Public URLs must work without the token before the installer can reference them.
    foreach ($name in ($assetHashes.Keys | Sort-Object)) {
        [byte[]]$bytes = & $Transport 'DownloadPublic' @{ Tag = $Metadata.Tag; Name = $name }
        $null = Get-Frp1AssetDecision -ExpectedSha256 $assetHashes[$name] -ActualSha256 (& $hash $bytes) -Exists $true
    }
    if ($sameIndex) { return 'NoOp' }
    if ((& $Transport 'GetHead' @{}) -cne $Metadata.SourceSha) { return 'SkipStale' }
    $commitResult = & $Transport 'CommitIndex' @{ ExpectedSha = $Metadata.SourceSha; Bytes = $repoBytes; Version = $Metadata.Version }
    if ($commitResult -ceq 'SkipStale') { return 'SkipStale' }
    return 'Published'
}

Export-ModuleMember -Function Invoke-Frp1Publication
