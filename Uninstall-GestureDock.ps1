# GestureDock Uninstaller
$ErrorActionPreference = "SilentlyContinue"

# Auto-elevate to Administrator
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Requesting Administrator privileges..." -ForegroundColor Yellow
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    Exit
}

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " GestureDock: Uninstallation" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

# 1. Stop background processes
Write-Host "[1/4] Stopping running GestureDock processes..." -ForegroundColor Gray
Get-Process GestureDock -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# 2. Remove Task Scheduler & Registry Auto-start
Write-Host "[2/4] Removing startup entries..." -ForegroundColor Gray
& schtasks /delete /tn "GestureDock-AutoStart" /f | Out-Null
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "GestureDock" -ErrorAction SilentlyContinue

# 3. Remove Desktop shortcuts
Write-Host "[3/4] Removing Desktop shortcuts..." -ForegroundColor Gray
$desktop = [Environment]::GetFolderPath("Desktop")
@("GestureDock Home.lnk", "GestureDock Close All.lnk", "GestureDock Clear All.lnk", "GestureDock.lnk") | ForEach-Object {
    $sc = Join-Path $desktop $_
    if (Test-Path $sc) { Remove-Item $sc -Force }
}

# 4. Remove installation files
Write-Host "[4/4] Removing program files..." -ForegroundColor Gray
$targetDir = "$env:ProgramFiles\GestureDock"
if (Test-Path $targetDir) {
    Remove-Item -Path $targetDir -Recurse -Force
}

Write-Host ""
Write-Host "=========================================================" -ForegroundColor Green
Write-Host " SUCCESS! GestureDock has been completely uninstalled." -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Green
Write-Host "Press any key to close..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
