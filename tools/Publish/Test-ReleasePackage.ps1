#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$RepoJsonPath,
    [Parameter(Mandatory)][ValidateRange(1577836800,4102444800)][long]$LastUpdateUnix,
    [string]$Version = '0.1.0.0',
    [string]$Tag = 'v0.1.0.0',
    [string]$Repository = 'demkadse/Final-Roleplay-1',
    [string]$NuGetPackageRoot
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleasePackage.psm1') -Force
Test-Frp1ReleasePackage @PSBoundParameters
