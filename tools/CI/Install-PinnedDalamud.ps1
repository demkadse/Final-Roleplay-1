#requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Destination)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'DalamudPin.psm1') -Force
$uri = 'https://raw.githubusercontent.com/goatcorp/dalamud-distrib/38d5859b17b014bfdbfbbd5d60d9433333ce330b/latest.zip'
$expectedHash = 'E94D993BF91C8A43CE0959942602EBD18C831B788A133700150CEB928283129F'
$destinationPath = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $destinationPath) { throw 'Pinned SDK destination must be new.' }
$download = [IO.Path]::GetTempFileName()
try {
    Invoke-WebRequest -Uri $uri -OutFile $download -TimeoutSec 180
    Assert-Frp1PinnedDownload $download
    $archive = [IO.Compression.ZipFile]::OpenRead($download)
    try {
        if ($archive.Entries.Count -ne 100) { throw 'Pinned SDK entry count changed.' }
        $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $validated = @()
        [long]$total = 0
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('\', '/')
            $clean = $name.TrimEnd('/')
            $target = ConvertTo-Frp1SdkEntryPath -EntryName $name -Destination $destinationPath
            if ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000 -or -not $paths.Add($clean)) { throw 'Duplicate or unsafe SDK entry.' }
            $total += $entry.Length
            if ($entry.Length -gt 128MB -or $total -gt 512MB) { throw 'Pinned SDK archive exceeded its extraction limit.' }
            $validated += @{ Entry = $entry; Target = $target; Directory = $name.EndsWith('/') }
        }
        [void][IO.Directory]::CreateDirectory($destinationPath)
        foreach ($item in $validated) {
            if ($item.Directory) { [void][IO.Directory]::CreateDirectory($item.Target); continue }
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($item.Target))
            [IO.Compression.ZipFileExtensions]::ExtractToFile($item.Entry, $item.Target, $false)
        }
    }
    finally { $archive.Dispose() }
    Assert-Frp1SdkMetadata -AssemblyVersion ([Reflection.AssemblyName]::GetAssemblyName((Join-Path $destinationPath 'Dalamud.dll')).Version.ToString()) `
        -CommitHash ([IO.File]::ReadAllText((Join-Path $destinationPath 'commit_hash.txt')))
    $env:DALAMUD_HOME = $destinationPath
    if ($env:GITHUB_ENV) { [IO.File]::AppendAllText($env:GITHUB_ENV, "DALAMUD_HOME=$destinationPath`n") }
    [pscustomobject]@{ Directory = $destinationPath; Sha256 = $expectedHash; AssemblyVersion = '15.0.4.0'; Entries = 100 }
}
finally { [IO.File]::Delete($download) }
