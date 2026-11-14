@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Set-AutoStart.ps1"
if errorlevel 1 (
    echo Automatic startup could not be enabled. See the error above.
) else (
    echo Automatic startup enabled. The overlay will open with Slay the Spire 2.
)
pause
