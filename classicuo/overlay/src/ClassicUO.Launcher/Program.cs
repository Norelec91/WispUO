// SPDX-License-Identifier: BSD-2-Clause

using Avalonia;
using System;
using System.Runtime.InteropServices;

namespace ClassicUO.Launcher
{
    internal static class Program
    {
        /// <summary>
        ///     Set by the main window when the user presses Play: the client is started
        ///     only after Avalonia has shut down, so the two never share the process.
        /// </summary>
        public static bool PlayRequested;

        [STAThread]
        public static int Main(string[] args)
        {
            UseBinFolder();

            // Anything on the command line means a script or another launcher already
            // knows what to run: hand it to the client untouched, no window.
            if (args.Length > 0)
            {
                return ClientStarter.Start(args);
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            return PlayRequested ? ClientStarter.Start(Array.Empty<string>()) : 0;
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
        }

        /// <summary>
        ///     On Windows the launcher is WispUO.exe, alone at the top of the install with
        ///     settings.json and Data, and every library sits in bin: Skia, HarfBuzz and ANGLE
        ///     are looked up there before Avalonia loads them. Elsewhere the launcher lives in
        ///     bin itself, started by the WispUO script, and finds them next to itself.
        /// </summary>
        private static void UseBinFolder()
        {
            if (OperatingSystem.IsWindows() && ClientStarter.BinDirectory != AppContext.BaseDirectory)
            {
                SetDllDirectoryW(ClientStarter.BinDirectory);
            }
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectoryW(string path);
    }
}
