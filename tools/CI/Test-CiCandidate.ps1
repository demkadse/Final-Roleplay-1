#requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$CandidateDirectory, [Parameter(Mandatory)][string]$SourceSha,
    [Parameter(Mandatory)][string]$Version, [Parameter(Mandatory)][string]$RunId,
    [Parameter(Mandatory)][int]$RunAttempt)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleasePolicy.psm1') -Force
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Assert-Frp1ReleaseTagSource $SourceSha ((& git -C $root rev-parse HEAD).Trim())
if ($RunId -notmatch '^[1-9][0-9]{0,19}$' -or $RunAttempt -lt 1) { throw 'Invalid workflow identity.' }
$directory = Get-Item -LiteralPath $CandidateDirectory
if (-not $directory.PSIsContainer -or $directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Invalid candidate directory.' }
$expected = @("FRP1-$Version.zip", 'repo.json', 'SHA256SUMS', 'build-metadata.json')
$files = @(Get-ChildItem -LiteralPath $directory.FullName -Force)
if ($files.Count -ne 4 -or @($files | Where-Object { $_.PSIsContainer -or $_.Attributes -band [IO.FileAttributes]::ReparsePoint -or $_.Name -cnotin $expected }).Count) {
    throw 'Unexpected candidate files.'
}
$metadataPath = Join-Path $directory.FullName 'build-metadata.json'
if ((Get-Item -LiteralPath $metadataPath).Length -gt 16KB) { throw 'Oversized build metadata.' }
$metadataText = [IO.File]::ReadAllText($metadataPath)
$document = [Text.Json.JsonDocument]::Parse($metadataText)
try {
    $keys = @('SchemaVersion','Repository','Branch','SourceSha','RunId','RunAttempt','Version','Tag','LastUpdateUnix','ZipSha256','RepoSha256','SumsSha256')
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    if ($document.RootElement.ValueKind -ne [Text.Json.JsonValueKind]::Object) { throw 'Invalid metadata object.' }
    foreach ($property in $document.RootElement.EnumerateObject()) {
        if ($property.Name -cnotin $keys -or -not $seen.Add($property.Name)) { throw 'Unknown or duplicate metadata field.' }
    }
    if ($seen.Count -ne $keys.Count) { throw 'Missing build metadata.' }
}
finally { $document.Dispose() }
$metadata = ConvertFrom-Json -InputObject $metadataText -AsHashtable
$timestamp = & git -C $root show -s --format=%ct $SourceSha
if ($LASTEXITCODE -ne 0 -or $timestamp -notmatch '^\d{10}$' -or
    $metadata.SchemaVersion -ne 1 -or $metadata.Repository -cne 'demkadse/Final-Roleplay-1' -or $metadata.Branch -cne 'master' -or
    $metadata.SourceSha -cne $SourceSha -or $metadata.Version -cne $Version -or $metadata.Tag -cne "v$Version" -or
    $metadata.RunId -cne $RunId -or $metadata.RunAttempt -ne $RunAttempt -or $metadata.LastUpdateUnix -ne [long]$timestamp) {
    throw 'Candidate does not belong to the expected source, version or workflow run.'
}
$sums = ''
foreach ($item in @(@("FRP1-$Version.zip", 'ZipSha256'), @('repo.json', 'RepoSha256'), @('SHA256SUMS', 'SumsSha256'))) {
    if ($metadata[$item[1]] -cnotmatch '^[A-F0-9]{64}$' -or (Get-FileHash -LiteralPath (Join-Path $directory.FullName $item[0])).Hash -cne $metadata[$item[1]]) {
        throw 'Candidate digest mismatch.'
    }
    if ($item[0] -cne 'SHA256SUMS') { $sums += $metadata[$item[1]] + '  ' + $item[0] + "`n" }
}
if ([IO.File]::ReadAllText((Join-Path $directory.FullName 'SHA256SUMS')) -cne $sums) { throw 'Invalid checksum file.' }
$validation = & (Join-Path $root 'tools/Publish/Test-ReleasePackage.ps1') -PackagePath (Join-Path $directory.FullName "FRP1-$Version.zip") `
    -RepoJsonPath (Join-Path $directory.FullName 'repo.json') -LastUpdateUnix $metadata.LastUpdateUnix -Version $Version -Tag $metadata.Tag
[pscustomobject]@{ Metadata = $metadata; Validation = $validation }
