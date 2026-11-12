#requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$CandidateDirectory, [Parameter(Mandatory)][string]$SourceSha,
    [Parameter(Mandatory)][string]$Version, [Parameter(Mandatory)][string]$RunId,
    [Parameter(Mandatory)][int]$RunAttempt)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# This entry point is exclusively for the protected, master-only publish job.
if ($env:GITHUB_ACTIONS -cne 'true' -or $env:GITHUB_REPOSITORY -cne 'demkadse/Final-Roleplay-1' -or
    $env:GITHUB_REF -cne 'refs/heads/master' -or $env:GITHUB_SHA -cne $SourceSha -or -not $env:GITHUB_TOKEN) {
    throw 'Publishing requires the expected master workflow context and its job token.'
}
$candidate = & (Join-Path $PSScriptRoot 'Test-CiCandidate.ps1') @PSBoundParameters
Import-Module (Join-Path $PSScriptRoot 'PublishFlow.psm1') -Force
$repository = 'demkadse/Final-Roleplay-1'
$apiRoot = "https://api.github.com/repos/$repository"
$authenticated = [Net.Http.HttpClient]::new()
$anonymous = [Net.Http.HttpClient]::new()
foreach ($client in @($authenticated, $anonymous)) {
    $client.DefaultRequestHeaders.UserAgent.ParseAdd('FRP1-Release/1.0')
    $client.Timeout = [TimeSpan]::FromSeconds(120)
}
$authenticated.DefaultRequestHeaders.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $env:GITHUB_TOKEN)
$authenticated.DefaultRequestHeaders.Add('X-GitHub-Api-Version', '2022-11-28')

function Send-ReleaseRequest([string]$Method, [string]$Uri, $Body = $null, [string]$File = '', [switch]$Public,
    [switch]$Binary, [switch]$AllowMissing) {
    $client = if ($Public) { $anonymous } else { $authenticated }
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Method), $Uri)
    $response = $null
    try {
        $request.Headers.Accept.ParseAdd($(if ($Binary) { 'application/octet-stream' } else { 'application/vnd.github+json' }))
        if ($File) {
            $request.Content = [Net.Http.ByteArrayContent]::new([IO.File]::ReadAllBytes($File))
            $request.Content.Headers.ContentType = [Net.Http.Headers.MediaTypeHeaderValue]::new('application/octet-stream')
        }
        elseif ($null -ne $Body) { $request.Content = [Net.Http.StringContent]::new(($Body | ConvertTo-Json -Depth 12 -Compress), [Text.Encoding]::UTF8, 'application/json') }
        $response = $client.SendAsync($request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        if ($AllowMissing -and [int]$response.StatusCode -eq 404) { return $null }
        if (-not $response.IsSuccessStatusCode) { throw "GitHub request failed (HTTP $([int]$response.StatusCode)); remote state was not overwritten." }
        $stream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $memory = [IO.MemoryStream]::new()
        try {
            $buffer = [byte[]]::new(81920)
            while (($read = $stream.Read($buffer,0,$buffer.Length)) -ne 0) {
                if ($memory.Length + $read -gt 70MB) { throw 'Remote response exceeded the bounded download limit.' }
                $memory.Write($buffer,0,$read)
            }
            $bytes = $memory.ToArray()
        }
        finally { $memory.Dispose(); $stream.Dispose() }
        if ($Binary) { return ,$bytes }
        if ($bytes.Length -eq 0) { return $null }
        return ConvertFrom-Json -InputObject ([Text.Encoding]::UTF8.GetString($bytes)) -AsHashtable -Depth 32
    }
    finally { if ($response) { $response.Dispose() }; $request.Dispose() }
}

function Get-ReleaseTag([string]$Tag) {
    $reference = Send-ReleaseRequest GET "$apiRoot/git/ref/tags/$Tag" -AllowMissing
    if (-not $reference) { return $null }
    for ($depth = 0; $depth -lt 5; $depth++) {
        if ($reference.object.type -ceq 'commit') { return $reference.object.sha }
        if ($reference.object.type -cne 'tag') { throw 'Unsupported release tag target.' }
        $tagObject = Send-ReleaseRequest GET "$apiRoot/git/tags/$($reference.object.sha)"
        $reference = @{ object = $tagObject.object }
    }
    throw 'Nested release tag depth exceeded.'
}

function ConvertTo-ReleaseState($Value) {
    if (-not $Value) { return $null }
    return @{ Id = $Value.id; Draft = $Value.draft; SourceSha = $Value.target_commitish; Body = $Value.body
        Assets = @($Value.assets | ForEach-Object { @{ Name = $_.name; Id = $_.id; State = $_.state } }) }
}

$transport = {
    param($operation, $arguments)
    switch ($operation) {
        GetHead { return (Send-ReleaseRequest GET "$apiRoot/git/ref/heads/master").object.sha }
        GetIndex {
            $file = Send-ReleaseRequest GET "$apiRoot/contents/repo.json?ref=$($arguments.Head)" -AllowMissing
            if (-not $file) { return $null }
            if ($file.type -cne 'file' -or $file.encoding -cne 'base64' -or $file.size -gt 64KB) { throw 'Invalid remote installer file.' }
            $bytes = [Convert]::FromBase64String($file.content)
            $entries = ConvertFrom-Json -InputObject ([Text.Encoding]::UTF8.GetString($bytes)) -NoEnumerate
            if ($entries -isnot [array] -or $entries.Count -ne 1 -or $entries[0].InternalName -cne 'FRP1.Plugin') { throw 'Invalid remote installer index.' }
            return @{ Version = $entries[0].AssemblyVersion; Bytes = $bytes }
        }
        GetVersionFloor {
            $refs = @(Send-ReleaseRequest GET "$apiRoot/git/matching-refs/tags/v")
            if ($refs.Count -gt 10000) { throw 'Too many release tags.' }
            $versions = @($refs | ForEach-Object { if ($_.ref -cmatch '^refs/tags/v((?:0|[1-9]\d{0,4})\.(?:0|[1-9]\d{0,4})\.(?:0|[1-9]\d{0,4})\.(?:0|[1-9]\d{0,4}))$') { [version]$Matches[1] } })
            if ($versions.Count) { return ($versions | Sort-Object -Descending | Select-Object -First 1).ToString() }
            return $null
        }
        GetTag { return Get-ReleaseTag $arguments.Tag }
        GetRelease {
            $release = Send-ReleaseRequest GET "$apiRoot/releases/tags/$($arguments.Tag)" -AllowMissing
            if ($release) { return ConvertTo-ReleaseState $release }
            # A draft may not resolve via the published-release-by-tag endpoint.
            for ($page = 1; $page -le 20; $page++) {
                $batch = @(Send-ReleaseRequest GET "$apiRoot/releases?per_page=100&page=$page")
                $matches = @($batch | Where-Object tag_name -CEQ $arguments.Tag)
                if ($matches.Count -gt 1) { throw 'Duplicate releases for this tag.' }
                if ($matches.Count -eq 1) { return ConvertTo-ReleaseState $matches[0] }
                if ($batch.Count -lt 100) { return $null }
            }
            throw 'Release listing exceeded its bounded lookup limit.'
        }
        CreateDraft {
            return ConvertTo-ReleaseState (Send-ReleaseRequest POST "$apiRoot/releases" @{
                tag_name = $arguments.Tag; target_commitish = $arguments.SourceSha; name = "Final Roleplay 1 $($arguments.Tag)"
                body = $arguments.Body; draft = $true; prerelease = $true; make_latest = 'false'
            })
        }
        UploadAsset {
            return Send-ReleaseRequest POST "https://uploads.github.com/repos/$repository/releases/$($arguments.ReleaseId)/assets?name=$([Uri]::EscapeDataString($arguments.Name))" -File $arguments.Path
        }
        DownloadAsset { return ,(Send-ReleaseRequest GET "$apiRoot/releases/assets/$($arguments.AssetId)" -Binary) }
        PublishRelease { return Send-ReleaseRequest PATCH "$apiRoot/releases/$($arguments.ReleaseId)" @{ draft = $false; prerelease = $true; make_latest = 'false' } }
        DownloadPublic {
            $uri = "https://github.com/$repository/releases/download/$($arguments.Tag)/$($arguments.Name)"
            for ($attempt = 0; $attempt -lt 6; $attempt++) {
                try { return ,(Send-ReleaseRequest GET $uri -Public -Binary) }
                catch { if ($attempt -eq 5) { throw }; Start-Sleep -Seconds 2 }
            }
        }
        CommitIndex {
            $commit = Send-ReleaseRequest GET "$apiRoot/git/commits/$($arguments.ExpectedSha)"
            $blob = Send-ReleaseRequest POST "$apiRoot/git/blobs" @{ content = [Convert]::ToBase64String($arguments.Bytes); encoding = 'base64' }
            $tree = Send-ReleaseRequest POST "$apiRoot/git/trees" @{ base_tree = $commit.tree.sha; tree = @(@{ path = 'repo.json'; mode = '100644'; type = 'blob'; sha = $blob.sha }) }
            $newCommit = Send-ReleaseRequest POST "$apiRoot/git/commits" @{ message = "Update installer to $($arguments.Version) [skip ci]"; tree = $tree.sha; parents = @($arguments.ExpectedSha) }
            # A concurrent user commit makes this non-fast-forward and GitHub rejects it.
            try { return Send-ReleaseRequest PATCH "$apiRoot/git/refs/heads/master" @{ sha = $newCommit.sha; force = $false } }
            catch {
                if ((Send-ReleaseRequest GET "$apiRoot/git/ref/heads/master").object.sha -cne $arguments.ExpectedSha) { return 'SkipStale' }
                throw
            }
        }
        default { throw 'Unknown publication operation.' }
    }
}
try {
    Invoke-Frp1Publication -Metadata $candidate.Metadata -CandidateDirectory $CandidateDirectory -Transport $transport
}
finally { $authenticated.Dispose(); $anonymous.Dispose() }
