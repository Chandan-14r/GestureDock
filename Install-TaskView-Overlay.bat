@echo off
setlocal EnableDelayedExpansion
title GestureDock Installer

:: Check for Administrator elevation
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo =========================================================
    echo  GestureDock: Requesting Administrator privileges...
    echo =========================================================
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process cmd.exe -ArgumentList '/k `\"\"%~f0\"\"`' -Verb RunAs"
    exit /b
)

:: Elevated: Change working directory to batch file location
cd /d "%~dp0"

echo =========================================================
echo  GestureDock: Windows 11 Task View Companion Setup
echo =========================================================
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-TaskView-Overlay.ps1"
set INSTALL_ERR=%errorlevel%

if %INSTALL_ERR% neq 0 (
    echo.
    echo =========================================================
    echo  Installation encountered an issue (Code: %INSTALL_ERR%).
    echo =========================================================
    pause
)
