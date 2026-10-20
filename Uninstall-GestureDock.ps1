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
try { & schtasks.exe /end /tn "GestureDock-AutoStart" 2>$null | Out-Null } catch {}
& taskkill.exe /F /IM "GestureDock.exe" /T 2>$null | Out-Null
Get-Process GestureDock -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

# 2. Remove Task Scheduler & Registry Auto-start
Write-Host "[2/4] Removing startup entries..." -ForegroundColor Gray
& schtasks /delete /tn "GestureDock-AutoStart" /f 2>$null | Out-Null
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "GestureDock" -ErrorAction SilentlyContinue
$startupDir = [Environment]::GetFolderPath("Startup")
if (Test-Path (Join-Path $startupDir "GestureDock.lnk")) {
    Remove-Item (Join-Path $startupDir "GestureDock.lnk") -Force -ErrorAction SilentlyContinue
}

# 3. Remove Desktop shortcuts
Write-Host "[3/4] Removing Desktop shortcuts..." -ForegroundColor Gray
$desktopPaths = @(
    [Environment]::GetFolderPath("Desktop"),
    [System.IO.Path]::Combine($env:USERPROFILE, "Desktop"),
    [System.IO.Path]::Combine($env:USERPROFILE, "OneDrive", "Desktop"),
    [Environment]::GetFolderPath("CommonDesktopDirectory")
) | Select-Object -Unique

foreach ($dp in $desktopPaths) {
    if (Test-Path $dp) {
        @("GestureDock Home.lnk", "GestureDock Close All.lnk", "GestureDock Clear All.lnk", "GestureDock.lnk") | ForEach-Object {
            $sc = Join-Path $dp $_
            if (Test-Path $sc) { Remove-Item $sc -Force -ErrorAction SilentlyContinue }
        }
    }
}

# 4. Remove installation files
Write-Host "[4/4] Removing program files..." -ForegroundColor Gray
$targetDir = "$env:ProgramFiles\GestureDock"
if (Test-Path $targetDir) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ((Get-Process GestureDock -ErrorAction SilentlyContinue) -and ($sw.Elapsed.TotalSeconds -lt 5)) {
        Start-Sleep -Milliseconds 200
    }
    try {
        Remove-Item -Path $targetDir -Recurse -Force -ErrorAction Stop
    } catch {
        Get-ChildItem -Path $targetDir -Recurse | ForEach-Object {
            try { Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue } catch {}
        }
    }
}

Write-Host ""
Write-Host "=========================================================" -ForegroundColor Green
Write-Host " SUCCESS! GestureDock has been completely uninstalled." -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Green
Write-Host "Press any key to close..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
