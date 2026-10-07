#!/bin/bash
#
# Packs a Linux bin/dist into a Flatpak bundle, for distributing it yourself:
#
#   bash scripts/build.sh               # first, on Linux (it calls this script too)
#   bash scripts/make-flatpak.sh        # -> bin/WispUO.flatpak
#
#   flatpak install --user bin/WispUO.flatpak
#   flatpak run io.github.Norelec91.WispUO
#
# Options:
#   --id <app id>            Flatpak application id (default below, or CUO_FLATPAK_ID)
#   --dist <dir>             the build to pack (default bin/dist)
#   --runtime <version>      org.freedesktop.Platform branch (default 25.08)
#
# This packs the binaries the build produced. It is not, and cannot become, a
# Flathub manifest: Flathub builds everything from source with no network, and
# does not accept AI-assisted manifests.
#
# Inside the sandbox the programs live in /app/lib/wispuo/bin; settings.json,
# launcher.json and Data go in the app's own data folder,
# ~/.var/app/io.github.Norelec91.WispUO/data.
# ClassicAssist, if bin/dist carries it, is copied there on first run, since it
# writes into its own folder. The .NET 10 runtime plugins need comes with the app.

set -euo pipefail

script_dir="$(cd -P -- "$(dirname -- "$0")" && pwd -P)"
root_dir="$(dirname "$script_dir")"

# the GitHub address reversed, github.com/Norelec91/WispUO
app_id="${CUO_FLATPAK_ID:-io.github.Norelec91.WispUO}"
runtime_version="25.08"
dist_dir="$root_dir/bin/dist"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --id) app_id="$2"; shift 2 ;;
    --dist) dist_dir="$(cd -P -- "$2" && pwd -P)"; shift 2 ;;
    --runtime) runtime_version="$2"; shift 2 ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

# Next to the build it packs: bin/flatpak and bin/WispUO.flatpak for bin/dist.
work_dir="$(dirname "$dist_dir")/flatpak"
bundle="$(dirname "$dist_dir")/WispUO.flatpak"

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "Flatpak bundles are made on Linux." >&2
  exit 1
fi

if ! command -v flatpak-builder >/dev/null 2>&1; then
  echo "flatpak-builder is not installed (dnf install flatpak-builder / apt install flatpak-builder)." >&2
  exit 1
fi

# Only a Linux build can go in: the entry point has the same name everywhere.
if [[ ! -f "$dist_dir/bin/cuo" || "$(head -c 4 "$dist_dir/bin/cuo" | od -An -c | tr -d ' ')" != "177ELF" ]]; then
  echo "$dist_dir holds no Linux build: run bash scripts/build.sh on Linux first." >&2
  exit 1
fi

stage="$work_dir/stage"
rm -rf "$stage"
mkdir -p "$stage"

# --- payload ---------------------------------------------------------------
cp -a "$dist_dir/bin" "$stage/bin"
# the .NET runtime a build ships for itself: the app has the SDK extension's
rm -rf "$stage/bin/dotnet"

# Plugins shipped with the build, without what a test run left in them.
if [[ -d "$dist_dir/Data/Plugins" ]]; then
  mkdir -p "$stage/seed/Data"
  cp -a "$dist_dir/Data/Plugins" "$stage/seed/Data/"
  find "$stage/seed/Data/Plugins" -mindepth 2 -maxdepth 2 -type d \
    \( -name Profiles -o -name Backup \) -exec rm -rf {} +
  # which build these are: the wrapper copies them again when it changes
  date -u +%Y%m%d%H%M%S > "$stage/seed/Data/Plugins/.wispuo-seed"
fi

# The 256x256 frame of the client's icon is a PNG already.
if command -v python3 >/dev/null 2>&1; then
  python3 - "$root_dir/src/ClassicUO.Client/cuoicon.ico" "$stage/icon.png" <<'PY' || true
import struct, sys
data = open(sys.argv[1], "rb").read()
for i in range(struct.unpack("<H", data[4:6])[0]):
    entry = data[6 + i * 16: 22 + i * 16]
    size, offset = struct.unpack("<II", entry[8:16])
    if (entry[0] or 256) == 256 and data[offset:offset + 4] == b"\x89PNG":
        open(sys.argv[2], "wb").write(data[offset:offset + size])
        break
PY
fi

cat > "$stage/wispuo" <<'LAUNCH'
#!/bin/sh
# settings.json, launcher.json and Data follow the working directory: the app's
# own data folder, the one place the sandbox lets it keep files.
data="${XDG_DATA_HOME:?}"
mkdir -p "$data"
cd "$data" || exit 1

# Plugins write into their own folder, so they run from a copy, made on first run
# and again over the old one whenever the app brings other plugins (the user's own
# files in there stay).
seed=/app/share/wispuo/seed/Data/Plugins
if [ -d "$seed" ] && [ "$(cat "$seed/.wispuo-seed" 2>/dev/null)" != "$(cat "$data/Data/Plugins/.wispuo-seed" 2>/dev/null)" ]; then
  mkdir -p "$data/Data/Plugins"
  cp -r "$seed/." "$data/Data/Plugins/"
fi

# The game on X11 like ClassicAssist, whose Avalonia 11 has no Wayland: with the
# game on Wayland and the assistant on XWayland, WSLg delivered no clicks to the
# assistant's window. SDL_VIDEO_DRIVER=wayland in the environment still wins.
export SDL_VIDEO_DRIVER="${SDL_VIDEO_DRIVER:-x11}"

export DOTNET_ROOT=/app/lib/dotnet
exec /app/lib/wispuo/bin/ClassicUOLauncher "$@"
LAUNCH

cat > "$stage/$app_id.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=WispUO
Comment=Ultima Online shards with ClassicUO
Exec=wispuo
Icon=$app_id
Terminal=false
Categories=Game;RolePlaying;
DESKTOP

cat > "$stage/$app_id.metainfo.xml" <<METAINFO
<?xml version="1.0" encoding="UTF-8"?>
<component type="desktop-application">
  <id>$app_id</id>
  <name>WispUO</name>
  <summary>Ultima Online shards with ClassicUO</summary>
  <metadata_license>CC0-1.0</metadata_license>
  <project_license>BSD-2-Clause</project_license>
  <description>
    <p>A distribution of ClassicUO, the open source Ultima Online client, with
    a launcher holding the list of shards and, optionally,
    the ClassicAssist assistant. The Ultima Online game files are not
    included: point each shard at your own copy.</p>
  </description>
  <launchable type="desktop-id">$app_id.desktop</launchable>
  <content_rating type="oars-1.1">
    <content_attribute id="violence-fantasy">mild</content_attribute>
    <content_attribute id="social-chat">intense</content_attribute>
  </content_rating>
</component>
METAINFO

# --- manifest --------------------------------------------------------------
# x11 for the launcher (Avalonia talks X11 only), wayland for the game window,
# pulseaudio and dri for sound and the GPU. The game files are usually somewhere
# in the home folder, read only.
manifest="$work_dir/$app_id.yml"

cat > "$manifest" <<MANIFEST
app-id: $app_id
runtime: org.freedesktop.Platform
runtime-version: '$runtime_version'
sdk: org.freedesktop.Sdk
sdk-extensions:
  - org.freedesktop.Sdk.Extension.dotnet10
command: wispuo
finish-args:
  - --share=network
  - --share=ipc
  - --socket=x11
  - --socket=wayland
  - --socket=pulseaudio
  - --device=dri
  - --filesystem=home:ro
modules:
  - name: dotnet-runtime
    buildsystem: simple
    build-commands:
      - /usr/lib/sdk/dotnet10/bin/install.sh
  - name: wispuo
    buildsystem: simple
    sources:
      - type: dir
        path: stage
    build-commands:
      - install -d /app/lib/wispuo
      - cp -a bin /app/lib/wispuo/
      - if [ -d seed ]; then install -d /app/share/wispuo && cp -a seed /app/share/wispuo/; fi
      - install -Dm755 wispuo /app/bin/wispuo
      - install -Dm644 $app_id.desktop /app/share/applications/$app_id.desktop
      - install -Dm644 $app_id.metainfo.xml /app/share/metainfo/$app_id.metainfo.xml
      - if [ -f icon.png ]; then install -Dm644 icon.png /app/share/icons/hicolor/256x256/apps/$app_id.png; fi
MANIFEST

# --- build -----------------------------------------------------------------
# The first build downloads the runtime, the SDK and the .NET extension.
flatpak-builder --user --install-deps-from=flathub --force-clean --disable-rofiles-fuse \
  --repo="$work_dir/repo" "$work_dir/build" "$manifest"

flatpak build-bundle --runtime-repo=https://dl.flathub.org/repo/flathub.flatpakrepo \
  "$work_dir/repo" "$bundle" "$app_id"

echo "Flatpak bundle: $bundle ($app_id)"
