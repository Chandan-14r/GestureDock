using System;
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
