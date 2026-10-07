#!/bin/bash
#
# Builds WispUO into bin/dist:
#
#   bash scripts/build.sh [options for ClassicUO's build: --arch, --no-flatpak, ...]
#
#   1. scripts/prepare.sh makes work/ClassicUO and work/ClassicAssist
#   2. ClassicAssist goes into bin/dist/Data/Plugins/ClassicAssist
#   3. ClassicUO's build makes the client, the launcher and the plugin host
#      (and, on Linux, the Flatpak bundle, which then carries ClassicAssist)
#   4. the .NET runtime plugins need goes into bin/dist/bin/dotnet, so nothing has
#      to be installed (the Flatpak leaves it out: it has its own)
#
# On macOS, unless --arch picks one, both architectures are built and joined into
# universal binaries (lipo), with a .NET runtime for each in bin/dotnet-arm64 and
# bin/dotnet-x64: the runtime's own libraries are built for one architecture, so
# two of them sit side by side and the client takes its own.
#
# ClassicAssist is copied over what is there: profiles, macros and settings a
# player keeps in that folder stay.

set -euo pipefail

root_dir="$(cd -P -- "$(dirname -- "$0")/.." && pwd -P)"
dist_dir="$root_dir/bin/dist"

case "$(uname -s)" in
  MINGW* | CYGWIN* | MSYS*) os=windows ;;
  Darwin) os=macos ;;
  *) os=linux ;;
esac

case "$(uname -m)" in
  arm64 | aarch64) host_arch=arm64 ;;
  *) host_arch=x64 ;;
esac

# the architecture asked for with --arch, the machine's otherwise
arch="$host_arch"
arch_given=0
args=("$@")

for ((i = 0; i < ${#args[@]}; i++)); do
  if [[ "${args[i]}" == "--arch" && $((i + 1)) -lt ${#args[@]} ]]; then
    arch_given=1
    case "${args[i + 1]}" in
      arm64 | aarch64) arch=arm64 ;;
      *) arch=x64 ;;
    esac
  fi
done

universal=0
[[ $os == macos && $arch_given -eq 0 ]] && universal=1

if [[ $os == windows ]]; then
  # Visual Studio's installer folder holds vswhere, which NativeAOT calls
  vs_installer="/c/Program Files (x86)/Microsoft Visual Studio/Installer"
  [[ -d "$vs_installer" ]] && export PATH="$vs_installer:$PATH"
  [[ -x "/c/Program Files/dotnet/dotnet.exe" ]] && export PATH="/c/Program Files/dotnet:$PATH"
fi

bash "$root_dir/scripts/prepare.sh"

# --- ClassicAssist ---------------------------------------------------------
ca_dir="$root_dir/work/ClassicAssist"
echo "Building ClassicAssist"
dotnet build "$ca_dir/ClassicAssist.slnx" -c Release -nologo -v quiet

# Its own launcher and updater stay out: WispUO starts the client, and updates
# come with WispUO. framework/ is the Mono build, which ClassicUO no longer hosts.
ca_out="$dist_dir/Data/Plugins/ClassicAssist"
mkdir -p "$ca_out"
(cd "$ca_dir/Output/ClassicAssist" &&
  find . -mindepth 1 -maxdepth 1 ! -name 'ClassicAssist.Launcher*' ! -name 'ClassicAssist.Updater*' ! -name framework \
    -exec cp -a {} "$ca_out/" \;)
find "$ca_out" -name '*.pdb' -delete

# The build carries native libraries for every system .NET runs on, twice (the
# plugin and its window): only this platform's stay, both architectures on macOS.
keeps_rid() {
  case "$os:$1" in
    windows:win | "windows:win-$arch") return 0 ;;
    macos:osx | macos:osx-arm64 | macos:osx-x64 | macos:osx.*) return 0 ;;
    linux:linux | "linux:linux-$arch" | linux:unix) return 0 ;;
  esac
  return 1
}

for runtimes in "$ca_out/runtimes" "$ca_out/ui/runtimes"; do
  [[ -d "$runtimes" ]] || continue
  for rid_dir in "$runtimes"/*/; do
    keeps_rid "$(basename "$rid_dir")" || rm -rf "$rid_dir"
  done
done

# The window's apphost is a native program of the machine's architecture: a
# universal app needs the other one's too.
if [[ $universal -eq 1 ]]; then
  other_arch=x64
  [[ $host_arch == x64 ]] && other_arch=arm64

  ui_other="$root_dir/bin/ca-ui-$other_arch"
  rm -rf "$ui_other"
  dotnet build "$ca_dir/ClassicAssist.Avalonia/ClassicAssist.Avalonia.csproj" -c Release -nologo -v quiet \
    -r "osx-$other_arch" --self-contained false -o "$ui_other"

  lipo -create "$ca_out/ui/ClassicAssist.Avalonia" "$ui_other/ClassicAssist.Avalonia" \
    -output "$ca_out/ui/ClassicAssist.Avalonia.universal"
  mv -f "$ca_out/ui/ClassicAssist.Avalonia.universal" "$ca_out/ui/ClassicAssist.Avalonia"
fi
echo "ClassicAssist -> $ca_out"

# --- ClassicUO -------------------------------------------------------------
if [[ $universal -eq 0 ]]; then
  bash "$root_dir/work/ClassicUO/scripts/build.sh" --out "$dist_dir" "$@"
else
  for a in arm64 x64; do
    bash "$root_dir/work/ClassicUO/scripts/build.sh" --out "$root_dir/bin/dist-$a" --arch "$a" "$@"
  done

  # bin/dist gets the arm64 build, and each of its Mach-O files built for one
  # architecture gets the x64 slice; the libraries shipped universal stay as they are
  rm -rf "$dist_dir/bin"
  cp -a "$root_dir/bin/dist-arm64/bin" "$dist_dir/bin"
  cp -a "$root_dir/bin/dist-arm64/WispUO" "$dist_dir/WispUO"

  (cd "$root_dir/bin/dist-x64/bin" && find . -type f) | while IFS= read -r file; do
    a="$dist_dir/bin/$file"
    x="$root_dir/bin/dist-x64/bin/$file"

    if [[ ! -e "$a" ]]; then
      mkdir -p "$(dirname "$a")"
      cp -a "$x" "$a"
      continue
    fi

    archs_a="$(lipo -archs "$a" 2>/dev/null || true)"
    archs_x="$(lipo -archs "$x" 2>/dev/null || true)"

    if [[ -n "$archs_a" && -n "$archs_x" && "$archs_a" != "$archs_x" ]]; then
      lipo -create "$a" "$x" -output "$a.universal"
      mv -f "$a.universal" "$a"
    fi
  done

  echo "universal: $(lipo -archs "$dist_dir/bin/cuo")"
fi

# --- .NET runtime ----------------------------------------------------------
# The client looks in bin/dotnet-<arch> and bin/dotnet before anywhere else.
# Only what a framework-dependent app needs: hostfxr and Microsoft.NETCore.App,
# the newest 10.x installed. Another architecture's runtime is downloaded, at
# the same version, with Microsoft's dotnet-install script.
runtime="$(dotnet --list-runtimes | grep '^Microsoft.NETCore.App 10\.' | sort -V | tail -n 1)"

if [[ -z "$runtime" ]]; then
  echo "no .NET 10 runtime to ship: plugins will need one installed" >&2
  exit 0
fi

version="$(echo "$runtime" | cut -d' ' -f2)"
shared="$(echo "$runtime" | sed 's/.*\[\(.*\)\]/\1/')"
[[ $os == windows ]] && shared="$(cygpath -u "$shared")"
host_root="$(cd "$shared/../.." && pwd -P)"

# the root of a <version> runtime for <arch>: the installed one, or a download
runtime_root() {
  if [[ "$1" == "$host_arch" ]]; then
    echo "$host_root"
    return
  fi

  local dir="$root_dir/bin/dotnet-runtimes/$1-$version"

  if [[ ! -d "$dir/shared/Microsoft.NETCore.App/$version" ]]; then
    curl -sSL https://dot.net/v1/dotnet-install.sh -o "$root_dir/bin/dotnet-install.sh"
    bash "$root_dir/bin/dotnet-install.sh" --runtime dotnet --version "$version" \
      --architecture "$1" --install-dir "$dir" --no-path >&2
  fi

  echo "$dir"
}

# copy_runtime <arch> <destination>
copy_runtime() {
  local root dest="$2" fxr
  root="$(runtime_root "$1")"
  fxr="$(ls -d "$root"/host/fxr/10.* | sort -V | tail -n 1)"

  rm -rf "$dest"
  mkdir -p "$dest/host/fxr" "$dest/shared/Microsoft.NETCore.App"
  cp -a "$fxr" "$dest/host/fxr/"
  cp -a "$root/shared/Microsoft.NETCore.App/$version" "$dest/shared/Microsoft.NETCore.App/"
  for notice in LICENSE.txt ThirdPartyNotices.txt; do
    [[ -f "$root/$notice" ]] && cp "$root/$notice" "$dest/"
  done
  echo ".NET $version ($1) -> $dest"
}

if [[ $universal -eq 1 ]]; then
  rm -rf "$dist_dir/bin/dotnet"
  copy_runtime arm64 "$dist_dir/bin/dotnet-arm64"
  copy_runtime x64 "$dist_dir/bin/dotnet-x64"
else
  copy_runtime "$arch" "$dist_dir/bin/dotnet"
fi
