#!/bin/bash
#
# Wraps a build into WispUO.app.
#
#   bash scripts/build.sh                 # first, produces bin/dist
#   bash scripts/make-macos-app.sh        # then, wraps it
#
#   [--dist <dir>] [--portable] [--data-dir <path>] [--sign]
#   [--out <WispUO.app>] [--version <v>]
#
# This only packages: the architecture is decided when you build, by
# scripts/build.sh. Whatever bin/dist holds is what ends up in the bundle.
#
# The programs in bin/dist/bin go straight into Contents/MacOS: macOS counts an
# executable there as part of the app and gives it the app's name, icon and
# version (menu bar, About), which it does not for one in a subfolder. The client
# and the launcher find their native libraries next to themselves.
#
# User data lives outside the bundle by default: writing inside an .app breaks
# its signature, fails under App Translocation, and is not writable from
# /Applications. --portable puts it back inside.

set -euo pipefail

script_dir="$(cd -P -- "$(dirname -- "$0")" && pwd -P)"
root_dir="$(dirname "$script_dir")"

dist_dir="$root_dir/bin/dist"
app_path="$root_dir/bin/WispUO.app"
portable=0
data_dir_given=0
sign=0
version="1.1.0.0"

# Kept unexpanded on purpose: the launcher resolves $HOME at run time, so the
# bundle still works for whoever runs it.
data_expr='$HOME/Library/Application Support/WispUO'

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dist)       dist_dir="$2"; shift 2 ;;
    --portable)   portable=1; shift ;;
    --data-dir)   data_expr="$2"; data_dir_given=1; shift 2 ;;
    --sign)       sign=1; shift ;;
    --out)        app_path="$2"; shift 2 ;;
    --version)    version="$2"; shift 2 ;;
    *) echo "unknown option: $1" >&2; exit 1 ;;
  esac
done

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script only runs on macOS." >&2
  exit 1
fi

if [[ $portable -eq 1 && $data_dir_given -eq 1 ]]; then
  echo "--data-dir and --portable are mutually exclusive." >&2
  exit 1
fi

# --- what is in the build --------------------------------------------------
if [[ ! -d "$dist_dir" ]]; then
  echo "No build in $dist_dir. Run this first:" >&2
  echo "  bash scripts/build.sh" >&2
  exit 1
fi

entry_point="bin/cuo"

if [[ ! -f "$dist_dir/$entry_point" ]]; then
  echo "$dist_dir holds no macOS build: there is no cuo in there." >&2
  exit 1
fi

# The entry point is named the same on every platform, so a Linux bin/dist would
# get packaged into an .app that cannot start. Only Mach-O passes lipo.
if ! lipo -archs "$dist_dir/$entry_point" >/dev/null 2>&1; then
  echo "$dist_dir/$entry_point is not a Mach-O binary: that build is for another" >&2
  echo "platform. Build on macOS first:  bash scripts/build.sh" >&2
  exit 1
fi

# The bundle opens the launcher, which shows the shard list and then replaces
# itself with the entry point. A build without it starts the client directly.
start_exe="$entry_point"

if [[ -f "$dist_dir/bin/ClassicUOLauncher" ]]; then
  start_exe="bin/ClassicUOLauncher"
fi

# inside the bundle bin/ is gone: Contents/MacOS holds what it held
entry_in_app="${entry_point#bin/}"
start_in_app="${start_exe#bin/}"

# --- data directory --------------------------------------------------------
data_dir="$data_expr"

if [[ "$data_expr" == '$HOME'* ]]; then
  data_dir="$HOME${data_expr#\$HOME}"
fi

macos_dir="$app_path/Contents/MacOS"
resources_dir="$app_path/Contents/Resources"

# Rebuilding wipes the bundle, so rescue anything the user accumulated inside an
# earlier portable install before it goes.
if [[ $portable -eq 0 && -d "$macos_dir" ]]; then
  for item in settings.json launcher.json Data Logs; do
    if [[ -e "$macos_dir/$item" && ! -e "$data_dir/$item" ]]; then
      mkdir -p "$data_dir"
      cp -R "$macos_dir/$item" "$data_dir/"
      echo "Moved $item out of the bundle into $data_dir"
    fi
  done
fi

rm -rf "$app_path"
mkdir -p "$macos_dir" "$resources_dir"

# --- payload ---------------------------------------------------------------
# Debug symbols, the Linux hosts and any user data left in bin/dist by a test run
# have no business being shipped inside a bundle.
rsync -a \
  --exclude '*.pdb' \
  --exclude '*.dSYM' \
  --exclude 'settings.json' \
  --exclude 'launcher.json' \
  --exclude 'Data' \
  --exclude 'Logs' \
  --exclude 'WispUO' \
  --exclude 'ClassicUO' \
  "$dist_dir/bin"/ "$macos_dir"/

# The plugins built with WispUO (ClassicAssist) travel as a seed: they write into
# their own folder, so the launcher script copies them to the data directory, on
# first run and whenever the app brings other ones, as the Flatpak does. A
# portable bundle keeps them in place.
if [[ -d "$dist_dir/Data/Plugins" ]]; then
  if [[ $portable -eq 1 ]]; then
    mkdir -p "$macos_dir/Data"
    cp -R "$dist_dir/Data/Plugins" "$macos_dir/Data/"
  else
    mkdir -p "$resources_dir/seed/Data"
    cp -R "$dist_dir/Data/Plugins" "$resources_dir/seed/Data/"
    # which build these are: the launcher script copies them again when it changes
    date -u +%Y%m%d%H%M%S > "$resources_dir/seed/Data/Plugins/.wispuo-seed"
  fi
fi

# --- launcher --------------------------------------------------------------
# settings.json, launcher.json, Data/Profiles and Logs follow the working directory.
# Native libraries resolve from the executable's own directory, so the workdir is
# free to sit outside the bundle.
if [[ $portable -eq 1 ]]; then
  cat > "$macos_dir/WispUO" <<LAUNCHER
#!/bin/bash
here="\$(cd -P -- "\$(dirname -- "\$0")" && pwd -P)"
cd "\$here"
exec "\$here/$start_in_app" "\$@"
LAUNCHER
else
  cat > "$macos_dir/WispUO" <<LAUNCHER
#!/bin/bash
here="\$(cd -P -- "\$(dirname -- "\$0")" && pwd -P)"
data="$data_expr"

mkdir -p "\$data"

# first run after a portable install: bring the user's files along
for item in settings.json launcher.json Data Logs; do
	if [[ -e "\$here/\$item" && ! -e "\$data/\$item" ]]; then
		cp -R "\$here/\$item" "\$data/"
	fi
done

# the plugins that come with WispUO: copied on first run, and again over the old
# copy whenever the app brings other ones (the user's own files in there stay)
seed="\$here/../Resources/seed/Data/Plugins"
if [[ -d "\$seed" && "\$(cat "\$seed/.wispuo-seed" 2>/dev/null)" != "\$(cat "\$data/Data/Plugins/.wispuo-seed" 2>/dev/null)" ]]; then
	mkdir -p "\$data/Data/Plugins"
	cp -R "\$seed/." "\$data/Data/Plugins/"
fi

cd "\$data"
exec "\$here/$start_in_app" "\$@"
LAUNCHER
fi

chmod +x "$macos_dir/WispUO" "$macos_dir/$entry_in_app" "$macos_dir/$start_in_app"

# --- icon ------------------------------------------------------------------
icon_src="$root_dir/src/ClassicUO.Client/cuoicon.ico"

if [[ -f "$icon_src" ]]; then
  work="$(mktemp -d)"
  iconset="$work/WispUO.iconset"
  mkdir -p "$iconset"

  sips -s format png "$icon_src" --out "$work/icon.png" >/dev/null 2>&1

  for size in 16 32 64 128 256 512; do
    sips -z $size $size "$work/icon.png" --out "$iconset/icon_${size}x${size}.png" >/dev/null 2>&1
    sips -z $((size * 2)) $((size * 2)) "$work/icon.png" \
      --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null 2>&1
  done

  iconutil -c icns "$iconset" -o "$resources_dir/WispUO.icns"
  rm -rf "$work"
fi

# --- Info.plist ------------------------------------------------------------
cat > "$app_path/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleDevelopmentRegion</key>
	<string>en</string>
	<key>CFBundleExecutable</key>
	<string>WispUO</string>
	<key>CFBundleIconFile</key>
	<string>WispUO</string>
	<key>CFBundleIdentifier</key>
	<string>io.github.Norelec91.WispUO</string>
	<key>CFBundleInfoDictionaryVersion</key>
	<string>6.0</string>
	<key>CFBundleName</key>
	<string>WispUO</string>
	<key>CFBundlePackageType</key>
	<string>APPL</string>
	<key>CFBundleShortVersionString</key>
	<string>$version</string>
	<key>CFBundleVersion</key>
	<string>$version</string>
	<key>LSApplicationCategoryType</key>
	<string>public.app-category.role-playing-games</string>
	<key>LSMinimumSystemVersion</key>
	<string>11.0</string>
	<key>NSHighResolutionCapable</key>
	<true/>
	<key>NSSupportsAutomaticGraphicsSwitching</key>
	<true/>
</dict>
</plist>
PLIST

printf 'APPL????' > "$app_path/Contents/PkgInfo"

# --- signing ---------------------------------------------------------------
# Ad-hoc: every Mach-O in the bundle, found by content rather than by name, since
# Apple Silicon runs no unsigned code. The bundle itself is not sealed:
# codesign refuses one whose Contents/MacOS holds managed .dll and .json files,
# and without a Developer ID Gatekeeper blocks it the same either way.
if [[ $sign -eq 1 ]]; then
  find "$app_path" -type f -print0 | while IFS= read -r -d '' f; do
    if file -b "$f" | grep -q '^Mach-O'; then
      codesign --force --sign - "$f"
    fi
  done
fi

xattr -cr "$app_path" 2>/dev/null || true

arch="$(lipo -archs "$macos_dir/$entry_in_app" 2>/dev/null || echo unknown)"

echo "Bundle created ($arch): $app_path"

if [[ $portable -eq 1 ]]; then
  echo "Settings and profiles: inside the bundle, in Contents/MacOS"
else
  echo "Settings and profiles: $data_dir"
fi
