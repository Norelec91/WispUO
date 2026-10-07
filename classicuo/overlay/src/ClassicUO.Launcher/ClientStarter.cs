// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ClassicUO.Launcher
{
    /// <summary>
    ///     Finds the client and starts it in the launcher's working directory, which is
    ///     where settings.json and the profiles live.
    /// </summary>
    internal static class ClientStarter
    {
        /// <summary>
        ///     The folder holding the client and every library (see Program.UseBinFolder):
        ///     bin next to the launcher on Windows, where the launcher is WispUO.exe at
        ///     the top; the launcher's own folder elsewhere, where it lives in bin itself.
        /// </summary>
        public static string BinDirectory
        {
            get
            {
                string bin = Path.Combine(AppContext.BaseDirectory, "bin");

                return Directory.Exists(bin) ? bin : AppContext.BaseDirectory;
            }
        }

        public static string FindClient()
        {
            string path = Path.Combine(BinDirectory, OperatingSystem.IsWindows() ? "cuo.exe" : "cuo");

            return File.Exists(path) ? path : null;
        }

        public static int Start(string[] args)
        {
            string client = FindClient();

            if (client == null)
            {
                Console.Error.WriteLine($"No ClassicUO client in {BinDirectory}");

                return 1;
            }

            if (!OperatingSystem.IsWindows())
            {
                // On macOS and Linux the client replaces the launcher in the same process:
                // a .app keeps its Dock icon, a terminal keeps waiting on the game, and the
                // Flatpak sandbox, which ends with its first process, stays up.
                Exec(client, args);
                Console.Error.WriteLine($"cannot exec {client}: errno {Marshal.GetLastPInvokeError()}");
            }

            ProcessStartInfo psi = new ProcessStartInfo(client)
            {
                UseShellExecute = false,
                WorkingDirectory = Environment.CurrentDirectory
            };

            foreach (string arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using Process process = Process.Start(psi);

            return process == null ? 1 : 0;
        }

        /// <summary>Only returns when execve fails.</summary>
        private static unsafe void Exec(string path, string[] args)
        {
            List<string> argv = new List<string> { path };
            argv.AddRange(args);

            // .NET keeps its own copy of the environment, so it is passed explicitly.
            List<string> envp = new List<string>();

            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                envp.Add($"{entry.Key}={entry.Value}");
            }

            IntPtr pPath = Marshal.StringToCoTaskMemUTF8(path);
            IntPtr* pArgv = ToNativeArray(argv);
            IntPtr* pEnvp = ToNativeArray(envp);

            if (OperatingSystem.IsMacOS())
            {
                SpawnInPlace(pPath, pArgv, pEnvp);
            }
            else
            {
                execve(pPath, pArgv, pEnvp);
            }

            // still here: execve failed, give the memory back before falling back
            FreeNativeArray(pArgv, argv.Count);
            FreeNativeArray(pEnvp, envp.Count);
            Marshal.FreeCoTaskMem(pPath);
        }

        /// <summary>
        ///     After execve on macOS the client's runtime crashed chaining its GC signal to
        ///     a null handler, most likely because the reset handlers keep the launcher's
        ///     SA_SIGINFO flag. posix_spawn with POSIX_SPAWN_SETEXEC replaces the
        ///     process the same way, and the kernel also puts every signal back to its
        ///     default and unblocks it. Only returns when that fails.
        /// </summary>
        private static unsafe void SpawnInPlace(IntPtr path, IntPtr* argv, IntPtr* envp)
        {
            const short POSIX_SPAWN_SETSIGDEF = 0x04;
            const short POSIX_SPAWN_SETSIGMASK = 0x08;
            const short POSIX_SPAWN_SETEXEC = 0x40;
            const int SIGKILL = 9, SIGSTOP = 17;

            // sigset_t is a 32-bit mask on macOS: every signal but the two that cannot
            // be changed goes back to its default, and none stays blocked
            uint defaults = 0;

            for (int signal = 1; signal < 32; signal++)
            {
                if (signal != SIGKILL && signal != SIGSTOP)
                {
                    defaults |= 1u << (signal - 1);
                }
            }

            uint blocked = 0;
            IntPtr attr = IntPtr.Zero;

            if (posix_spawnattr_init(&attr) != 0)
            {
                return;
            }

            posix_spawnattr_setflags(&attr, POSIX_SPAWN_SETEXEC | POSIX_SPAWN_SETSIGDEF | POSIX_SPAWN_SETSIGMASK);
            posix_spawnattr_setsigdefault(&attr, &defaults);
            posix_spawnattr_setsigmask(&attr, &blocked);

            int pid;
            int rc = posix_spawn(&pid, path, IntPtr.Zero, &attr, argv, envp);

            // still here: it failed
            posix_spawnattr_destroy(&attr);
            Marshal.SetLastPInvokeError(rc);
        }

        private static unsafe IntPtr* ToNativeArray(List<string> values)
        {
            IntPtr* array = (IntPtr*)NativeMemory.Alloc((nuint)(values.Count + 1), (nuint)sizeof(IntPtr));

            for (int i = 0; i < values.Count; i++)
            {
                array[i] = Marshal.StringToCoTaskMemUTF8(values[i]);
            }

            array[values.Count] = IntPtr.Zero;

            return array;
        }

        private static unsafe void FreeNativeArray(IntPtr* array, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Marshal.FreeCoTaskMem(array[i]);
            }

            NativeMemory.Free(array);
        }

        [DllImport("libc", SetLastError = true)]
        private static extern unsafe int execve(IntPtr path, IntPtr* argv, IntPtr* envp);

        // macOS only: posix_spawnattr_t is a pointer there, sigset_t a uint
        [DllImport("libc")]
        private static extern unsafe int posix_spawnattr_init(IntPtr* attr);

        [DllImport("libc")]
        private static extern unsafe int posix_spawnattr_destroy(IntPtr* attr);

        [DllImport("libc")]
        private static extern unsafe int posix_spawnattr_setflags(IntPtr* attr, short flags);

        [DllImport("libc")]
        private static extern unsafe int posix_spawnattr_setsigdefault(IntPtr* attr, uint* sigdefault);

        [DllImport("libc")]
        private static extern unsafe int posix_spawnattr_setsigmask(IntPtr* attr, uint* sigmask);

        [DllImport("libc")]
        private static extern unsafe int posix_spawn(int* pid, IntPtr path, IntPtr fileActions, IntPtr* attr, IntPtr* argv, IntPtr* envp);
    }
}
