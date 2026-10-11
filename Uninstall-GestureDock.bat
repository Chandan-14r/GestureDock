@echo off
title GestureDock Uninstaller
cd /d "%~dp0"
echo =========================================================
echo  GestureDock: Uninstallation
echo =========================================================
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall-GestureDock.ps1"
