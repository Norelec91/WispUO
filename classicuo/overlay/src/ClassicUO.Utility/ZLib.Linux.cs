// SPDX-License-Identifier: BSD-2-Clause

// What this distribution changes in how zlib is found, kept here so ZLib.cs only
// carries the call into it and merging upstream's file stays easy.

using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ClassicUO.Utility
{
    public static partial class ZLib
    {
        /// <summary>
        ///     [DllImport("libz")] makes Linux look for libz.so, which only the zlib
        ///     development package installs: a player without it crashed loading the UO
        ///     files. Every system has libz.so.1, the library itself, so "libz" goes there.
        ///     macOS has libz.dylib, which "libz" already finds.
        /// </summary>
        private static void UseVersionedLibzOnLinux()
        {
            if (!OperatingSystem.IsLinux())
            {
                return;
            }

            NativeLibrary.SetDllImportResolver(typeof(ZLib).Assembly, ResolveLibz);
        }

        private static IntPtr ResolveLibz(string name, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (name != "libz")
            {
                return IntPtr.Zero;
            }

            // libz.so.1 first, then whatever the default lookup would have found
            return NativeLibrary.TryLoad("libz.so.1", out IntPtr handle) ? handle : IntPtr.Zero;
        }
    }
}
