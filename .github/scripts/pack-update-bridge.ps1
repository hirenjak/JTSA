param(
    [Parameter(Mandatory = $true)] [string] $Version,
    [string] $ReleaseDirectory = 'Releases'
)

$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath (Join-Path $ReleaseDirectory "JTSA-$Version-full.nupkg")).Path
$releaseDirectory = (Resolve-Path -LiteralPath $ReleaseDirectory).Path
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('JTSA-UpdateBridge-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null

try {
    Add-Type -AssemblyName System.IO.Compression
    $stockUpdater = Join-Path $temporaryDirectory 'VelopackUpdater.exe'
    $zip = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $entry = $zip.GetEntry('lib/app/Squirrel.exe')
        if ($null -eq $entry) { throw 'The Velopack updater is missing from the release package.' }
        $inputStream = $entry.Open()
        $outputStream = [IO.File]::Create($stockUpdater)
        try { $inputStream.CopyTo($outputStream) }
        finally { $outputStream.Dispose(); $inputStream.Dispose() }
    }
    finally { $zip.Dispose() }

    $publishDirectory = Join-Path $temporaryDirectory 'publish'
    dotnet publish installer/JTSA.UpdateBridge/JTSA.UpdateBridge.csproj `
        --configuration Release --runtime win-x64 --self-contained true `
        --output $publishDirectory `
        "-p:VelopackUpdaterPath=$stockUpdater"
    if ($LASTEXITCODE -ne 0) { throw 'Update bridge publish failed.' }

    $bridge = Join-Path $publishDirectory 'Squirrel.exe'
    if (-not (Test-Path -LiteralPath $bridge)) { throw 'The update bridge executable was not produced.' }
    $zip = [IO.Compression.ZipFile]::Open($package, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $entry = $zip.GetEntry('lib/app/Squirrel.exe')
        if ($null -eq $entry) { throw 'The Velopack updater disappeared from the release package.' }
        $entry.Delete()
        $replacement = $zip.CreateEntry('lib/app/Squirrel.exe', [IO.Compression.CompressionLevel]::Optimal)
        $inputStream = [IO.File]::OpenRead($bridge)
        $outputStream = $replacement.Open()
        try { $inputStream.CopyTo($outputStream) }
        finally { $outputStream.Dispose(); $inputStream.Dispose() }
    }
    finally { $zip.Dispose() }

    $sha1 = (Get-FileHash -LiteralPath $package -Algorithm SHA1).Hash
    $sha256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    $size = (Get-Item -LiteralPath $package).Length
    $fileName = [IO.Path]::GetFileName($package)

    $releasesFile = Join-Path $releaseDirectory 'releases.win.json'
    $releases = Get-Content -LiteralPath $releasesFile -Raw | ConvertFrom-Json
    $asset = @($releases.Assets | Where-Object { $_.FileName -eq $fileName -and $_.Type -eq 'Full' })
    if ($asset.Count -ne 1) { throw "Expected one full release record for $fileName." }
    $asset[0].SHA1 = $sha1
    $asset[0].SHA256 = $sha256
    $asset[0].Size = $size
    [IO.File]::WriteAllText($releasesFile, ($releases | ConvertTo-Json -Depth 20 -Compress))

    $legacyFeed = Join-Path $releaseDirectory 'RELEASES'
    $lines = [IO.File]::ReadAllLines($legacyFeed)
    $matches = @($lines | Where-Object { ($_ -split '\s+')[1] -eq $fileName })
    if ($matches.Count -ne 1) { throw "Expected one legacy release record for $fileName." }
    $lines = @($lines | ForEach-Object {
        if (($_ -split '\s+')[1] -eq $fileName) { "$sha1 $fileName $size" } else { $_ }
    })
    [IO.File]::WriteAllLines($legacyFeed, $lines)
}
finally {
    $resolvedTemporary = [IO.Path]::GetFullPath($temporaryDirectory)
    $expectedParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
    if ([IO.Path]::GetDirectoryName($resolvedTemporary).TrimEnd('\', '/') -ne $expectedParent -or
        -not [IO.Path]::GetFileName($resolvedTemporary).StartsWith('JTSA-UpdateBridge-')) {
        throw "Unexpected temporary path: $resolvedTemporary"
    }
    Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force -ErrorAction SilentlyContinue
}
