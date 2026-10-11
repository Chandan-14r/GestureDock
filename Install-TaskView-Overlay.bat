@echo off
title GestureDock Installer
cd /d "%~dp0"
echo =========================================================
echo  GestureDock: Windows 11 Task View Companion Setup
echo =========================================================
echo.
echo Launching installer with Administrator elevation...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-TaskView-Overlay.ps1"
if %errorlevel% neq 0 (
    echo.
    echo Installation ended with error code %errorlevel%.
    pause
)
