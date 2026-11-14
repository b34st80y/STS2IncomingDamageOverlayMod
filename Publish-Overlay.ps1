param([string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'STS2IncomingDamageOverlay'
$outDir = Join-Path $PSScriptRoot "artifacts/overlay-$Version"
if (Test-Path -LiteralPath $outDir) { throw "Output already exists: $outDir. Use a fresh version or move the existing output first." }
$package = Join-Path $outDir 'STS2-Incoming-Damage-Overlay-win-x64'
dotnet publish (Join-Path $project 'STS2IncomingDamageOverlay.csproj') -c Release -r win-x64 --self-contained true -p:Version=$Version -p:UseSharedCompilation=false -o $package
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
foreach ($file in @('Set-AutoStart.ps1', 'Enable-AutoStart.cmd', 'Disable-AutoStart.cmd', 'RELEASE-README.txt')) {
    Copy-Item -LiteralPath (Join-Path $project $file) -Destination $package
}
$zip = Join-Path $outDir 'STS2-Incoming-Damage-Overlay-win-x64.zip'
Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $outDir 'SHA256SUMS.txt'), "$hash  STS2-Incoming-Damage-Overlay-win-x64.zip`n", [Text.UTF8Encoding]::new($false))
Write-Output $zip
