@echo off
setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Micronote.ps1"

if errorlevel 1 (
    echo.
    echo Avvio di Micronote Fish non riuscito. Premi un tasto per chiudere.
    pause >nul
)
