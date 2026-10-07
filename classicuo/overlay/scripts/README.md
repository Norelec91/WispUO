# Build

## The command

    bash scripts/build.sh

Everywhere, and everywhere it produces the same thing: **`bin/dist`**, a folder
ready to run.

| System | What you launch |
|---|---|
| Windows (from Git Bash) | `WispUO.exe` |
| Linux, macOS | `./WispUO` |

Both open the launcher (`src/ClassicUO.Launcher`, Avalonia, NativeAOT): the
list of shards, each with its Ultima Online folder, client version and whether
it loads ClassicAssist. Play writes the choice into `settings.json` and starts
the client; the shard list itself is kept in `launcher.json`, next to it. The
account is typed on the client's login screen, which remembers it when asked;
the launcher keeps the last one saved for each shard and puts it back on Play.

The top of `bin/dist` only holds what a player deals with; the programs and
their libraries are in `bin`:

    WispUO(.exe)        the launcher on Windows, a script starting bin/ClassicUOLauncher elsewhere
    settings.json       written by the launcher, read by the client
    launcher.json       the shard list
    Data/               profiles, plugins, maps
    bin/                cuo, the launcher, the plugin host, SDL3, FNA3D, Skia...

Client and launcher both run in the top folder, which is where the client
looks for `settings.json` and `Data` (its working directory, as upstream). `bin`
holds no user data: the build empties and refills it every time.

Started with any argument the launcher shows no window and hands the arguments
to the client, so scripts and other launchers keep working unchanged. They can
also call the client directly, `bin/cuo.exe` on Windows and `bin/cuo`
elsewhere, from the top folder.

The client is a single NativeAOT executable, with no bootstrap and no Mono.
Managed plugins (CUO_API, such as ClassicAssist) run on the .NET 10 runtime,
which the client starts in its own process only when `settings.json` lists a
plugin — see below.

## The macOS bundle

A separate, optional step that packages whatever is in `bin/dist`:

    bash scripts/build.sh
    bash scripts/make-macos-app.sh      # → bin/WispUO.app

The architecture is decided by the build; the bundler only adds the launcher
script, the icon and `Info.plist`. By default user data lives outside the
bundle.

The architecture follows the machine everywhere, and everywhere `--arch` forces
it for cross-builds — that is how an ARM Mac produces the bundle for Intel ones.
A bundle built for the other architecture is native on the machine it is meant
for, not on the one that compiled it.

## The Flatpak bundle

On Linux `build.sh` also packs `bin/dist` into `bin/WispUO.flatpak` when
`flatpak-builder` is installed (`--no-flatpak` skips it); `make-flatpak.sh` does
the same step on its own. The first run downloads the freedesktop runtime, its
SDK and the .NET 10 extension.

    flatpak install --user bin/WispUO.flatpak

The bundle carries the binaries of the build and the .NET runtime plugins need.
Settings, the shard list and Data live in
`~/.var/app/io.github.Norelec91.WispUO/data`; ClassicAssist is copied there on
first run, and again over the old copy when an update brings another one (its
profiles and macros stay). The game files are read from the home folder. `--id`
or `CUO_FLATPAK_ID` builds the bundle under another application id.

This is for distributing the build yourself. It is not a Flathub manifest:
Flathub builds everything from source with no network access and does not
accept AI-assisted manifests.

## Prerequisites

- **.NET SDK 10** everywhere
- **Windows**: Visual Studio Build Tools with the C++ workload (NativeAOT uses
  the MSVC linker)
- **Linux**: `clang` and `zlib1g-dev`
- **macOS**: Xcode command line tools

To run on Linux the launcher needs the X11 session libraries `libICE` and
`libSM`, which desktop installs have and minimal ones (WSL, containers) may
not: `dnf install libICE libSM` or `apt install libice6 libsm6`.

## The variants

`build.sh` (and `build-naot.sh`, which it calls):

| flag | effect |
|---|---|
| `--arch x64` / `--arch arm64` | force the architecture instead of the machine's, for cross-builds. Windows is x64 only: nobody publishes the ARM64 natives |
| `--out`, `--version`, `--dev-build` | output directory, version stamped into the assemblies, DEV_BUILD constant. Used by the release workflow |
| `--no-flatpak` | on Linux, do not pack the build as a Flatpak bundle |

`make-macos-app.sh`:

| flag | effect |
|---|---|
| `--portable` | settings and profiles inside the bundle instead of `~/Library/Application Support/ClassicUO` |
| `--data-dir <path>` | a different data directory |
| `--sign` | ad-hoc signature of every binary in the bundle |
| `--dist <dir>`, `--out`, `--version` | build to package, bundle path, version in Info.plist |

## `fetch-natives.sh`

Updates the native libraries in `external/`, downloading them from
[fnalibs-dailies](https://github.com/FNA-XNA/fnalibs-dailies), the repository FNA
publishes them from:

    bash scripts/fetch-natives.sh                      # every platform
    bash scripts/fetch-natives.sh --only linux-arm64
    bash scripts/fetch-natives.sh --check              # verify without downloading

It is not part of the build: those libraries stay committed in `external/`
because upstream publishes them as GitHub Actions artifacts, which **expire
after 90 days** and need a token to download — there is no URL to pin. The script
is how you refresh them and how the repository records *what* was taken, in
`external/NATIVES.md` (SDL version, artifacts, and the sha256 of every file).

Needs `gh auth login` or `GH_TOKEN`. Careful: fnalibs tracks the development
version of FNA while this repository pins FNA to a tag, so after an update check
that the client still starts.

`zlib.dll` on Windows does not come from fnalibs — it is the client's own
dependency, P/Invoked for UOP files — so the script leaves it alone.

## Why the launcher is a separate executable

Avalonia and SDL do not share a process well: Avalonia cannot shut down to make
room for SDL, and on macOS both want the main thread and `NSApplication`. So the
launcher runs first, on its own. On macOS and Linux the client then replaces it
in the same process (the .app keeps its Dock icon, a terminal or the Flatpak
sandbox waits on it); on Windows the launcher starts it and exits. macOS goes
through `posix_spawn` with `POSIX_SPAWN_SETEXEC` rather than `execve`, so that
every signal starts from its default: after `execve` the client's runtime crashed
chaining its GC signal to a null handler, most likely because the launcher's
`SA_SIGINFO` flag survives the reset.

## How plugins run

The client is NativeAOT and cannot load managed assemblies. When `settings.json`
lists plugins, `CoreClrPluginHost.cs` finds `hostfxr` and starts the .NET runtime
inside the client process, then loads `ClassicUO.PluginHost.dll`, a plain IL
library published next to `cuo` in `bin`. The host fills in the same table of callbacks
(`HostBindings`) the old .NET Framework bootstrap used to hand over, so the
client side — `PluginHost.cs`, `Network/Plugin.cs` — is unchanged from upstream.

hostfxr gives the host a load context of its own; the host loads every plugin
into that same context, so they all share `cuoapi` and its reflection stubs,
resolves each plugin's dependencies from the plugin's folder, and calls
`Assistant.Engine.Install` as before. It also names itself the entry assembly,
which is how plugins such as Razor Enhanced find those stubs.

The runtime is looked for next to the client first (`dotnet/`), then in
`DOTNET_ROOT`, then where the official installers put it. The client then sets
`DOTNET_ROOT` to the one it found, so the processes a plugin starts (ClassicAssist
runs its window in one) use it too. Without a plugin the runtime is never
started, and nothing beyond the client is needed.
