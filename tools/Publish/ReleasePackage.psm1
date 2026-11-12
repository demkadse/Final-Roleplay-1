Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$script:Utf8 = [Text.UTF8Encoding]::new($false)
$script:StorePunchline = 'Früher öffentlicher Teststand: RP-Profile direkt zwischen Clients.'
$script:StoreDescription = 'Früher öffentlicher Teststand für lokale Rollenspielprofile und freiwilligen verschlüsselten Direktaustausch zwischen Clients. Keine Registrierung, Profilserver oder Telemetrie. Spiel- und netzübergreifende Abnahme stehen aus; NAT und Firewalls können Direktverbindungen verhindern.'
$script:PayloadNames = @('FRP1.Core.dll','FRP1.Core.pdb','FRP1.Plugin.deps.json','FRP1.Plugin.dll','FRP1.Plugin.json',
    'Microsoft.Win32.SystemEvents.dll','System.Drawing.Common.dll','System.Private.Windows.Core.dll',
    'System.Private.Windows.GdiPlus.dll','System.Security.Cryptography.ProtectedData.dll',
    'runtimes/win/lib/net10.0/Microsoft.Win32.SystemEvents.dll')
$script:Packages = @{
    'system.drawing.common' = 'iSYRRGk8lwVbWWeVi9E3ioXa3x0JhywbQ+Weq+efkq8qkGGDLZBEU1/PT/XPL6509CUISRN0IyNmIMMh19nBRw=='
    'microsoft.win32.systemevents' = 'zxYppUoNufXm4hnTAZlSDUNwjTp/IKs+thg4oZIoYB7E4a/ATGNtz2KbGSR0AZ3jSz2aEnNVFgUq8tcLQ/jnIQ=='
    'system.security.cryptography.protecteddata' = 'EotZMnndtZUr8irzX5KlhhPIa4kWTeejPTEAr3s77mhkkTIxEZImt9u6AnaA8L/LhXZHi33PxO1UKUO0JRlk5g=='
}
$script:Assets = @{
    'System.Drawing.Common.dll' = @('system.drawing.common','lib/net10.0/System.Drawing.Common.dll')
    'System.Private.Windows.Core.dll' = @('system.drawing.common','lib/net10.0/System.Private.Windows.Core.dll')
    'System.Private.Windows.GdiPlus.dll' = @('system.drawing.common','lib/net10.0/System.Private.Windows.GdiPlus.dll')
    'Microsoft.Win32.SystemEvents.dll' = @('microsoft.win32.systemevents','lib/net10.0/Microsoft.Win32.SystemEvents.dll')
    'runtimes/win/lib/net10.0/Microsoft.Win32.SystemEvents.dll' = @('microsoft.win32.systemevents','runtimes/win/lib/net10.0/Microsoft.Win32.SystemEvents.dll')
    'System.Security.Cryptography.ProtectedData.dll' = @('system.security.cryptography.protecteddata','lib/net10.0/System.Security.Cryptography.ProtectedData.dll')
}

if (-not ('Frp1.Publish.Inspector' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
namespace Frp1.Publish {
  public sealed class MetadataInfo {
    public string Name = "";
    public string Version = "";
    public string PdbGuid = "";
    public List<string> CodeViewPaths = new List<string>();
    public List<string> DebugGuids = new List<string>();
    public List<string> Documents = new List<string>();
  }
  public static class Inspector {
    static void Documents(MetadataReader reader, MetadataInfo result) {
      foreach (var handle in reader.Documents)
        result.Documents.Add(reader.GetString(reader.GetDocument(handle).Name).Replace('\\','/'));
    }
    public static MetadataInfo Assembly(byte[] bytes) {
      using var stream = new MemoryStream(bytes, false);
      using var pe = new PEReader(stream);
      if (!pe.HasMetadata) throw new InvalidDataException("Expected managed assembly.");
      var reader = pe.GetMetadataReader();
      var definition = reader.GetAssemblyDefinition();
      var result = new MetadataInfo { Name = reader.GetString(definition.Name), Version = definition.Version.ToString() };
      foreach (var entry in pe.ReadDebugDirectory()) {
        if (entry.Type == DebugDirectoryEntryType.CodeView) {
          var value = pe.ReadCodeViewDebugDirectoryData(entry);
          result.CodeViewPaths.Add(value.Path.Replace('\\','/'));
          result.DebugGuids.Add(value.Guid.ToString());
        }
        if (entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb) {
          using var provider = pe.ReadEmbeddedPortablePdbDebugDirectoryData(entry);
          Documents(provider.GetMetadataReader(), result);
        }
      }
      return result;
    }
    public static MetadataInfo PortablePdb(byte[] bytes) {
      using var stream = new MemoryStream(bytes, false);
      using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
      var reader = provider.GetMetadataReader();
      var result = new MetadataInfo { PdbGuid = new Guid(reader.DebugMetadataHeader.Id.Take(16).ToArray()).ToString() };
      Documents(reader, result);
      return result;
    }
  }
}
'@
}

function Get-Frp1Digest([byte[]]$Bytes) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)) }

function ConvertTo-Frp1CanonicalText([byte[]]$Bytes) {
    # Git on Windows may check text files out with CRLF. Preserve the complete
    # notice text while keeping our own staged text deterministic across clones.
    $text = $script:Utf8.GetString($Bytes).TrimStart([char]0xFEFF).Replace("`r`n", "`n")
    return ,$script:Utf8.GetBytes($text)
}

function Assert-Frp1Parameters([string]$Version, [string]$Tag, [string]$Repository, [long]$LastUpdateUnix) {
    if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$' -or
        @($Version.Split('.') | Where-Object { [long]$_ -gt 65534 }).Count -ne 0) { throw 'Invalid four-component plugin version.' }
    if ($Tag -cne "v$Version") { throw 'Release tag does not match the plugin version.' }
    if ($Repository -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}/[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$') { throw 'Invalid GitHub repository name.' }
    if ($LastUpdateUnix -lt 1577836800 -or $LastUpdateUnix -gt 4102444800) { throw 'LastUpdateUnix must be a UTC Unix timestamp between 2020 and 2100.' }
}

function ConvertTo-Frp1EntryName([string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Name) -or $Name.Length -gt 240) { throw 'Invalid ZIP entry name.' }
    $name = $Name.Replace('\','/')
    if ($name.StartsWith('/') -or $name -match '[\x00-\x1f<>:"|?*]') { throw 'Unsafe ZIP entry name.' }
    foreach ($part in $name.Split('/')) {
        if ($part -in @('','.', '..') -or $part.EndsWith('.') -or $part.EndsWith(' ') -or
            $part -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)') { throw 'Unsafe ZIP path segment.' }
    }
    return $name
}

function Read-Frp1Archive([string]$Path) {
    $archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Path))
    $result = [Collections.Generic.Dictionary[string,byte[]]]::new([StringComparer]::Ordinal)
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [long]$total = 0
    try {
        if ($archive.Entries.Count -gt 64) { throw 'Too many release ZIP entries.' }
        foreach ($entry in $archive.Entries) {
            $name = ConvertTo-Frp1EntryName $entry.FullName
            if (-not $names.Add($name)) { throw 'Duplicate ZIP entry.' }
            if ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) { throw 'ZIP symlinks are not allowed.' }
            $total += $entry.Length
            if ($entry.Length -gt 32MB -or $total -gt 64MB) { throw 'Release archive exceeds the size limit.' }
            $stream = $entry.Open(); $buffer = [IO.MemoryStream]::new()
            try {
                $chunk = [byte[]]::new(81920)
                while (($read = $stream.Read($chunk, 0, $chunk.Length)) -ne 0) {
                    if ($buffer.Length + $read -gt $entry.Length -or $buffer.Length + $read -gt 32MB) { throw 'ZIP entry exceeded its declared size.' }
                    $buffer.Write($chunk, 0, $read)
                }
                if ($buffer.Length -ne $entry.Length) { throw 'ZIP entry length mismatch.' }
                $result.Add($name, $buffer.ToArray())
            }
            finally { $buffer.Dispose(); $stream.Dispose() }
        }
    }
    finally { $archive.Dispose() }
    return ,$result
}

function Read-Frp1Json([byte[]]$Bytes, [bool]$Array = $false) {
    if ($Bytes.Length -eq 0 -or $Bytes.Length -gt 64KB) { throw 'Invalid release JSON size.' }
    $stream = [IO.MemoryStream]::new($Bytes, $false)
    $options = [Text.Json.JsonDocumentOptions]::new(); $options.MaxDepth = 16
    try { $document = [Text.Json.JsonDocument]::Parse($stream, $options) }
    catch { $stream.Dispose(); throw }
    try {
        $element = $document.RootElement
        if ($Array) {
            if ($element.ValueKind -ne [Text.Json.JsonValueKind]::Array -or $element.GetArrayLength() -ne 1) { throw 'Installer JSON must contain exactly one array entry.' }
            $element = $element[0]
        }
        if ($element.ValueKind -ne [Text.Json.JsonValueKind]::Object) { throw 'Expected a release JSON object.' }
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($property in $element.EnumerateObject()) { if (-not $seen.Add($property.Name)) { throw 'Duplicate release JSON property.' } }
        return ($element.GetRawText() | ConvertFrom-Json -AsHashtable -Depth 16)
    }
    finally { $document.Dispose(); $stream.Dispose() }
}

function Get-Frp1LegalFiles([string]$NuGetPackageRoot) {
    $result = [Collections.Generic.Dictionary[string,byte[]]]::new([StringComparer]::Ordinal)
    $runtime = ConvertTo-Frp1CanonicalText ([IO.File]::ReadAllBytes((Join-Path $script:ProjectRoot 'LICENSES/dotnet-runtime-10.0.14-LICENSE.TXT')))
    if ((Get-Frp1Digest $runtime) -cne 'CFC21F5E8BD655AE997EEC916138B707B1D290B83272C02A95C9F821B8C87310') { throw 'Official runtime MIT license changed.' }
    $assets = [Collections.Generic.Dictionary[string,byte[]]]::new([StringComparer]::Ordinal)
    foreach ($id in $script:Packages.Keys) {
        $packagePath = Join-Path $NuGetPackageRoot "$id/10.0.14/$id.10.0.14.nupkg"
        if ((Get-Item -LiteralPath $packagePath).Length -gt 16MB) { throw 'NuGet package exceeds the expected bounded size.' }
        $packageBytes = [IO.File]::ReadAllBytes($packagePath)
        if ([Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData($packageBytes)) -cne $script:Packages[$id]) { throw 'NuGet package does not match the pinned package hash.' }
        $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
        try {
            foreach ($file in @('LICENSE.TXT','THIRD-PARTY-NOTICES.TXT')) {
                $entry = $archive.GetEntry($file)
                $bytes = $runtime
                if ($entry) {
                    $input = $entry.Open(); $memory = [IO.MemoryStream]::new()
                    try { $input.CopyTo($memory); $bytes = $memory.ToArray() } finally { $memory.Dispose(); $input.Dispose() }
                }
                elseif ($file -ne 'LICENSE.TXT') { throw 'Official package notices are missing.' }
                $relative = "LICENSES/$id/10.0.14/$file"
                if ((Get-Frp1Digest (ConvertTo-Frp1CanonicalText ([IO.File]::ReadAllBytes((Join-Path $script:ProjectRoot $relative))))) -cne
                    (Get-Frp1Digest (ConvertTo-Frp1CanonicalText $bytes))) { throw 'Checked-in notice differs from the official source.' }
                $result.Add($relative, $bytes)
            }
            foreach ($name in $script:Assets.Keys) {
                if ($script:Assets[$name][0] -ne $id) { continue }
                $entry = $archive.GetEntry($script:Assets[$name][1])
                if (-not $entry) { throw 'Required official NuGet assembly is missing.' }
                $input = $entry.Open(); $memory = [IO.MemoryStream]::new()
                try { $input.CopyTo($memory); $assets.Add($name, $memory.ToArray()) } finally { $memory.Dispose(); $input.Dispose() }
            }
        }
        finally { $archive.Dispose() }
    }
    $license = ConvertTo-Frp1CanonicalText ([IO.File]::ReadAllBytes((Join-Path $script:ProjectRoot 'LICENSE')))
    if ((Get-Frp1Digest $license) -cne '32F09FBA8ABAD3D78A19D9394F282D6EE0233F12C000493A2962186E185DF1E3') { throw 'FRP1 MIT license differs from the approved standard text.' }
    $result.Add('LICENSE', $license)
    $result.Add('THIRD-PARTY-NOTICES.txt', (ConvertTo-Frp1CanonicalText ([IO.File]::ReadAllBytes((Join-Path $script:ProjectRoot 'THIRD-PARTY-NOTICES.txt')))))
    return @{ Legal = $result; Assets = $assets }
}

function Assert-Frp1Payload($Files, [string]$Version, [string]$Repository, $Official, [bool]$Release = $false) {
    $allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($name in $script:PayloadNames) { [void]$allowed.Add($name) }
    if ($Release) { foreach ($name in $Official.Legal.Keys) { [void]$allowed.Add($name) } }
    foreach ($name in $Files.Keys) { if (-not $allowed.Contains($name)) { throw "Unexpected or private release entry: $name" } }
    foreach ($name in $allowed) { if (-not $Files.ContainsKey($name)) { throw "Missing release entry: $name" } }
    $manifest = Read-Frp1Json $Files['FRP1.Plugin.json']
    if ($manifest.InternalName -cne 'FRP1.Plugin' -or $manifest.AssemblyVersion -cne $Version -or
        $manifest.DalamudApiLevel -isnot [long] -and $manifest.DalamudApiLevel -isnot [int] -or $manifest.DalamudApiLevel -ne 15) { throw 'Plugin manifest identity, version or API mismatch.' }
    $repoUrl = "https://github.com/$Repository"
    if (($Release -or $manifest.Contains('RepoUrl')) -and $manifest.RepoUrl -cne $repoUrl) { throw 'Plugin manifest repository mismatch.' }
    $debugPaths = [Collections.Generic.List[string]]::new(); $documents = [Collections.Generic.List[string]]::new()
    $core = $null; $plugin = $null
    foreach ($name in $Files.Keys) {
        if (-not $name.EndsWith('.dll', [StringComparison]::Ordinal)) { continue }
        $info = [Frp1.Publish.Inspector]::Assembly($Files[$name])
        $own = $name -cin @('FRP1.Plugin.dll','FRP1.Core.dll')
        if ($name -ceq 'FRP1.Plugin.dll') { $plugin = $info; if ($info.Name -cne 'FRP1.Plugin' -or $info.Version -cne $Version) { throw 'Plugin assembly metadata mismatch.' } }
        elseif ($name -ceq 'FRP1.Core.dll') { $core = $info; if ($info.Name -cne 'FRP1.Core') { throw 'Core assembly identity mismatch.' } }
        else { if ((Get-Frp1Digest $Files[$name]) -cne (Get-Frp1Digest $Official.Assets[$name])) { throw 'Bundled assembly differs from its pinned NuGet asset.' } }
        foreach ($path in $info.CodeViewPaths) {
            if ($path -notmatch '^[A-Za-z0-9_.-]+\.pdb$' -and -not $path.StartsWith($(if ($own) { '/_/FRP1/' } else { '/_/' }), [StringComparison]::Ordinal)) { throw 'Unmapped machine path in assembly debug metadata.' }
            $debugPaths.Add($path)
        }
        foreach ($path in $info.Documents) { if (-not $path.StartsWith($(if ($own) { '/_/FRP1/' } else { '/_/' }), [StringComparison]::Ordinal)) { throw 'Unmapped embedded PDB source path.' }; $documents.Add($path) }
    }
    $pdb = [Frp1.Publish.Inspector]::PortablePdb($Files['FRP1.Core.pdb'])
    if (-not $core.DebugGuids.Contains($pdb.PdbGuid) -or $pdb.Documents.Count -eq 0) { throw 'Core DLL and portable PDB do not match.' }
    foreach ($path in $pdb.Documents) { if (-not $path.StartsWith('/_/FRP1/', [StringComparison]::Ordinal)) { throw 'Unmapped machine path in portable PDB.' }; $documents.Add($path) }
    if ($Release) { foreach ($name in $Official.Legal.Keys) { if ((Get-Frp1Digest $Files[$name]) -cne (Get-Frp1Digest $Official.Legal[$name])) { throw 'Release license or notice differs from the official source.' } } }
    return @{ Manifest = $manifest; DebugPaths = $debugPaths.ToArray(); SourceDocuments = $documents.ToArray() }
}

function Write-Frp1Atomic([string]$Path, [byte[]]$Bytes) {
    if ([IO.File]::Exists($Path)) {
        if ((Get-Frp1Digest ([IO.File]::ReadAllBytes($Path))) -ceq (Get-Frp1Digest $Bytes)) { return }
        throw 'Refusing to overwrite a different staged artifact; choose a new staging directory.'
    }
    $temporary = $Path + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    try { [IO.File]::WriteAllBytes($temporary, $Bytes); [IO.File]::Move($temporary, $Path) }
    finally { if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) } }
}

function Test-Frp1ReleasePackage {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$PackagePath, [Parameter(Mandatory)][string]$RepoJsonPath,
        [Parameter(Mandatory)][long]$LastUpdateUnix, [string]$Version, [string]$Tag,
        [string]$Repository = 'demkadse/Final-Roleplay-1', [string]$NuGetPackageRoot = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'))
    $files = Read-Frp1Archive $PackagePath
    if (-not $Version) { $Version = (Read-Frp1Json $files['FRP1.Plugin.json']).AssemblyVersion }
    if (-not $Tag) { $Tag = "v$Version" }
    Assert-Frp1Parameters $Version $Tag $Repository $LastUpdateUnix
    $official = Get-Frp1LegalFiles $NuGetPackageRoot
    $validated = Assert-Frp1Payload $files $Version $Repository $official $true
    $entry = Read-Frp1Json ([IO.File]::ReadAllBytes($RepoJsonPath)) $true
    foreach ($key in @('InternalName','AssemblyVersion','DalamudApiLevel','RepoUrl','DownloadLinkInstall','DownloadLinkUpdate',
        'DownloadLinkTesting','IsHide','IsTestingExclusive','TestingAssemblyVersion','LastUpdate','Description','Punchline')) {
        if (-not $entry.Contains($key)) { throw 'Required installer metadata is missing.' }
    }
    foreach ($key in $validated.Manifest.Keys) {
        if ($key -in @('Description','Punchline')) { continue }
        if (-not $entry.Contains($key) -or (ConvertTo-Json -InputObject $entry[$key] -Compress -Depth 16) -cne
            (ConvertTo-Json -InputObject $validated.Manifest[$key] -Compress -Depth 16)) { throw 'Installer metadata differs from the packaged manifest.' }
    }
    $url = "https://github.com/$Repository/releases/download/$Tag/FRP1-$Version.zip"
    if ($entry.InternalName -cne 'FRP1.Plugin' -or $entry.AssemblyVersion -cne $Version -or $entry.DalamudApiLevel -ne 15 -or
        $entry.RepoUrl -cne "https://github.com/$Repository" -or $entry.DownloadLinkInstall -cne $url -or
        $entry.DownloadLinkUpdate -cne $url -or $entry.DownloadLinkTesting -cne $url -or
        $entry.IsHide -isnot [bool] -or $entry.IsHide -or $entry.IsTestingExclusive -isnot [bool] -or
        $entry.IsTestingExclusive -or $null -ne $entry.TestingAssemblyVersion -or $entry.LastUpdate -isnot [long] -and $entry.LastUpdate -isnot [int] -or
        $entry.LastUpdate -ne $LastUpdateUnix -or $entry.Description -cne $script:StoreDescription -or
        $entry.Punchline -cne $script:StorePunchline) { throw 'Installer metadata is inconsistent or requires an unintended testing opt-in.' }
    [pscustomobject]@{ PackagePath = [IO.Path]::GetFullPath($PackagePath); InstallerPath = [IO.Path]::GetFullPath($RepoJsonPath)
        Sha256 = (Get-Frp1Digest ([IO.File]::ReadAllBytes($PackagePath))); PluginVersion = $Version; ApiLevel = 15
        Entries = @($files.Keys | Sort-Object -CaseSensitive); DebugPaths = $validated.DebugPaths; SourceDocuments = $validated.SourceDocuments
        Licenses = @($official.Legal.Keys | Sort-Object -CaseSensitive); LastUpdateUnix = $LastUpdateUnix }
}

function New-Frp1ReleasePackage {
    [CmdletBinding()]
    param([Parameter(Mandatory)][long]$LastUpdateUnix,
        [string]$InputZip = (Join-Path $script:ProjectRoot 'src/FRP1.Plugin/bin/Release/FRP1.Plugin/latest.zip'),
        [string]$OutputDirectory,
        [string]$NuGetPackageRoot = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'),
        [string]$Version, [string]$Tag, [string]$Repository = 'demkadse/Final-Roleplay-1')
    $files = Read-Frp1Archive $InputZip
    if (-not $Version) { $Version = (Read-Frp1Json $files['FRP1.Plugin.json']).AssemblyVersion }
    if (-not $Tag) { $Tag = "v$Version" }
    Assert-Frp1Parameters $Version $Tag $Repository $LastUpdateUnix
    if (-not $OutputDirectory) { $OutputDirectory = Join-Path $script:ProjectRoot ".release-staging/$Tag" }
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    $stage = [IO.Path]::GetFullPath((Join-Path $script:ProjectRoot '.release-staging'))
    if ($output -ne $stage -and -not $output.StartsWith($stage + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputDirectory must stay inside the ignored .release-staging directory.' }
    $ancestor = $output
    while ($ancestor.Length -ge $script:ProjectRoot.Length) {
        if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse points are not allowed in the staging path.' }
        $parent = [IO.Path]::GetDirectoryName($ancestor); if (-not $parent -or $parent -eq $ancestor) { break }; $ancestor = $parent
    }
    $official = Get-Frp1LegalFiles $NuGetPackageRoot
    $validated = Assert-Frp1Payload $files $Version $Repository $official
    $manifest = $validated.Manifest
    $manifest.RepoUrl = "https://github.com/$Repository"
    $files['FRP1.Plugin.json'] = $script:Utf8.GetBytes(($manifest | ConvertTo-Json -Depth 16) + "`n")
    foreach ($name in $official.Legal.Keys) { $files.Add($name, $official.Legal[$name]) }
    $memory = [IO.MemoryStream]::new()
    try {
        $archive = [IO.Compression.ZipArchive]::new($memory, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($name in ($files.Keys | Sort-Object -CaseSensitive)) {
                $item = $archive.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
                $item.LastWriteTime = [DateTimeOffset]::FromUnixTimeSeconds($LastUpdateUnix)
                $item.ExternalAttributes = 0
                $stream = $item.Open(); try { $stream.Write($files[$name]) } finally { $stream.Dispose() }
            }
        }
        finally { $archive.Dispose() }
        $zipBytes = $memory.ToArray()
    }
    finally { $memory.Dispose() }
    $storeEntry = [ordered]@{}
    foreach ($key in $manifest.Keys) { $storeEntry[$key] = $manifest[$key] }
    $url = "https://github.com/$Repository/releases/download/$Tag/FRP1-$Version.zip"
    $storeEntry.Punchline = $script:StorePunchline
    $storeEntry.Description = $script:StoreDescription
    $storeEntry.DownloadLinkInstall = $url; $storeEntry.DownloadLinkUpdate = $url; $storeEntry.DownloadLinkTesting = $url
    $storeEntry.IsHide = $false; $storeEntry.IsTestingExclusive = $false; $storeEntry.TestingAssemblyVersion = $null; $storeEntry.LastUpdate = $LastUpdateUnix
    $jsonBytes = $script:Utf8.GetBytes((ConvertTo-Json -InputObject @($storeEntry) -Depth 16) + "`n")
    [IO.Directory]::CreateDirectory($output) | Out-Null
    $packagePath = Join-Path $output "FRP1-$Version.zip"
    $repoPath = Join-Path $output 'repo.json'
    Write-Frp1Atomic $packagePath $zipBytes
    Write-Frp1Atomic $repoPath $jsonBytes
    $sums = (Get-Frp1Digest $zipBytes) + "  FRP1-$Version.zip`n" + (Get-Frp1Digest $jsonBytes) + "  repo.json`n"
    Write-Frp1Atomic (Join-Path $output 'SHA256SUMS') ($script:Utf8.GetBytes($sums))
    Test-Frp1ReleasePackage -PackagePath $packagePath -RepoJsonPath $repoPath -LastUpdateUnix $LastUpdateUnix -Version $Version -Tag $Tag -Repository $Repository -NuGetPackageRoot $NuGetPackageRoot
}

Export-ModuleMember -Function New-Frp1ReleasePackage, Test-Frp1ReleasePackage
