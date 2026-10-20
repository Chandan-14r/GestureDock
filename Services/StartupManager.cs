using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace GestureDock.Services
{
    public class StartupManager
    {
        private const string AppName = "GestureDock";
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }

        public static void EnsureStartupRegistered()
        {
            try
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return;

                // 1. Registry Run Key (Runs on user login, battery, and AC power)
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (key != null)
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }

                // 2. Windows Startup Folder (shell:startup)
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (!string.IsNullOrEmpty(startupFolder) && Directory.Exists(startupFolder))
                {
                    string shortcutPath = Path.Combine(startupFolder, "GestureDock.lnk");
                    CreateShortcut(shortcutPath, exePath, "", "GestureDock Windows 11 Task View Companion Utility");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to ensure startup: {ex.Message}");
            }
        }

        public static void SetStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return;

                if (enable)
                {
                    string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\" --minimized");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set startup registry key: {ex.Message}");
            }
        }

        public static void RemoveDesktopShortcuts()
        {
            try
            {
                var desktopDirs = new List<string>();

                string standardDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!string.IsNullOrEmpty(standardDesktop) && Directory.Exists(standardDesktop))
                {
                    desktopDirs.Add(standardDesktop);
                }

                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProfile))
                {
                    string directDesktop = Path.Combine(userProfile, "Desktop");
                    if (Directory.Exists(directDesktop) && !desktopDirs.Contains(directDesktop))
                        desktopDirs.Add(directDesktop);

                    string oneDriveDesktop = Path.Combine(userProfile, "OneDrive", "Desktop");
                    if (Directory.Exists(oneDriveDesktop) && !desktopDirs.Contains(oneDriveDesktop))
                        desktopDirs.Add(oneDriveDesktop);
                }

                string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                if (!string.IsNullOrEmpty(commonDesktop) && Directory.Exists(commonDesktop) && !desktopDirs.Contains(commonDesktop))
                {
                    desktopDirs.Add(commonDesktop);
                }

                string[] shortcutNames = new[]
                {
                    "GestureDock.lnk",
                    "GestureDock Home.lnk",
                    "GestureDock Close All.lnk",
                    "GestureDock Clear All.lnk"
                };

                foreach (var dir in desktopDirs)
                {
                    foreach (var name in shortcutNames)
                    {
                        string fullPath = Path.Combine(dir, name);
                        if (File.Exists(fullPath))
                        {
                            try
                            {
                                File.Delete(fullPath);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error removing shortcuts: {ex.Message}");
            }
        }

        public static void CreateDesktopShortcuts()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                string exeDir = Path.GetDirectoryName(exePath) ?? "";
                string iconsDir = Path.Combine(exeDir, "icons");

                CreateShortcut(
                    Path.Combine(desktopPath, "GestureDock.lnk"),
                    exePath,
                    "",
                    "GestureDock - Windows 11 Task View Companion Utility",
                    Path.Combine(iconsDir, "GestureDock.ico"));

                CreateShortcut(
                    Path.Combine(desktopPath, "GestureDock Home.lnk"),
                    exePath,
                    "--action home",
                    "GestureDock - Show Desktop / Minimize All",
                    Path.Combine(iconsDir, "Home.ico"));

                CreateShortcut(
                    Path.Combine(desktopPath, "GestureDock Close All.lnk"),
                    exePath,
                    "--action closeall",
                    "GestureDock - Select & Close User Applications",
                    Path.Combine(iconsDir, "CloseAll.ico"));

                CreateShortcut(
                    Path.Combine(desktopPath, "GestureDock Clear All.lnk"),
                    exePath,
                    "--action clearall",
                    "GestureDock - Instantly Clear All Open Applications",
                    Path.Combine(iconsDir, "ClearAll.ico"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating shortcuts: {ex.Message}");
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string description, string? iconPath = null)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.Arguments = arguments;
                shortcut.Description = description;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath) ?? "";
                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                {
                    shortcut.IconLocation = $"{iconPath},0";
                }
                shortcut.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write shortcut {shortcutPath}: {ex.Message}");
            }
        }
    }
}
