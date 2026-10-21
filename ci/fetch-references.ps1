param(
    [Parameter(Mandatory=$true)][string]$Destination,
    [string]$LocalArchive
)
$ErrorActionPreference = 'Stop'
$expected = $env:GUNMAGGEDON_REFERENCES_SHA256
if ($expected -notmatch '^[a-fA-F0-9]{64}$') { throw 'Set the GUNMAGGEDON_REFERENCES_SHA256 repository variable to the private ZIP digest.' }
if (Test-Path -LiteralPath $Destination) { throw 'Reference extraction directory must not already exist.' }
$Destination = [IO.Path]::GetFullPath($Destination)
$downloaded = $false
if (-not $LocalArchive) {
    $uri = $null
    if (-not [Uri]::TryCreate($env:GUNMAGGEDON_REFERENCES_URL,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -ne 'https') {
        throw 'Set the GUNMAGGEDON_REFERENCES_URL Actions secret to the private ZIP download URL.'
    }
    $LocalArchive = [IO.Path]::GetTempFileName()
    $downloaded = $true
    $headers = @{ Accept = 'application/octet-stream' }
    if ($uri.Host -eq 'api.github.com' -and $uri.AbsolutePath -match '^/repos/[^/]+/[^/]+/contents/') {
        $headers.Accept = 'application/vnd.github.raw+json'
    }
    if ($env:GUNMAGGEDON_REFERENCES_TOKEN) { $headers.Authorization = 'Bearer ' + $env:GUNMAGGEDON_REFERENCES_TOKEN }
    try { Invoke-WebRequest -Uri $uri -Headers $headers -OutFile $LocalArchive -UseBasicParsing | Out-Null }
    catch { throw 'Private reference download failed. Check the configured URL and access token.' }
}
try {
    if ((Get-FileHash -LiteralPath $LocalArchive).Hash.ToLowerInvariant() -ne $expected.ToLowerInvariant()) {
        throw 'Private reference bundle digest differs from the configured version.'
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($LocalArchive)
    try {
        $names = @{}
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName
            if ($name -notmatch '^[A-Za-z0-9_.-]+\.dll$' -and $name -ne 'references.json') { throw 'The reference ZIP must contain only root-level DLLs and references.json.' }
            if ($names.ContainsKey($name.ToLowerInvariant())) { throw 'Duplicate archive entry.' }
            $names[$name.ToLowerInvariant()] = $true
        }
        foreach ($required in @('Assembly-CSharp.dll','UnityEngine.CoreModule.dll','mscorlib.dll','BepInEx.dll','0Harmony.dll','references.json')) {
            if (-not $names.ContainsKey($required.ToLowerInvariant())) { throw "Reference archive is missing $required" }
        }
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
        foreach ($entry in $archive.Entries) { [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $Destination $entry.FullName)) }
    } finally { $archive.Dispose() }
    Write-Output 'Private build references verified and extracted; no dependencies will be published.'
} finally {
    if ($downloaded -and (Test-Path -LiteralPath $LocalArchive)) { Remove-Item -LiteralPath $LocalArchive }
}
