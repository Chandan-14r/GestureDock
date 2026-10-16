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
Write-Host "[1/6] Stopping active background instances..." -ForegroundColor Gray
try { & schtasks.exe /end /tn "GestureDock-AutoStart" 2>$null | Out-Null } catch {}
& taskkill.exe /F /IM "GestureDock.exe" /T 2>$null | Out-Null
Get-Process GestureDock -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# Actively wait for all GestureDock instances to fully exit
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
while ((Get-Process GestureDock -ErrorAction SilentlyContinue) -and ($stopwatch.Elapsed.TotalSeconds -lt 10)) {
    Start-Sleep -Milliseconds 250
}
Start-Sleep -Milliseconds 600

# 2. Create or load local self-signed Code Signing Certificate
Write-Host "[2/6] Setting up local Code Signing Certificate for UIAccess..." -ForegroundColor Gray
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

# If GestureDock.exe is currently present, cleanly remove or rename it to bypass pending Windows file locks
$targetExe = "$targetDir\GestureDock.exe"
if (Test-Path $targetExe) {
    for ($i = 0; $i -lt 5; $i++) {
        try {
            Remove-Item $targetExe -Force -ErrorAction Stop
            break
        } catch {
            try {
                $tempOld = "$targetDir\GestureDock.exe.old"
                if (Test-Path $tempOld) { Remove-Item $tempOld -Force -ErrorAction SilentlyContinue }
                Rename-Item $targetExe "GestureDock.exe.old" -Force -ErrorAction Stop
                break
            } catch {
                & taskkill.exe /F /IM "GestureDock.exe" /T 2>$null | Out-Null
                Start-Sleep -Milliseconds 600
            }
        }
    }
}

# Copy files with retry loop in case Windows kernel holds transient locks
$copied = $false
$retryCount = 0
$maxRetries = 6

while (-not $copied -and ($retryCount -lt $maxRetries)) {
    try {
        Copy-Item "$sourceDir\*" -Destination $targetDir -Recurse -Force -ErrorAction Stop
        $copied = $true
    } catch {
        $retryCount++
        if ($retryCount -lt $maxRetries) {
            Write-Host "       Waiting for Windows to release file handles (retry $retryCount/$maxRetries)..." -ForegroundColor Yellow
            & taskkill.exe /F /IM "GestureDock.exe" /T 2>$null | Out-Null
            Start-Sleep -Seconds 1
        } else {
            throw $_
        }
    }
}

# Clean up any temporary .old file
Remove-Item "$targetDir\*.old" -Force -ErrorAction SilentlyContinue
Write-Host "       Application files installed successfully." -ForegroundColor Green

# 4. Sign the executable with the trusted certificate
Write-Host "[4/6] Signing GestureDock.exe with UIAccess certificate..." -ForegroundColor Gray
Set-AuthenticodeSignature -Certificate $cert -FilePath "$targetDir\GestureDock.exe" | Out-Null
Write-Host "       Authenticode signature applied." -ForegroundColor Green

# 5. Clean up any unwanted desktop shortcuts
Write-Host "[5/6] Cleaning up desktop shortcuts..." -ForegroundColor Gray
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
Write-Host "       Desktop kept clean (no shortcut clutter)." -ForegroundColor Green

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
