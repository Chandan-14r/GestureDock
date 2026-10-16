# GestureDock UIAccess Installer
# Grants GestureDock the ability to float directly over Windows 11 Task View

$ErrorActionPreference = "Stop"

# Auto-elevate to Administrator if not already elevated
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Requesting Administrator privileges..." -ForegroundColor Yellow
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    Exit
}

function Test-FileExclusiveAccess([string]$path) {
    if (-not (Test-Path $path)) { return $true }
    try {
        $fs = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $fs.Dispose()
        return $true
    } catch {
        return $false
    }
}

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " GestureDock: Windows 11 Task View Overlay Setup" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

# 1. Stop any currently running GestureDock instance
Write-Host "[1/6] Stopping active background instances..." -ForegroundColor Gray
try { & schtasks.exe /end /tn "GestureDock-AutoStart" 2>$null | Out-Null } catch {}
& taskkill.exe /F /IM "GestureDock.exe" /T 2>$null | Out-Null
Get-Process GestureDock -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
while ((Get-Process GestureDock -ErrorAction SilentlyContinue) -and ($stopwatch.Elapsed.TotalSeconds -lt 5)) {
    Start-Sleep -Milliseconds 200
}
Start-Sleep -Milliseconds 500

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

$sourceDir = if (Test-Path "$PSScriptRoot\publish_uiaccess\GestureDock.exe") {
    "$PSScriptRoot\publish_uiaccess"
} elseif (Test-Path "$PSScriptRoot\GestureDock.exe") {
    "$PSScriptRoot"
} else {
    throw "GestureDock.exe not found in $PSScriptRoot or publish_uiaccess!"
}

# Wait for any lingering kernel/DWM locks on target GestureDock.exe
$targetExe = "$targetDir\GestureDock.exe"
if (Test-Path $targetExe) {
    $lockWait = [System.Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-FileExclusiveAccess $targetExe) -and ($lockWait.Elapsed.TotalSeconds -lt 8)) {
        & taskkill.exe /F /IM "GestureDock.exe" /T 2>$null | Out-Null
        Start-Sleep -Milliseconds 400
    }

    # If it is STILL locked by Windows kernel teardown, rename it to .old
    if (-not (Test-FileExclusiveAccess $targetExe)) {
        $timestamp = (Get-Date).ToString("yyyyMMdd_HHmmss")
        $oldFile = "$targetDir\GestureDock.exe.old_$timestamp"
        Write-Host "       Moving locked binary aside to $(Split-Path $oldFile -Leaf)..." -ForegroundColor Yellow
        Rename-Item -Path $targetExe -NewName (Split-Path $oldFile -Leaf) -Force -ErrorAction SilentlyContinue
    } else {
        try { Remove-Item $targetExe -Force -ErrorAction SilentlyContinue } catch {}
    }
}

# Copy files individually with safety checks
$sourceFiles = Get-ChildItem -Path $sourceDir -File
foreach ($file in $sourceFiles) {
    # Skip installer scripts from copying into target directory
    if ($file.Name -match "^Install-TaskView-Overlay\.") { continue }

    $destPath = Join-Path $targetDir $file.Name
    if (Test-Path $destPath) {
        if (-not (Test-FileExclusiveAccess $destPath)) {
            $timestamp = (Get-Date).ToString("yyyyMMdd_HHmmss")
            $oldDest = "$destPath.old_$timestamp"
            Rename-Item -Path $destPath -NewName (Split-Path $oldDest -Leaf) -Force -ErrorAction SilentlyContinue
        } else {
            try { Remove-Item $destPath -Force -ErrorAction SilentlyContinue } catch {}
        }
    }
    Copy-Item $file.FullName -Destination $destPath -Force
}

# Copy icons directory if present
if (Test-Path "$sourceDir\icons") {
    $targetIcons = Join-Path $targetDir "icons"
    if (-not (Test-Path $targetIcons)) { New-Item -ItemType Directory -Force -Path $targetIcons | Out-Null }
    Copy-Item "$sourceDir\icons\*" -Destination $targetIcons -Recurse -Force
}

# Clean up any old temporary files that are now unlocked
Get-ChildItem -Path $targetDir -Filter "*.old*" | ForEach-Object {
    try { Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue } catch {}
}
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
    # Remove HKCU Run if present to avoid duplicate launches
    Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "GestureDock" -ErrorAction SilentlyContinue
} catch { }

# Clean up any obsolete installer scripts in target directory to prevent confusion
Remove-Item "$targetDir\Install-TaskView-Overlay.*" -Force -ErrorAction SilentlyContinue

# Launch from Program Files with UIAccess
Start-Process "$targetDir\GestureDock.exe"

Write-Host ""
Write-Host "=========================================================" -ForegroundColor Green
Write-Host " SUCCESS! GestureDock is now running with UIAccess." -ForegroundColor Green
Write-Host " Swipe up with 3 fingers (or press Win+Tab) to test it!" -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Green
Write-Host "Press any key to close this window..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
