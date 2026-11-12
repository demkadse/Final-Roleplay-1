#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateRange(1577836800,4102444800)][long]$LastUpdateUnix,
    [string]$InputZip,
    [string]$OutputDirectory,
    [string]$NuGetPackageRoot,
    [string]$Version,
    [string]$Tag,
    [string]$Repository = 'demkadse/Final-Roleplay-1'
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleasePackage.psm1') -Force
New-Frp1ReleasePackage @PSBoundParameters
