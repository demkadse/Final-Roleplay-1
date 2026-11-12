Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Frp1PinnedDownload {
    param([Parameter(Mandatory)][string]$ArchivePath)
    if ((Get-Item -LiteralPath $ArchivePath).Length -ne 49840952 -or
        (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash -cne 'E94D993BF91C8A43CE0959942602EBD18C831B788A133700150CEB928283129F') {
        throw 'Pinned Dalamud archive failed size or SHA-256 verification.'
    }
}

function ConvertTo-Frp1SdkEntryPath {
    param([Parameter(Mandatory)][string]$EntryName, [Parameter(Mandatory)][string]$Destination)
    $name = $EntryName.Replace('\', '/')
    $clean = $name.TrimEnd('/')
    if (-not $clean -or $name.Length -gt 240 -or $name.StartsWith('/') -or $name -match '[\x00-\x1f<>:"|?*]') { throw 'Unsafe pinned SDK entry.' }
    foreach ($segment in $clean.Split('/')) {
        if ($segment -in @('', '.', '..') -or $segment.EndsWith('.') -or $segment.EndsWith(' ') -or
            $segment -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)') { throw 'Unsafe pinned SDK path.' }
    }
    $root = [IO.Path]::GetFullPath($Destination).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $target = [IO.Path]::GetFullPath((Join-Path $root $clean))
    if (-not $target.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Escaped SDK entry.' }
    return $target
}

function Assert-Frp1SdkMetadata {
    param([Parameter(Mandatory)][string]$AssemblyVersion, [Parameter(Mandatory)][string]$CommitHash)
    if ($AssemblyVersion -cne '15.0.4.0' -or $CommitHash.Trim() -cne '12e318c1936e449e5883faa61214eb2ced478349') {
        throw 'Pinned Dalamud assembly or commit metadata mismatch.'
    }
}

Export-ModuleMember -Function Assert-Frp1PinnedDownload,ConvertTo-Frp1SdkEntryPath,Assert-Frp1SdkMetadata
