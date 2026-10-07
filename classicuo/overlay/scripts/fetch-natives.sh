#!/bin/bash
#
# Downloads the FNA native libraries into external/, and records what it took.
#
#   bash scripts/fetch-natives.sh [--only <platform>] [--check]
#
#   platforms: windows-x64, linux-x64, linux-arm64, macos   (default: all)
#
# Upstream publishes these as fnalibs, built daily by FNA-XNA/fnalibs-dailies.
# Two caveats shape this script:
#
#   * they are GitHub Actions artifacts, not releases: the download needs a
#     token and the archives expire after 90 days, so there is no URL to pin.
#     That is why the libraries stay committed in external/ and this script is
#     the way to refresh them, not a step of the build;
#   * the archives do not cover everything in external/: zlib.dll on Windows is
#     the client's own dependency, so only the files fnalibs provides are
#     overwritten and the rest is left alone.
#
# The D3D12 folder is the Agility SDK, which SDL loads as a vendored
# D3D12Core.dll and which has to end up next to the executable.
#
# fnalibs tracks the development version of FNA. This repo pins FNA to a commit,
# so an update can outrun it: check that the client still runs afterwards.
#
# --check verifies what is on disk against external/NATIVES.md without
# downloading anything.

set -euo pipefail

script_dir="$(cd -P -- "$(dirname -- "$0")" && pwd -P)"
root_dir="$(dirname "$script_dir")"
external_dir="$root_dir/external"
manifest="$external_dir/NATIVES.md"

repo="FNA-XNA/fnalibs-dailies"
only=""
check_only=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --only)  only="$2"; shift 2 ;;
    --check) check_only=1; shift ;;
    *) echo "unknown option: $1" >&2; exit 1 ;;
  esac
done

# folder in the archive -> folder in external/
declare -a wanted

add_platform() {
  case "$1" in
    windows-x64) wanted+=("fnalibs:x64:x64" "fnalibs:D3D12:x64/D3D12") ;;
    linux-x64)   wanted+=("fnalibs:lib64:lib64") ;;
    linux-arm64) wanted+=("fnalibs:libaarch64:libaarch64") ;;
    macos)       wanted+=("fnalibs-apple:osx:osx") ;;
    *) echo "unknown platform: $1 (windows-x64, linux-x64, linux-arm64, macos)" >&2; exit 1 ;;
  esac
}

if [[ -n "$only" ]]; then
  add_platform "$only"
else
  for p in windows-x64 linux-x64 linux-arm64 macos; do add_platform "$p"; done
fi

sha_of() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | cut -d' ' -f1
  else
    shasum -a 256 "$1" | cut -d' ' -f1
  fi
}

# --- verify only -----------------------------------------------------------
if [[ $check_only -eq 1 ]]; then
  if [[ ! -f "$manifest" ]]; then
    echo "No $manifest: these libraries were never fetched by this script." >&2
    exit 1
  fi

  bad=0

  while read -r sha path; do
    # Only "<64 hex>  <path>" lines are entries: everything else is prose, and a
    # loose test lets words like "else:" or "for" through as if they were hashes.
    [[ "$sha" =~ ^[0-9a-f]{64}$ ]] || continue

    if [[ ! -f "$external_dir/$path" ]]; then
      echo "missing:  $path"
      bad=1
    elif [[ "$(sha_of "$external_dir/$path")" != "$sha" ]]; then
      echo "changed:  $path"
      bad=1
    fi
  done < "$manifest"

  if [[ $bad -eq 0 ]]; then
    echo "external/ matches $(basename "$manifest")."
  fi

  exit $bad
fi

# --- download --------------------------------------------------------------
# gh already carries a token; fall back to a plain one for CI without it.
if command -v gh >/dev/null 2>&1 && gh auth status >/dev/null 2>&1; then
  api() { gh api "$@"; }
  api_raw() { gh api "$1" > "$2"; }
elif [[ -n "${GH_TOKEN:-${GITHUB_TOKEN:-}}" ]]; then
  token="${GH_TOKEN:-$GITHUB_TOKEN}"
  api() { curl -fsSL -H "Authorization: Bearer $token" "https://api.github.com/$1"; }
  api_raw() { curl -fsSL -H "Authorization: Bearer $token" -o "$2" "https://api.github.com/$1"; }
else
  echo "These archives are GitHub Actions artifacts: downloading them needs a token." >&2
  echo "Either log in with 'gh auth login', or set GH_TOKEN." >&2
  exit 1
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

declare -A artifact_id
declare -A artifact_date

fetch_artifact() {
  local name="$1"

  [[ -f "$work/$name.zip" ]] && return

  local info
  info="$(api "repos/$repo/actions/artifacts?per_page=100" |
    python3 -c "
import json,sys
name = sys.argv[1]
alive = [a for a in json.load(sys.stdin)['artifacts'] if a['name'] == name and not a['expired']]
if not alive:
    sys.exit('no live artifact named ' + name)
a = max(alive, key=lambda a: a['created_at'])
print(a['id'], a['created_at'])
" "$name")"

  artifact_id[$name]="${info% *}"
  artifact_date[$name]="${info#* }"

  echo "Downloading $name (${artifact_date[$name]:0:10})"
  api_raw "repos/$repo/actions/artifacts/${artifact_id[$name]}/zip" "$work/$name.zip"

  mkdir -p "$work/$name"
  unzip -qo "$work/$name.zip" -d "$work/$name"
}

installed=()

for entry in "${wanted[@]}"; do
  IFS=: read -r archive src dest <<< "$entry"

  fetch_artifact "$archive"

  if [[ ! -d "$work/$archive/$src" ]]; then
    echo "$archive has no $src folder; upstream layout changed." >&2
    exit 1
  fi

  mkdir -p "$external_dir/$dest"

  for f in "$work/$archive/$src"/*; do
    name="$(basename "$f")"
    cp "$f" "$external_dir/$dest/$name"
    installed+=("$dest/$name")
    echo "  $dest/$name"
  done
done

# --- manifest --------------------------------------------------------------
sdl_version="$(curl -fsSL "https://raw.githubusercontent.com/$repo/main/VERSIONS" 2>/dev/null |
  python3 -c "
import sys
v = dict(l.strip().split('=') for l in sys.stdin if '=' in l)
print('3.%s.%s' % (v.get('SDL_MINOR_VERSION','?'), v.get('SDL_PATCH_VERSION','?')))
" || echo "unknown")"

# Merged with what is already there: a --only run must not drop the provenance of
# the platforms it did not touch.
{
  printf '%s\n' "${!artifact_id[@]}" | while read -r name; do
    echo "artifact|$name|${artifact_id[$name]}|${artifact_date[$name]:0:10}"
  done

  for path in "${installed[@]}"; do
    echo "file|$path|$(sha_of "$external_dir/$path")"
  done
} > "$work/updates"

python3 - "$manifest" "$work/updates" "$sdl_version" <<'PYTHON'
import re, sys

manifest, updates, sdl_version = sys.argv[1], sys.argv[2], sys.argv[3]

artifacts, files = {}, {}

try:
    with open(manifest) as fh:
        for line in fh:
            m = re.match(r"- (\S+): artifact (\d+), built (\S+)", line)
            if m:
                artifacts[m.group(1)] = (m.group(2), m.group(3))
            m = re.match(r"([0-9a-f]{64})  (\S+)", line)
            if m:
                files[m.group(2)] = m.group(1)
except FileNotFoundError:
    pass

with open(updates) as fh:
    for line in fh:
        kind, *rest = line.strip().split("|")
        if kind == "artifact":
            artifacts[rest[0]] = (rest[1], rest[2])
        elif kind == "file":
            files[rest[0]] = rest[1]

with open(manifest, "w") as fh:
    fh.write("# Native libraries\n\n")
    fh.write("Fetched from [FNA-XNA/fnalibs-dailies]"
             "(https://github.com/FNA-XNA/fnalibs-dailies) by "
             "`scripts/fetch-natives.sh`.\n")
    fh.write("SDL version: %s\n\n" % sdl_version)

    for name in sorted(artifacts):
        fh.write("- %s: artifact %s, built %s\n" % (name, *artifacts[name]))

    fh.write("\nAnything in external/ that is not listed below comes from somewhere\n"
             "else: zlib.dll on Windows is the client's own dependency, P/Invoked\n"
             "for UOP files.\n\n```\n")

    for path in sorted(files):
        fh.write("%s  %s\n" % (files[path], path))

    fh.write("```\n")
PYTHON

echo
echo "${#installed[@]} files updated, provenance in $(basename "$manifest")"
