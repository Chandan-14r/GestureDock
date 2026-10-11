# GestureDock UIAccess Installer
# Grants GestureDock the ability to float directly over Windows 11 Task View

$ErrorActionPreference = "Stop"

# Auto-elevate to Administrator if not already elevated
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Requesting Administrator privileges..." -ForegroundColor Yellow
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    Exit
}

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " GestureDock: Windows 11 Task View Overlay Setup" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

# 1. Stop any currently running GestureDock instance
Write-Host "[1/5] Stopping active background instances..." -ForegroundColor Gray
Get-Process GestureDock -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

# 2. Create or load local self-signed Code Signing Certificate
Write-Host "[2/5] Setting up local Code Signing Certificate for UIAccess..." -ForegroundColor Gray
$certName = "GestureDock Local UIAccess"
$cert = Get-ChildItem -Path Cert:\LocalMachine\Root | Where-Object { $_.Subject -like "*$certName*" } | Select-Object -First 1

if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=$certName" -CertStoreLocation "Cert:\LocalMachine\My"
    
    # Trust locally in Root and Trusted Publishers
    $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "LocalMachine")
    $rootStore.Open("ReadWrite")
    $rootStore.Add($cert)
    $rootStore.Close()

    $pubStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPublisher", "LocalMachine")
    $pubStore.Open("ReadWrite")
    $pubStore.Add($cert)
    $pubStore.Close()
    Write-Host "       Certificate generated and trusted in LocalMachine Root." -ForegroundColor Green
} else {
    Write-Host "       Existing trusted certificate found." -ForegroundColor Green
}

# 3. Target Program Files directory
$targetDir = "$env:ProgramFiles\GestureDock"
Write-Host "[3/6] Installing GestureDock to $targetDir..." -ForegroundColor Gray
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
}

$sourceDir = if (Test-Path "$PSScriptRoot\publish_uiaccess") { "$PSScriptRoot\publish_uiaccess" } else { "$PSScriptRoot" }
Copy-Item "$sourceDir\*" -Destination $targetDir -Recurse -Force

# 4. Sign the executable with the trusted certificate
Write-Host "[4/6] Signing GestureDock.exe with UIAccess certificate..." -ForegroundColor Gray
Set-AuthenticodeSignature -Certificate $cert -FilePath "$targetDir\GestureDock.exe" | Out-Null
Write-Host "       Authenticode signature applied." -ForegroundColor Green

# 5. Update Shortcuts
Write-Host "[5/6] Updating Desktop shortcuts with modern Fluent icons..." -ForegroundColor Gray
$wsh = New-Object -ComObject WScript.Shell
$desktop = [Environment]::GetFolderPath("Desktop")
$iconsDir = "$targetDir\icons"

$scHome = $wsh.CreateShortcut("$desktop\GestureDock Home.lnk")
$scHome.TargetPath = "$targetDir\GestureDock.exe"
$scHome.Arguments = "--action home"
$scHome.WorkingDirectory = $targetDir
$scHome.IconLocation = "$iconsDir\Home.ico,0"
$scHome.Save()

$scClose = $wsh.CreateShortcut("$desktop\GestureDock Close All.lnk")
$scClose.TargetPath = "$targetDir\GestureDock.exe"
$scClose.Arguments = "--action closeall"
$scClose.WorkingDirectory = $targetDir
$scClose.IconLocation = "$iconsDir\CloseAll.ico,0"
$scClose.Save()

$scClear = $wsh.CreateShortcut("$desktop\GestureDock Clear All.lnk")
$scClear.TargetPath = "$targetDir\GestureDock.exe"
$scClear.Arguments = "--action clearall"
$scClear.WorkingDirectory = $targetDir
$scClear.IconLocation = "$iconsDir\ClearAll.ico,0"
$scClear.Save()

$scMain = $wsh.CreateShortcut("$desktop\GestureDock.lnk")
$scMain.TargetPath = "$targetDir\GestureDock.exe"
$scMain.WorkingDirectory = $targetDir
$scMain.IconLocation = "$iconsDir\GestureDock.ico,0"
$scMain.Save()

# 6. Configure Windows Auto-Start on System Boot/Logon
Write-Host "[6/6] Configuring automatic startup on Windows boot..." -ForegroundColor Gray
$taskName = "GestureDock-AutoStart"
try {
    # Task Scheduler at logon with highest privileges for silent UIAccess startup
    & schtasks /create /tn "$taskName" /tr "`"$targetDir\GestureDock.exe`"" /sc onlogon /rl highest /f | Out-Null
    Write-Host "       Windows Task Scheduler registered (runs automatically on boot/login)." -ForegroundColor Green
} catch { }

try {
    # HKCU Run entry as additional fallback
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "GestureDock" -Value "`"$targetDir\GestureDock.exe`"" -Force
    Write-Host "       HKCU Run registry entry created." -ForegroundColor Green
} catch { }

# Launch from Program Files with UIAccess
Start-Process "$targetDir\GestureDock.exe"

Write-Host ""
Write-Host "=========================================================" -ForegroundColor Green
Write-Host " SUCCESS! GestureDock is now running with UIAccess." -ForegroundColor Green
Write-Host " Swipe up with 3 fingers (or press Win+Tab) to test it!" -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Green
Write-Host "Press any key to close this window..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
