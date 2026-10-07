// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

/// <summary>
///     Plugins ship their dependencies next to themselves, as they did under .NET
///     Framework. hostfxr loads this host into a load context of its own, which only
///     knows what the host's deps.json lists, so plugins go into that same context and
///     the folders of the loaded plugins are searched when something is not found:
///     managed assemblies, and native libraries in the folder or under
///     runtimes/&lt;rid&gt;/native. Sharing the context keeps cuoapi and the reflection
///     stubs of this host the same types for every plugin.
/// </summary>
static class PluginResolver
{
    private static readonly List<string> _directories = new List<string>();
    private static bool _hooked;

    public static AssemblyLoadContext Context { get; } =
        AssemblyLoadContext.GetLoadContext(typeof(PluginResolver).Assembly) ?? AssemblyLoadContext.Default;

    public static void AddDirectory(string directory)
    {
        if (string.IsNullOrEmpty(directory) || _directories.Contains(directory))
            return;

        _directories.Add(directory);

        if (_hooked)
            return;

        _hooked = true;
        Context.Resolving += ResolveManaged;
        Context.ResolvingUnmanagedDll += ResolveNative;

        // Some plugins load their real assembly into the default context themselves
        // (NeoUO's ClassicUO_Plugin.dll does). cuoapi must still be the host's own copy,
        // or its delegates are other types there and the hand-over fails with an
        // InvalidCastException. Registered before any plugin runs, so it is asked first.
        if (Context != AssemblyLoadContext.Default)
            AssemblyLoadContext.Default.Resolving += ResolveShared;
    }

    private static readonly Assembly CuoApi = typeof(CUO_API.OnGetUOFilePath).Assembly;

    private static Assembly ResolveShared(AssemblyLoadContext context, AssemblyName name)
    {
        if (!string.Equals(name.Name, CuoApi.GetName().Name, StringComparison.OrdinalIgnoreCase))
            return null;

        if (Trace) Console.WriteLine($"[plugin host] {name.Name} for the default context: the host's copy");
        return CuoApi;
    }

    // CUO_PLUGIN_TRACE=1 prints every lookup, for a plugin that does not start
    private static readonly bool Trace = Environment.GetEnvironmentVariable("CUO_PLUGIN_TRACE") == "1";

    private static Assembly ResolveManaged(AssemblyLoadContext context, AssemblyName name)
    {
        if (Trace) Console.WriteLine($"[plugin host] managed {name.FullName} in {context.Name}");
        foreach (var dir in _directories)
        {
            var path = Path.Combine(dir, name.Name + ".dll");

            if (File.Exists(path))
                return context.LoadFromAssemblyPath(path);
        }

        return null;
    }

    // The runtime's own RID first, then the portable one NuGet packages ship native
    // libraries under: a distribution-built .NET says fedora.44-x64, packages say linux-x64.
    private static readonly string[] RuntimeIdentifiers = BuildRuntimeIdentifiers();

    private static string[] BuildRuntimeIdentifiers()
    {
        string os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => "x64"
        };

        var rids = new List<string> { RuntimeInformation.RuntimeIdentifier, $"{os}-{arch}" };
        if (os != "win")
            rids.Add($"unix-{arch}");

        return rids.Distinct().ToArray();
    }

    private static IntPtr ResolveNative(Assembly assembly, string name)
    {
        if (Trace) Console.WriteLine($"[plugin host] native {name} for {assembly.GetName().Name} (rids {string.Join(", ", RuntimeIdentifiers)})");
        var fileNames = new List<string> { name };

        // "Mono.Unix" has a dot without having an extension: only a library suffix counts
        if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase) && !name.Contains(".so"))
        {
            if (OperatingSystem.IsWindows())
                fileNames.Add(name + ".dll");
            else if (OperatingSystem.IsMacOS())
                fileNames.AddRange(new[] { name + ".dylib", "lib" + name + ".dylib" });
            else
                fileNames.AddRange(new[] { name + ".so", "lib" + name + ".so" });
        }

        foreach (var dir in _directories)
        {
            var folders = new List<string> { dir };
            foreach (var rid in RuntimeIdentifiers)
                folders.Add(Path.Combine(dir, "runtimes", rid, "native"));

            foreach (var folder in folders)
            {
                foreach (var fileName in fileNames)
                {
                    var path = Path.Combine(folder, fileName);

                    if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
                        return handle;
                }
            }
        }

        return IntPtr.Zero;
    }
}
