@echo off
setlocal EnableDelayedExpansion
title GestureDock Uninstaller

:: Check for Administrator elevation
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo =========================================================
    echo  GestureDock: Requesting Administrator privileges...
    echo =========================================================
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process cmd.exe -ArgumentList '/c `\"\"%~f0\"\"`' -Verb RunAs"
    exit /b
)

:: Elevated: Change working directory to batch file location
cd /d "%~dp0"

echo =========================================================
echo  GestureDock: Uninstallation
echo =========================================================
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall-GestureDock.ps1"
