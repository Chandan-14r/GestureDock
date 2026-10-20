# GestureDock UIAccess Installer
# Grants GestureDock the ability to float directly over Windows 11 Task View

$ErrorActionPreference = "Continue"

# Helper to stop running instances completely silently
function Stop-GestureDock {
    try { cmd.exe /c "schtasks.exe /end /tn GestureDock-AutoStart >nul 2>&1" } catch {}
    try { cmd.exe /c "taskkill.exe /F /IM GestureDock.exe /T >nul 2>&1" } catch {}
    try { Get-Process GestureDock -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue } catch {}
}

# Helper to test exclusive write access on a file
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

# Auto-elevate to Administrator if not already elevated
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Requesting Administrator privileges..." -ForegroundColor Yellow
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    Exit
}

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " GestureDock: Windows 11 Task View Overlay Setup" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

try {
    # 1. Stop any currently running GestureDock instance
    Write-Host "[1/6] Stopping active background instances..." -ForegroundColor Gray
    Stop-GestureDock

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ((Get-Process GestureDock -ErrorAction SilentlyContinue) -and ($stopwatch.Elapsed.TotalSeconds -lt 4)) {
        Start-Sleep -Milliseconds 200
    }
    Start-Sleep -Milliseconds 400
    Write-Host "       Background instances stopped." -ForegroundColor Green

    # 2. Create or load local self-signed Code Signing Certificate
    Write-Host "[2/6] Setting up local Code Signing Certificate for UIAccess..." -ForegroundColor Gray
    $certName = "GestureDock Local UIAccess"
    $cert = Get-ChildItem -Path Cert:\LocalMachine\Root -ErrorAction SilentlyContinue | Where-Object { $_.Subject -like "*$certName*" } | Select-Object -First 1

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
            Stop-GestureDock
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
    Get-ChildItem -Path $targetDir -Filter "*.old*" -ErrorAction SilentlyContinue | ForEach-Object {
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

    # 6. Configure Windows Auto-Start on System Boot/Logon (Triple-Redundant: Registry + Startup Folder + Task Scheduler)
    Write-Host "[6/6] Configuring automatic startup on Windows boot..." -ForegroundColor Gray

    # 6.1 Register in Registry Run (primary auto-start: works on battery, AC, and every reboot)
    try {
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "GestureDock" -Value "`"$targetDir\GestureDock.exe`"" -Force
        Write-Host "       HKCU Run registry entry registered." -ForegroundColor Green
    } catch { }

    # 6.2 Register in Windows Startup folder (shell:startup)
    try {
        $startupDir = [Environment]::GetFolderPath("Startup")
        if (Test-Path $startupDir) {
            $wsh = New-Object -ComObject WScript.Shell
            $sc = $wsh.CreateShortcut((Join-Path $startupDir "GestureDock.lnk"))
            $sc.TargetPath = "$targetDir\GestureDock.exe"
            $sc.WorkingDirectory = $targetDir
            $sc.Description = "GestureDock - Windows 11 Task View Companion"
            $sc.Save()
            Write-Host "       Windows Startup folder shortcut registered." -ForegroundColor Green
        }
    } catch { }

    # 6.3 Register in Task Scheduler with NO battery restrictions and NO elevation mismatch
    $taskName = "GestureDock-AutoStart"
    try {
        $userSid = ([System.Security.Principal.WindowsIdentity]::GetCurrent()).User.Value
        $taskXml = @"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Author>GestureDock</Author>
    <Description>GestureDock Windows 11 Task View Companion Utility</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <UserId>$userSid</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>4</Priority>
    <RestartOnFailure>
      <Interval>PT1M</Interval>
      <Count>3</Count>
    </RestartOnFailure>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>$targetDir\GestureDock.exe</Command>
    </Exec>
  </Actions>
</Task>
"@
        $xmlPath = "$env:TEMP\GestureDock-Task.xml"
        [System.IO.File]::WriteAllText($xmlPath, $taskXml, [System.Text.Encoding]::Unicode)
        cmd.exe /c "schtasks.exe /create /tn ""$taskName"" /xml ""$xmlPath"" /f >nul 2>&1"
        Remove-Item $xmlPath -Force -ErrorAction SilentlyContinue
        Write-Host "       Task Scheduler registered (runs on battery & AC, restarts on failure)." -ForegroundColor Green
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
} catch {
    Write-Host ""
    Write-Host "ERROR: $_" -ForegroundColor Red
    Write-Host "Installation could not complete." -ForegroundColor Red
}

Write-Host ""
Write-Host "Press any key to close this window..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
