#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceSha,
    [Parameter(Mandatory)][long]$RunNumber,
    [Parameter(Mandatory)][ValidatePattern('^[1-9][0-9]{0,19}$')][string]$RunId,
    [Parameter(Mandatory)][ValidateRange(1,100000)][int]$RunAttempt,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$InputZip,
    [string]$NuGetPackageRoot
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleasePolicy.psm1') -Force
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$head = (& git -C $root rev-parse HEAD).Trim()
Assert-Frp1ReleaseTagSource $SourceSha $head
[xml]$project = [IO.File]::ReadAllText((Join-Path $root 'src/FRP1.Plugin/FRP1.Plugin.csproj'))
$base = $project.Project.PropertyGroup.Frp1Version.InnerText
$version = Get-Frp1CiVersion -BaseVersion $base -RunNumber $RunNumber
$timestampText = & git -C $root show -s --format=%ct $SourceSha
if ($LASTEXITCODE -ne 0 -or $timestampText -notmatch '^\d{10}$') { throw 'Cannot read source commit timestamp.' }
$timestamp = [long]$timestampText
$parameters = @{ LastUpdateUnix = $timestamp; Version = $version; Tag = "v$version"; OutputDirectory = $OutputDirectory }
if ($InputZip) { $parameters.InputZip = $InputZip }
if ($NuGetPackageRoot) { $parameters.NuGetPackageRoot = $NuGetPackageRoot }
$package = & (Join-Path $root 'tools/Publish/New-ReleasePackage.ps1') @parameters
$output = [IO.Path]::GetFullPath($OutputDirectory)
$metadata = [ordered]@{
    SchemaVersion = 1; Repository = 'demkadse/Final-Roleplay-1'; Branch = 'master'; SourceSha = $SourceSha
    RunId = $RunId; RunAttempt = $RunAttempt; Version = $version; Tag = "v$version"; LastUpdateUnix = $timestamp
    ZipSha256 = $package.Sha256; RepoSha256 = (Get-FileHash -LiteralPath (Join-Path $output 'repo.json')).Hash
    SumsSha256 = (Get-FileHash -LiteralPath (Join-Path $output 'SHA256SUMS')).Hash
}
[IO.File]::WriteAllText((Join-Path $output 'build-metadata.json'), ($metadata | ConvertTo-Json) + "`n", [Text.UTF8Encoding]::new($false))
$artifactName = "frp1-release-$RunId-$RunAttempt"
if ($env:GITHUB_OUTPUT) {
    [IO.File]::AppendAllText($env:GITHUB_OUTPUT, "version=$version`ntag=v$version`nlast_update=$timestamp`nartifact_name=$artifactName`n")
}
[pscustomobject]@{ Version = $version; Tag = "v$version"; LastUpdateUnix = $timestamp; ArtifactName = $artifactName; Package = $package }
