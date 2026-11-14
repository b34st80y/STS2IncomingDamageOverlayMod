@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Set-AutoStart.ps1" -Disable
if errorlevel 1 (
    echo Automatic startup could not be disabled. See the error above.
) else (
    echo Automatic startup disabled. You can still launch the overlay manually.
)
pause
