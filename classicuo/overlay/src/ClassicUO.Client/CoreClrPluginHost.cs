// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Utility.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ClassicUO
{
    /// <summary>
    ///     Starts the .NET runtime inside the NativeAOT client and loads ClassicUO.PluginHost
    ///     into it. The client cannot load managed assemblies itself, so managed plugins
    ///     run there, driven through the same <see cref="HostBindings"/> table the old
    ///     bootstrap used to hand over.
    /// </summary>
    internal static unsafe class CoreClrPluginHost
    {
        private const string HOST_ASSEMBLY = "ClassicUO.PluginHost";
        private const int HDT_LOAD_ASSEMBLY_AND_GET_FUNCTION_POINTER = 5;

        // the delegate_type_name value meaning "the method is [UnmanagedCallersOnly]"
        private static readonly IntPtr UNMANAGEDCALLERSONLY_METHOD = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct InitializeParameters
        {
            public nuint Size;
            public IntPtr HostPath;
            public IntPtr DotnetRoot;
        }

        /// <summary>Returns null, after logging why, when no runtime can be started.</summary>
        public static UnmanagedAssistantHost Create()
        {
            string hostAssembly = Path.Combine(AppContext.BaseDirectory, HOST_ASSEMBLY + ".dll");
            string runtimeConfig = Path.Combine(AppContext.BaseDirectory, HOST_ASSEMBLY + ".runtimeconfig.json");

            if (!File.Exists(hostAssembly) || !File.Exists(runtimeConfig))
            {
                Log.Error($"plugin host not found next to the client: {hostAssembly}");

                return null;
            }

            if (!TryFindHostFxr(out string dotnetRoot, out string hostFxrPath))
            {
                Log.Error("plugins need the .NET 10 runtime, and none was found (set DOTNET_ROOT if it is installed somewhere unusual)");

                return null;
            }

            Log.Trace($"starting .NET from {dotnetRoot}");

            // Processes a plugin starts (ClassicAssist's window) find the same runtime,
            // even when it is the one shipped next to the client.
            SetProcessEnvironment("DOTNET_ROOT", dotnetRoot);

            IntPtr lib = NativeLibrary.Load(hostFxrPath);

            var initialize = (delegate* unmanaged<IntPtr, InitializeParameters*, IntPtr*, int>)
                NativeLibrary.GetExport(lib, "hostfxr_initialize_for_runtime_config");
            var getDelegate = (delegate* unmanaged<IntPtr, int, IntPtr*, int>)
                NativeLibrary.GetExport(lib, "hostfxr_get_runtime_delegate");
            var close = (delegate* unmanaged<IntPtr, int>)
                NativeLibrary.GetExport(lib, "hostfxr_close");

            List<IntPtr> strings = new List<IntPtr>();
            IntPtr Str(string s)
            {
                IntPtr p = OperatingSystem.IsWindows() ? Marshal.StringToHGlobalUni(s) : Marshal.StringToCoTaskMemUTF8(s);
                strings.Add(p);

                return p;
            }

            IntPtr context = IntPtr.Zero;

            try
            {
                InitializeParameters parameters = new InitializeParameters
                {
                    Size = (nuint)sizeof(InitializeParameters),
                    HostPath = Str(Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "cuo.exe" : "cuo")),
                    DotnetRoot = Str(dotnetRoot)
                };

                int rc = initialize(Str(runtimeConfig), &parameters, &context);

                // 1 and 2 are success codes: the runtime was already up
                if (rc < 0 || context == IntPtr.Zero)
                {
                    Log.Error($"hostfxr_initialize_for_runtime_config failed: 0x{rc:X8}");

                    return null;
                }

                IntPtr loadPtr;
                rc = getDelegate(context, HDT_LOAD_ASSEMBLY_AND_GET_FUNCTION_POINTER, &loadPtr);

                if (rc < 0)
                {
                    Log.Error($"hostfxr_get_runtime_delegate failed: 0x{rc:X8}");

                    return null;
                }

                // The host lands in a load context of its own; it loads the plugins into
                // that same context, so they share cuoapi and its reflection stubs.
                var loadAssembly = (delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr*, int>)loadPtr;

                IntPtr setupPtr;
                rc = loadAssembly
                (
                    Str(hostAssembly),
                    Str($"Entry, {HOST_ASSEMBLY}"),
                    Str("Setup"),
                    UNMANAGEDCALLERSONLY_METHOD,
                    IntPtr.Zero,
                    &setupPtr
                );

                if (rc < 0)
                {
                    Log.Error($"cannot load {HOST_ASSEMBLY}: 0x{rc:X8}");

                    return null;
                }

                HostBindings bindings = default;
                ((delegate* unmanaged<HostBindings*, void>)setupPtr)(&bindings);

                Log.Trace("plugin host ready");

                return new UnmanagedAssistantHost(&bindings);
            }
            catch (Exception ex)
            {
                Log.Error($"cannot start the plugin host: {ex}");

                return null;
            }
            finally
            {
                // the context only matters until the delegates are out; the runtime stays loaded
                if (context != IntPtr.Zero)
                {
                    close(context);
                }

                foreach (IntPtr p in strings)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        Marshal.FreeHGlobal(p);
                    }
                    else
                    {
                        Marshal.FreeCoTaskMem(p);
                    }
                }
            }
        }

        /// <summary>
        ///     Sets a variable where the runtime started next, and the processes it starts,
        ///     will read it. On Unix NativeAOT keeps the environment in a copy of its own, so
        ///     there it goes through the C library.
        /// </summary>
        private static void SetProcessEnvironment(string name, string value)
        {
            if (OperatingSystem.IsWindows())
            {
                Environment.SetEnvironmentVariable(name, value);

                return;
            }

            IntPtr nameUtf8 = Marshal.StringToCoTaskMemUTF8(name);
            IntPtr valueUtf8 = Marshal.StringToCoTaskMemUTF8(value);

            try
            {
                IntPtr libc = NativeLibrary.Load(OperatingSystem.IsMacOS() ? "/usr/lib/libSystem.B.dylib" : "libc.so.6");
                var setenv = (delegate* unmanaged<IntPtr, IntPtr, int, int>)NativeLibrary.GetExport(libc, "setenv");

                setenv(nameUtf8, valueUtf8, 1);
            }
            catch (Exception ex)
            {
                Log.Warn($"cannot set {name}: {ex.Message}");
            }
            finally
            {
                Marshal.FreeCoTaskMem(nameUtf8);
                Marshal.FreeCoTaskMem(valueUtf8);
            }
        }

        /// <summary>
        ///     A runtime shipped next to the client comes first, then DOTNET_ROOT, then the
        ///     places the official installers use. A universal macOS build ships one per
        ///     architecture, dotnet-arm64 and dotnet-x64, and the client takes its own.
        /// </summary>
        private static bool TryFindHostFxr(out string dotnetRoot, out string hostFxrPath)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";

            IEnumerable<string> roots = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "dotnet-" + arch),
                Path.Combine(AppContext.BaseDirectory, "dotnet"),
                Environment.GetEnvironmentVariable("DOTNET_ROOT")
            };

            if (OperatingSystem.IsWindows())
            {
                roots = roots.Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"));
            }
            else if (OperatingSystem.IsMacOS())
            {
                roots = roots.Append("/usr/local/share/dotnet").Append("/opt/homebrew/opt/dotnet/libexec");
            }
            else
            {
                roots = roots.Append("/usr/share/dotnet").Append("/usr/lib/dotnet").Append("/usr/lib64/dotnet");
            }

            roots = roots.Append(Path.Combine(home, ".dotnet"));

            string libName = OperatingSystem.IsWindows() ? "hostfxr.dll" : OperatingSystem.IsMacOS() ? "libhostfxr.dylib" : "libhostfxr.so";

            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(Path.Combine(root, "host", "fxr")))
                {
                    continue;
                }

                string newest = Directory.GetDirectories(Path.Combine(root, "host", "fxr"))
                    .Where(d => File.Exists(Path.Combine(d, libName)))
                    .OrderByDescending(d => Version.TryParse(Path.GetFileName(d).Split('-')[0], out Version v) ? v : new Version())
                    .FirstOrDefault();

                if (newest != null)
                {
                    dotnetRoot = root;
                    hostFxrPath = Path.Combine(newest, libName);

                    return true;
                }
            }

            dotnetRoot = hostFxrPath = null;

            return false;
        }
    }
}
