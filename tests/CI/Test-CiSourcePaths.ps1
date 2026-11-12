#requires -Version 7.2
[CmdletBinding()]
param([string] $BuildPropsPath = (Join-Path $PSScriptRoot '../../Directory.Build.props'))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$propsPath = (Resolve-Path -LiteralPath $BuildPropsPath).Path
$workRoot = Join-Path $PSScriptRoot ('bin/source-paths-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($workRoot)
$originalCi = $env:CI
$originalActions = $env:GITHUB_ACTIONS
$passed = 0
$failed = 0

function Assert-NeutralPath([string] $Path, [switch] $AllowFileName) {
    if ($AllowFileName -and $Path -match '^[A-Za-z0-9_.-]+\.pdb$') { return }
    if (-not $Path.StartsWith('/_/FRP1/', [StringComparison]::Ordinal)) {
        throw 'Compiled debug metadata does not use the neutral project root.'
    }
}

function Assert-PdbDocuments($Reader) {
    $sourceFound = $false
    foreach ($handle in $Reader.Documents) {
        $document = $Reader.GetDocument($handle)
        $name = $Reader.GetString($document.Name)
        try { Assert-NeutralPath $name }
        catch { throw 'A compiled PDB document does not use the neutral project root.' }
        if ($name.EndsWith('/FixtureSource.cs', [StringComparison]::Ordinal)) { $sourceFound = $true }
    }
    if (-not $sourceFound) { throw 'The compiled fixture source was not inspected in the PDB.' }
    $sourceLinkFound = $false
    foreach ($handle in $Reader.CustomDebugInformation) {
        $entry = $Reader.GetCustomDebugInformation($handle)
        if ($Reader.GetGuid($entry.Kind) -ne [Guid]'CC110556-A091-4D38-9FEC-25AB9A351A6A') { continue }
        $sourceLinkFound = $true
        $sourceLink = [Text.Encoding]::UTF8.GetString($Reader.GetBlobBytes($entry.Value)) | ConvertFrom-Json -AsHashtable
        if ($sourceLink.documents.Count -eq 0) { throw 'Source Link does not map any compiled source.' }
        foreach ($mappedRoot in $sourceLink.documents.Keys) {
            try { Assert-NeutralPath $mappedRoot }
            catch { throw 'A Source Link mapping does not use the neutral project root.' }
        }
    }
    if (-not $sourceLinkFound) { throw 'The actual compiler did not emit Source Link information.' }
}

try {
    foreach ($ci in @($false, $true)) {
        foreach ($debugType in @('portable', 'embedded')) {
            $caseName = "CI=$ci, DebugType=$debugType"
            try {
                $env:CI = if ($ci) { 'true' } else { $null }
                $env:GITHUB_ACTIONS = if ($ci) { 'true' } else { $null }
                $directory = Join-Path $workRoot (([int]$ci).ToString() + '-' + $debugType)
                [void][IO.Directory]::CreateDirectory($directory)
                $project = Join-Path $directory 'SourcePaths.csproj'
                [IO.File]::WriteAllText($project, @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <DebugType>$debugType</DebugType>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
  </PropertyGroup>
</Project>
"@)
                [IO.File]::WriteAllText((Join-Path $directory 'FixtureSource.cs'), 'public static class FixtureSource { public static int Value => 42; }')
                $buildOutput = @(& dotnet build $project -c Release --nologo -v minimal "-p:DirectoryBuildPropsPath=$propsPath" "-p:ContinuousIntegrationBuild=$($ci.ToString().ToLowerInvariant())" 2>&1)
                if ($LASTEXITCODE -ne 0) { throw ('Fixture build failed: ' + ($buildOutput -join "`n")) }
                $output = Join-Path $directory 'bin/Release/net10.0'
                $stream = [IO.File]::OpenRead((Join-Path $output 'SourcePaths.dll'))
                $pe = [Reflection.PortableExecutable.PEReader]::new($stream)
                try {
                    $codeViewFound = $false
                    $embeddedFound = $false
                    foreach ($entry in $pe.ReadDebugDirectory()) {
                        if ($entry.Type -eq [Reflection.PortableExecutable.DebugDirectoryEntryType]::CodeView) {
                            Assert-NeutralPath ($pe.ReadCodeViewDebugDirectoryData($entry).Path) -AllowFileName
                            $codeViewFound = $true
                        }
                        if ($entry.Type -eq [Reflection.PortableExecutable.DebugDirectoryEntryType]::EmbeddedPortablePdb) {
                            $provider = $pe.ReadEmbeddedPortablePdbDebugDirectoryData($entry)
                            try { Assert-PdbDocuments ($provider.GetMetadataReader()) }
                            finally { $provider.Dispose() }
                            $embeddedFound = $true
                        }
                    }
                    if (-not $codeViewFound) { throw 'No compiled CodeView path was inspected.' }
                    if ($debugType -eq 'embedded' -and -not $embeddedFound) { throw 'The embedded PDB was not inspected.' }
                }
                finally { $pe.Dispose(); $stream.Dispose() }
                if ($debugType -eq 'portable') {
                    $pdbStream = [IO.File]::OpenRead((Join-Path $output 'SourcePaths.pdb'))
                    $provider = [Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($pdbStream)
                    try { Assert-PdbDocuments ($provider.GetMetadataReader()) }
                    finally { $provider.Dispose(); $pdbStream.Dispose() }
                }
                $passed++
                Write-Output "PASS: $caseName emits neutral PDB and consistent Source Link mappings"
            }
            catch {
                $failed++
                Write-Output "FAIL: $caseName [$($_.Exception.GetType().Name)] $($_.Exception.Message)"
            }
        }
    }
}
finally {
    $env:CI = $originalCi
    $env:GITHUB_ACTIONS = $originalActions
    if (Test-Path -LiteralPath $workRoot) {
        $resolved = (Resolve-Path -LiteralPath $workRoot).Path
        $parent = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'bin')).TrimEnd('\') + '\'
        if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or
            -not [IO.Path]::GetFileName($resolved).StartsWith('source-paths-', [StringComparison]::Ordinal)) { throw 'Unsafe source path test cleanup target.' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
Write-Output "CI source-path checks: $passed passed, $failed failed."
if ($failed -ne 0) { throw "CI source-path verification failed: $failed checks." }
