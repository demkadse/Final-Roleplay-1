#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateRange(1577836800,4102444800)][long]$LastUpdateUnix,
    [string]$InputZip,
    [string]$OutputDirectory,
    [string]$NuGetPackageRoot,
    [string]$Version = '0.1.0.0',
    [string]$Tag = 'v0.1.0.0',
    [string]$Repository = 'demkadse/FinalRoleplay1'
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleasePackage.psm1') -Force
New-Frp1ReleasePackage @PSBoundParameters
