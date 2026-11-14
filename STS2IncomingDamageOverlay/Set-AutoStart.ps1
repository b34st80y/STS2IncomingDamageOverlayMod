param([switch]$Disable)
$ErrorActionPreference = 'Stop'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$name = 'STS2IncomingDamageOverlay'
if ($Disable) {
    Remove-ItemProperty -LiteralPath $runKey -Name $name -ErrorAction SilentlyContinue
    Get-CimInstance Win32_Process -Filter "Name = 'STS2IncomingDamageOverlay.exe'" |
        Where-Object { $_.CommandLine -match '--watch' } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction SilentlyContinue }
    return
}
$exe = Join-Path $PSScriptRoot 'bin\Release\net9.0-windows\STS2IncomingDamageOverlay.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Build the Release overlay first.' }
New-Item -Path $runKey -Force | Out-Null
Set-ItemProperty -LiteralPath $runKey -Name $name -Value ('"' + $exe + '" --watch')
Start-Process -FilePath $exe -ArgumentList '--watch' -WindowStyle Hidden
