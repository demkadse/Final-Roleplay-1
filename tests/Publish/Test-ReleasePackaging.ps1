#requires -Version 7.2
[CmdletBinding()]
param(
    [string] $InputZip = (Join-Path $PSScriptRoot '..\..\src\FRP1.Plugin\bin\Release\FRP1.Plugin\latest.zip'),
    [string] $NuGetPackageRoot = (Join-Path $env:USERPROFILE '.nuget\packages'),
    [string] $Version,
    [string] $Tag,
    [long] $LastUpdateUnix = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds(),
    [switch] $KeepArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$prepareScript = Join-Path $projectRoot 'tools\Publish\New-ReleasePackage.ps1'
$validateScript = Join-Path $projectRoot 'tools\Publish\Test-ReleasePackage.ps1'
$inputPath = (Resolve-Path -LiteralPath $InputZip).Path
if ([string]::IsNullOrWhiteSpace($Version)) {
    $sourceArchive = [IO.Compression.ZipFile]::OpenRead($inputPath)
    try {
        $sourceManifest = $sourceArchive.GetEntry('FRP1.Plugin.json')
        if ($null -eq $sourceManifest) { throw 'The input archive has no plugin manifest for version inference.' }
        $reader = [IO.StreamReader]::new($sourceManifest.Open())
        try { $Version = ($reader.ReadToEnd() | ConvertFrom-Json).AssemblyVersion }
        finally { $reader.Dispose() }
    }
    finally { $sourceArchive.Dispose() }
}
if ([string]::IsNullOrWhiteSpace($Tag)) { $Tag = 'v' + $Version }
$runId = [Guid]::NewGuid().ToString('N')
$fixtureRoot = Join-Path $PSScriptRoot ('bin\package-tests-' + $runId)
$stageRoot = Join-Path $projectRoot ('.release-staging\package-tests-' + $runId)
$repository = 'demkadse/Final-Roleplay-1'
$assetName = 'FRP1-' + $Version + '.zip'
$script:passed = 0
$script:failed = 0
$script:stageNumber = 0
[void][IO.Directory]::CreateDirectory($fixtureRoot)

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw [InvalidOperationException]::new($Message) }
}

function Invoke-Check([string] $Name, [scriptblock] $Action) {
    try {
        & $Action
        $script:passed++
        Write-Output ('PASS: ' + $Name)
    }
    catch {
        $script:failed++
        Write-Output ('FAIL: ' + $Name + ' [' + $_.Exception.GetType().Name + '] ' + $_.Exception.Message)
    }
}

function New-StagePath {
    $script:stageNumber++
    return Join-Path $stageRoot ('case-' + $script:stageNumber.ToString('D3'))
}

function Read-ZipBytes([IO.Compression.ZipArchiveEntry] $Entry) {
    $source = $Entry.Open()
    $memory = [IO.MemoryStream]::new()
    try { $source.CopyTo($memory); return ,$memory.ToArray() }
    finally { $source.Dispose(); $memory.Dispose() }
}

function Get-NormalizedEntries([string] $Path) {
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    try { return @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }) }
    finally { $zip.Dispose() }
}

function New-ArchiveFixture(
    [string] $Source,
    [string] $Destination,
    [string[]] $Omit = @(),
    [hashtable] $Replace = @{},
    [hashtable] $Extra = @{}
) {
    $original = [IO.Compression.ZipFile]::OpenRead($Source)
    $modified = [IO.Compression.ZipFile]::Open($Destination, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in $original.Entries) {
            $name = $entry.FullName.Replace('\', '/')
            if ($name -in $Omit) { continue }
            $created = $modified.CreateEntry($entry.FullName)
            $target = $created.Open()
            try {
                if ($Replace.ContainsKey($name)) {
                    [byte[]] $data = $Replace[$name]
                    $target.Write($data, 0, $data.Length)
                }
                else {
                    $sourceStream = $entry.Open()
                    try { $sourceStream.CopyTo($target) } finally { $sourceStream.Dispose() }
                }
            }
            finally { $target.Dispose() }
        }
        foreach ($name in $Extra.Keys) {
            $created = $modified.CreateEntry($name)
            $target = $created.Open()
            try {
                [byte[]] $data = $Extra[$name]
                $target.Write($data, 0, $data.Length)
            }
            finally { $target.Dispose() }
        }
    }
    finally { $modified.Dispose(); $original.Dispose() }
}

function Invoke-Prepare([string] $Source, [string] $Output, [string] $ReleaseTag = $tag, [string] $PackageRoot = $NuGetPackageRoot) {
    & $prepareScript -InputZip $Source -OutputDirectory $Output -NuGetPackageRoot $PackageRoot `
        -Version $version -Tag $ReleaseTag -Repository $repository -LastUpdateUnix $LastUpdateUnix | Out-Null
}

function Assert-PreparationRejected([string] $Source) {
    $rejected = $false
    try { Invoke-Prepare $Source (New-StagePath) *> $null }
    catch { $rejected = $true }
    Assert-True $rejected 'The invalid archive was accepted.'
}

function Normalize-Notice([string] $Text) { return $Text.Replace("`r`n", "`n").Trim() }

function Assert-NeutralDebugPath([string] $Path, [switch] $AllowFileName) {
    Assert-True (-not [string]::IsNullOrWhiteSpace($Path)) 'An own debug path is empty.'
    if ($AllowFileName -and $Path -cmatch '^[A-Za-z0-9_.-]+\.pdb$') { return }
    Assert-True ($Path.StartsWith('/_/FRP1/', [StringComparison]::Ordinal)) 'An own debug path does not use the neutral project mapping.'
    Assert-True ($Path -notmatch '[A-Za-z]:[\\/]|\\\\|/Users/|/boehm/') 'An own debug path exposes an absolute user directory.'
}

try {
    $sourceHash = (Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash
    $positiveStage = New-StagePath
    $releaseZip = Join-Path $positiveStage $assetName
    $repoJsonPath = Join-Path $positiveStage 'repo.json'

    Invoke-Check 'Offline release staging creates the three expected public artifacts' {
        Invoke-Prepare $inputPath $positiveStage
        foreach ($file in @($releaseZip, $repoJsonPath, (Join-Path $positiveStage 'SHA256SUMS'))) {
            Assert-True (Test-Path -LiteralPath $file -PathType Leaf) 'A required staged artifact is missing.'
        }
    }

    Invoke-Check 'Installer is a one-element array with stable version, API, tag and download URLs' {
        $raw = Get-Content -LiteralPath $repoJsonPath -Raw
        $document = [Text.Json.JsonDocument]::Parse($raw)
        try {
            Assert-True ($document.RootElement.ValueKind -eq [Text.Json.JsonValueKind]::Array) 'Installer root must be an array.'
            Assert-True ($document.RootElement.GetArrayLength() -eq 1) 'Installer array must contain exactly one plugin.'
        }
        finally { $document.Dispose() }
        $record = ($raw | ConvertFrom-Json -NoEnumerate)[0]
        Assert-True ($record.InternalName -eq 'FRP1.Plugin') 'Installer internal name differs from the DLL.'
        Assert-True ($record.AssemblyVersion -eq $version) 'Installer assembly version differs from the release.'
        Assert-True ($record.DalamudApiLevel -eq 15) 'Installer API level differs from the tested plugin.'
        Assert-True ($record.RepoUrl -ceq ('https://github.com/' + $repository)) 'Installer repository differs from the confirmed public repository.'
        $expectedUrl = 'https://github.com/' + $repository + '/releases/download/' + $tag + '/' + $assetName
        foreach ($field in @('DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')) {
            Assert-True ($record.$field -ceq $expectedUrl) 'A download field does not point to the exact release asset.'
        }
        $testing = $record.PSObject.Properties['IsTestingExclusive']
        Assert-True ($null -eq $testing -or $testing.Value -ne $true) 'Stable installation requires a testing opt-in.'
        Assert-True ($record.LastUpdate -eq $LastUpdateUnix) 'Installer LastUpdate differs from the requested timestamp.'
    }

    Invoke-Check 'Standalone public package validator accepts the authentic release package' {
        & $validateScript -PackagePath $releaseZip -RepoJsonPath $repoJsonPath -NuGetPackageRoot $NuGetPackageRoot `
            -Version $version -Tag $tag -Repository $repository -LastUpdateUnix $LastUpdateUnix | Out-Null
    }

    Invoke-Check 'SHA256SUMS authenticates the exact release asset bytes' {
        $actualHash = (Get-FileHash -LiteralPath $releaseZip -Algorithm SHA256).Hash
        $lines = @(Get-Content -LiteralPath (Join-Path $positiveStage 'SHA256SUMS'))
        $checksumMatches = @($lines | Where-Object { $_ -match ('(?i)^' + $actualHash + '\s+\*?' + [Regex]::Escape($assetName) + '$') })
        Assert-True ($checksumMatches.Count -eq 1) 'SHA256SUMS does not match the staged asset.'
    }

    Invoke-Check 'Release ZIP names are normalized and required own assemblies remain present' {
        $entries = Get-NormalizedEntries $releaseZip
        Assert-True ('FRP1.Plugin.dll' -in $entries -and 'FRP1.Core.dll' -in $entries -and 'FRP1.Plugin.json' -in $entries) 'A required own release file is missing.'
        $archive = [IO.Compression.ZipFile]::OpenRead($releaseZip)
        try {
            foreach ($entry in $archive.Entries) {
                Assert-True (-not $entry.FullName.Contains('\')) 'A final ZIP entry still uses a Windows separator.'
                Assert-True ($entry.FullName -notmatch '(^/|^[A-Za-z]:|(^|/)\.\.(/|$))') 'A final ZIP entry can escape its archive root.'
            }
        }
        finally { $archive.Dispose() }
    }

    Invoke-Check 'Own DLL CodeView paths and portable PDB documents use the neutral mapping' {
        $archive = [IO.Compression.ZipFile]::OpenRead($releaseZip)
        $codeViewCount = 0
        $documentCount = 0
        try {
            foreach ($entry in $archive.Entries) {
                if ($entry.FullName -notmatch '^FRP1\.(Core|Plugin)\.(dll|pdb)$') { continue }
                [byte[]] $bytes = Read-ZipBytes $entry
                $memory = [IO.MemoryStream]::new($bytes, $false)
                try {
                    if ($entry.FullName.EndsWith('.dll', [StringComparison]::OrdinalIgnoreCase)) {
                        $pe = [Reflection.PortableExecutable.PEReader]::new($memory)
                        try {
                            $assemblyReader = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
                            $assemblyVersion = $assemblyReader.GetAssemblyDefinition().Version.ToString()
                            $expectedAssemblyVersion = if ($entry.FullName -ceq 'FRP1.Plugin.dll') { $version } else { '1.0.0.0' }
                            Assert-True ($assemblyVersion -ceq $expectedAssemblyVersion) 'An own DLL has an unexpected assembly version; Core and Plugin versions are independent.'
                            foreach ($debugEntry in $pe.ReadDebugDirectory()) {
                                if ($debugEntry.Type -eq [Reflection.PortableExecutable.DebugDirectoryEntryType]::CodeView) {
                                    Assert-NeutralDebugPath ($pe.ReadCodeViewDebugDirectoryData($debugEntry).Path) -AllowFileName
                                    $codeViewCount++
                                }
                                elseif ($debugEntry.Type -eq [Reflection.PortableExecutable.DebugDirectoryEntryType]::EmbeddedPortablePdb) {
                                    $provider = $pe.ReadEmbeddedPortablePdbDebugDirectoryData($debugEntry)
                                    try {
                                        $reader = $provider.GetMetadataReader()
                                        foreach ($handle in $reader.Documents) {
                                            $sourceDocument = $reader.GetDocument($handle)
                                            Assert-NeutralDebugPath ($reader.GetString($sourceDocument.Name))
                                            $documentCount++
                                        }
                                    }
                                    finally { $provider.Dispose() }
                                }
                            }
                        }
                        finally { $pe.Dispose() }
                    }
                    else {
                        $provider = [Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($memory)
                        try {
                            $reader = $provider.GetMetadataReader()
                            foreach ($handle in $reader.Documents) {
                                $sourceDocument = $reader.GetDocument($handle)
                                Assert-NeutralDebugPath ($reader.GetString($sourceDocument.Name))
                                $documentCount++
                            }
                        }
                        finally { $provider.Dispose() }
                    }
                }
                finally { $memory.Dispose() }
            }
        }
        finally { $archive.Dispose() }
        Assert-True ($codeViewCount -ge 2) 'Both own DLLs must expose inspectable neutral CodeView paths.'
        Assert-True ($documentCount -gt 0) 'Bundled own PDB documents were not inspected.'
    }

    $archive = [IO.Compression.ZipFile]::OpenRead($releaseZip)
    $bundledText = [Collections.Generic.List[string]]::new()
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.Replace('\', '/') -notlike 'LICENSES/*') { continue }
            $bundledText.Add((Normalize-Notice ([Text.Encoding]::UTF8.GetString((Read-ZipBytes $entry)))))
        }
    }
    finally { $archive.Dispose() }
    foreach ($package in @('microsoft.win32.systemevents', 'system.drawing.common', 'system.security.cryptography.protecteddata')) {
        Invoke-Check ('Complete third-party notices are bundled for ' + $package) {
            $sourceNotice = Join-Path $NuGetPackageRoot ($package + '\10.0.14\THIRD-PARTY-NOTICES.TXT')
            $expectedNotice = Normalize-Notice (Get-Content -LiteralPath $sourceNotice -Raw)
            Assert-True (@($bundledText | Where-Object { $_.Contains($expectedNotice, [StringComparison]::Ordinal) }).Count -gt 0) 'A complete dependency notice is missing or replaced with a stub.'
        }
    }
    Invoke-Check 'Complete original MIT license is bundled' {
        $license = Normalize-Notice (Get-Content -LiteralPath (Join-Path $NuGetPackageRoot 'system.drawing.common\10.0.14\LICENSE.TXT') -Raw)
        Assert-True (@($bundledText | Where-Object { $_.Contains($license, [StringComparison]::Ordinal) }).Count -gt 0) 'The complete original MIT license is missing.'
    }
    Invoke-Check 'Own MIT license is bundled completely with the confirmed copyright' {
        $ownLicense = Normalize-Notice (Get-Content -LiteralPath (Join-Path $projectRoot 'LICENSE') -Raw)
        Assert-True ($ownLicense.Contains('Copyright (c) 2026 demkadse', [StringComparison]::Ordinal)) 'The own MIT license has an unexpected copyright holder.'
        Assert-True ($ownLicense.Contains('Permission is hereby granted, free of charge', [StringComparison]::Ordinal)) 'The own license omits the MIT permission grant.'
        Assert-True ($ownLicense.Contains('THE SOFTWARE IS PROVIDED "AS IS"', [StringComparison]::Ordinal)) 'The own license omits the MIT disclaimer.'
        $archive = [IO.Compression.ZipFile]::OpenRead($releaseZip)
        try {
            $entry = $archive.GetEntry('LICENSE')
            Assert-True ($null -ne $entry) 'The own license is missing from the release root.'
            $included = Normalize-Notice ([Text.Encoding]::UTF8.GetString((Read-ZipBytes $entry)))
            Assert-True ($included -ceq $ownLicense) 'The bundled own license differs from the complete repository license.'
        }
        finally { $archive.Dispose() }
    }
    Invoke-Check 'Repeated preparation with identical input and timestamp reproduces ZIP and installer hashes' {
        $repeatedStage = New-StagePath
        Invoke-Prepare $inputPath $repeatedStage
        foreach ($name in @($assetName, 'repo.json', 'SHA256SUMS')) {
            $firstHash = (Get-FileHash -LiteralPath (Join-Path $positiveStage $name) -Algorithm SHA256).Hash
            $secondHash = (Get-FileHash -LiteralPath (Join-Path $repeatedStage $name) -Algorithm SHA256).Hash
            Assert-True ($firstHash -ceq $secondHash) 'Repeated preparation changed an artifact despite identical input and timestamp.'
        }
    }

    $fixtureNumber = 0
    $absoluteSynthetic = (Join-Path $fixtureRoot 'synthetic-absolute-escape.txt').Replace('\', '/')
    foreach ($unsafeName in @('../synthetic-escape.txt', '..\synthetic-escape.txt', 'nested/../../synthetic-escape.txt', $absoluteSynthetic)) {
        $fixtureNumber++
        $fixture = Join-Path $fixtureRoot ('unsafe-' + $fixtureNumber + '.zip')
        New-ArchiveFixture $inputPath $fixture -Extra @{ $unsafeName = [Text.Encoding]::UTF8.GetBytes('Synthetic harmless fixture') }
        Invoke-Check ('Unsafe ZIP entry is refused, variant ' + $fixtureNumber) { Assert-PreparationRejected $fixture }
    }
    foreach ($privateName in @('.env', 'auth.json', 'peer-identity.dpapi', 'profiles/synthetic.json')) {
        $fixtureNumber++
        $fixture = Join-Path $fixtureRoot ('private-' + $fixtureNumber + '.zip')
        New-ArchiveFixture $inputPath $fixture -Extra @{ $privateName = [Text.Encoding]::UTF8.GetBytes('Synthetic fixture; no credentials') }
        Invoke-Check ('Private or user-data entry is refused: ' + $privateName) { Assert-PreparationRejected $fixture }
    }
    foreach ($missing in @('FRP1.Plugin.dll', 'FRP1.Core.dll')) {
        $fixtureNumber++
        $fixture = Join-Path $fixtureRoot ('missing-' + $fixtureNumber + '.zip')
        New-ArchiveFixture $inputPath $fixture -Omit @($missing)
        Invoke-Check ('Missing required DLL is refused: ' + $missing) { Assert-PreparationRejected $fixture }
    }
    $originalArchive = [IO.Compression.ZipFile]::OpenRead($inputPath)
    try { $manifestText = [Text.Encoding]::UTF8.GetString((Read-ZipBytes ($originalArchive.GetEntry('FRP1.Plugin.json')))) }
    finally { $originalArchive.Dispose() }
    foreach ($field in @('AssemblyVersion', 'DalamudApiLevel', 'InternalName')) {
        $fixtureNumber++
        $fixture = Join-Path $fixtureRoot ('metadata-' + $fixtureNumber + '.zip')
        $manifest = $manifestText | ConvertFrom-Json
        if ($field -eq 'DalamudApiLevel') { $manifest.$field = 999 }
        elseif ($field -eq 'AssemblyVersion') { $manifest.$field = '9.9.9.9' }
        else { $manifest.$field = 'Unexpected.Plugin' }
        New-ArchiveFixture $inputPath $fixture -Replace @{ 'FRP1.Plugin.json' = [Text.Encoding]::UTF8.GetBytes(($manifest | ConvertTo-Json -Depth 10)) }
        Invoke-Check ('Tampered metadata is refused: ' + $field) { Assert-PreparationRejected $fixture }
    }
    Invoke-Check 'Duplicate ZIP entries cannot shadow a required assembly' {
        $fixture = Join-Path $fixtureRoot 'duplicate.zip'
        New-ArchiveFixture $inputPath $fixture -Extra @{ 'FRP1.Core.dll' = [Text.Encoding]::UTF8.GetBytes('Synthetic duplicate') }
        Assert-PreparationRejected $fixture
    }
    Invoke-Check 'Release tag must agree with the requested assembly version' {
        $rejected = $false
        try { Invoke-Prepare $inputPath (New-StagePath) 'v9.9.9.9' *> $null } catch { $rejected = $true }
        Assert-True $rejected 'Mismatched version/tag was accepted.'
    }
    Invoke-Check 'Missing dependency licenses cause failure without downloading replacements' {
        $emptyPackages = Join-Path $fixtureRoot 'empty-package-cache'
        [void][IO.Directory]::CreateDirectory($emptyPackages)
        $rejected = $false
        try { Invoke-Prepare $inputPath (New-StagePath) -PackageRoot $emptyPackages *> $null } catch { $rejected = $true }
        Assert-True $rejected 'Missing license sources were silently accepted.'
    }
    Invoke-Check 'Public validator rejects a release with license files removed' {
        $fixture = Join-Path $fixtureRoot 'missing-licenses.zip'
        $omitted = @(Get-NormalizedEntries $releaseZip | Where-Object { $_ -like 'LICENSES/*' })
        Assert-True ($omitted.Count -gt 0) 'No bundled license files were found to exercise the negative fixture.'
        New-ArchiveFixture $releaseZip $fixture -Omit $omitted
        $rejected = $false
        try {
            & $validateScript -PackagePath $fixture -RepoJsonPath $repoJsonPath -NuGetPackageRoot $NuGetPackageRoot `
                -Version $version -Tag $tag -Repository $repository -LastUpdateUnix $LastUpdateUnix *> $null
        }
        catch { $rejected = $true }
        Assert-True $rejected 'The public validator accepted a release without notices.'
    }
    Invoke-Check 'Public validator rejects a release with the own MIT license missing' {
        $fixture = Join-Path $fixtureRoot 'missing-own-license.zip'
        New-ArchiveFixture $releaseZip $fixture -Omit @('LICENSE')
        $rejected = $false
        try {
            & $validateScript -PackagePath $fixture -RepoJsonPath $repoJsonPath -NuGetPackageRoot $NuGetPackageRoot `
                -Version $version -Tag $tag -Repository $repository -LastUpdateUnix $LastUpdateUnix *> $null
        }
        catch { $rejected = $true }
        Assert-True $rejected 'The public validator accepted a release without the own license.'
    }
    Invoke-Check 'Public validator rejects a release with a truncated or replaced own license' {
        $fixture = Join-Path $fixtureRoot 'tampered-own-license.zip'
        New-ArchiveFixture $releaseZip $fixture -Replace @{ 'LICENSE' = [Text.Encoding]::UTF8.GetBytes("MIT License`nSynthetic incomplete fixture") }
        $rejected = $false
        try {
            & $validateScript -PackagePath $fixture -RepoJsonPath $repoJsonPath -NuGetPackageRoot $NuGetPackageRoot `
                -Version $version -Tag $tag -Repository $repository -LastUpdateUnix $LastUpdateUnix *> $null
        }
        catch { $rejected = $true }
        Assert-True $rejected 'The public validator accepted a release with the own license replaced.'
    }
    Invoke-Check 'Existing staged files are never overwritten by release preparation' {
        $existing = New-StagePath
        [void][IO.Directory]::CreateDirectory($existing)
        $sentinel = Join-Path $existing $assetName
        Set-Content -LiteralPath $sentinel -Value 'Preserve this synthetic fixture' -NoNewline
        $rejected = $false
        try { Invoke-Prepare $inputPath $existing *> $null } catch { $rejected = $true }
        Assert-True $rejected 'Preparation overwrote an existing staged release asset.'
        Assert-True ((Get-Content -LiteralPath $sentinel -Raw) -ceq 'Preserve this synthetic fixture') 'Preparation changed a preexisting staged file.'
    }
    Invoke-Check 'Authentic SDK input archive remains byte-for-byte unchanged' {
        Assert-True ((Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash -ceq $sourceHash) 'The authentic input package was modified.'
    }
}
finally {
    if (-not $KeepArtifacts) {
        foreach ($target in @(
            @{ Path = $fixtureRoot; Parent = (Join-Path $PSScriptRoot 'bin') },
            @{ Path = $stageRoot; Parent = (Join-Path $projectRoot '.release-staging') }
        )) {
            if (-not (Test-Path -LiteralPath $target.Path)) { continue }
            $resolved = (Resolve-Path -LiteralPath $target.Path).Path
            $expectedParent = [IO.Path]::GetFullPath($target.Parent).TrimEnd('\') + '\'
            if (-not $resolved.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase) -or
                -not [IO.Path]::GetFileName($resolved).StartsWith('package-tests-', [StringComparison]::Ordinal)) {
                throw 'Refusing cleanup outside this test run.'
            }
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}

Write-Output ('Release packaging checks: ' + $script:passed + ' passed, ' + $script:failed + ' failed.')
if ($script:failed -ne 0) { throw ('Release packaging verification failed: ' + $script:failed + ' checks.') }
exit 0
