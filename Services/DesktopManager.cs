using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GestureDock.Services
{
    public class DesktopManager
    {
        [ComImport]
        [Guid("C08AFD90-F2A1-11D1-8455-00A0C91F3880")]
        [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IShellDispatch4
        {
            [DispId(0x60020000)]
            void ToggleDesktop();
            [DispId(0x60020001)]
            void MinimizeAll();
            [DispId(0x60020002)]
            void UndoMinimizeALL();
        }

        [ComImport]
        [Guid("13709620-C279-11CE-A49E-444553540000")]
        private class ShellApplication
        {
        }

        /// <summary>
        /// Executes the native Windows Show Desktop / Minimize All action.
        /// Uses Shell.Application COM interface directly for 100% native Windows behavior.
        /// </summary>
        public static void ShowDesktop()
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    shell.ToggleDesktop();
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"COM Shell.Application failed: {ex.Message}");
            }

            // Fallback: IShellDispatch4 MinimizeAll
            try
            {
                var shellObj = (IShellDispatch4)new ShellApplication();
                shellObj.ToggleDesktop();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IShellDispatch4 failed: {ex.Message}");
            }
        }
    }
}
