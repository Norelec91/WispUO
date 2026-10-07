#!/bin/bash
#
# Turns the commits on top of an upstream tree into what WispUO keeps:
#
#   classicuo/patches/     one patch per commit, for the upstream files it changes
#   classicuo/overlay/     the files upstream does not have, and binaries
#   classicuo/remove.txt   upstream files the distribution drops
#   classicuo/fna.commit   the FNA commit, when it is not upstream's
#
# and the same in classicassist/ for ClassicAssist.
#
#   bash scripts/update-patches.sh [classicuo|classicassist]
#       from work/ClassicUO or work/ClassicAssist: commit your changes there first
#
#   bash scripts/update-patches.sh <component> --from <repo> --base <commit> [--head <ref>]
#       from any repository whose base commit has the same files as upstream
#
# prepare.sh does the opposite. Patches are applied with git am, so the commit
# messages are the patch descriptions.

set -euo pipefail

root_dir="$(cd -P -- "$(dirname -- "$0")/.." && pwd -P)"
source "$root_dir/scripts/stamp.sh"

component="${1:-}"
from=""
base=""
head="HEAD"

if [[ -z "$component" ]]; then
  for c in classicuo classicassist; do
    bash "$0" "$c"
  done
  exit 0
fi
shift

while [[ $# -gt 0 ]]; do
  case "$1" in
    --from) from="$2"; shift 2 ;;
    --base) base="$2"; shift 2 ;;
    --head) head="$2"; shift 2 ;;
    *) echo "unknown option: $1" >&2; exit 1 ;;
  esac
done

case "$component" in
  classicuo) upstream_name="ClassicUO" ;;
  classicassist) upstream_name="ClassicAssist" ;;
  *) echo "unknown component: $component (classicuo, classicassist)" >&2; exit 1 ;;
esac

stamp_file=""

if [[ -z "$from" ]]; then
  from="$root_dir/work/$upstream_name"
  base="$(git -C "$root_dir/upstream/$upstream_name" rev-parse HEAD)"
  stamp_file="$from/.git/wispuo-stamp"

  # A tree prepared from another spec would put back what changed since (stamp.sh).
  if [[ -f "$stamp_file" && "$(cat "$stamp_file")" != "$(spec_stamp "$root_dir" "$component" "$base")" ]]; then
    echo "$component/ changed since work/$upstream_name was prepared: run scripts/prepare.sh first," >&2
    echo "then redo the work there (exporting now would undo those changes)." >&2
    exit 1
  fi
fi

if [[ -z "$base" ]]; then
  echo "--from needs --base" >&2
  exit 1
fi

# No line ending conversion anywhere: what is exported is the repository's bytes.
g() { git -C "$from" -c core.autocrlf=false -c core.eol=lf "$@"; }

out="$root_dir/$component"
rm -rf "$out/patches" "$out/overlay" "$out/remove.txt" "$out/fna.commit"
mkdir -p "$out/patches" "$out/overlay"

patched=()
copied=()
removed=()

# --raw gives the modes, so submodules (160000) stand apart from files.
while IFS=$'\t' read -r meta path; do
  read -r _ new_mode _ new_sha status <<< "$meta"

  if [[ "$new_mode" == "160000" ]]; then
    if [[ "$path" == "external/FNA" ]]; then
      echo "$new_sha" > "$out/fna.commit"
    else
      echo "warning: submodule $path changed, not exported" >&2
    fi
    continue
  fi

  case "$status" in
    D) removed+=("$path") ;;
    A) copied+=("$path") ;;
    M)
      # binaries do not go through patches
      if [[ "$(g diff --numstat "$base" "$head" -- "$path" | cut -f1)" == "-" ]]; then
        copied+=("$path")
      else
        patched+=("$path")
      fi
      ;;
    *) echo "warning: $status $path not handled" >&2 ;;
  esac
done < <(g diff --raw --no-renames --no-abbrev "$base" "$head" | sed 's/^://')

if [[ ${#patched[@]} -gt 0 ]]; then
  # only the commits that touch upstream files make a patch; --no-numbered keeps
  # "n/total" out of the subjects, or a new patch would rewrite every other one
  g format-patch -q --no-renames --no-signature --zero-commit --no-numbered -o "$out/patches" \
    "$base..$head" -- "${patched[@]}"
fi

if [[ ${#copied[@]} -gt 0 ]]; then
  g archive --format=tar "$head" -- "${copied[@]}" | tar -x -C "$out/overlay"

  # Windows drops the executable bit on disk, so WispUO's index keeps it:
  # prepare.sh reads it back from there.
  git -C "$root_dir" add -- "$out/overlay"
  while IFS= read -r path; do
    git -C "$root_dir" update-index --chmod=+x -- "$out/overlay/$path"
  done < <(g ls-tree -r "$head" -- "${copied[@]}" | awk -F'\t' '$1 ~ /^100755 / { print $2 }')
fi

if [[ ${#removed[@]} -gt 0 ]]; then
  printf '%s\n' "${removed[@]}" > "$out/remove.txt"
fi

# the spec now is what the work tree holds
if [[ -n "$stamp_file" ]]; then
  spec_stamp "$root_dir" "$component" "$base" > "$stamp_file"
fi

echo "$component: ${#patched[@]} upstream files in $(ls "$out/patches" | wc -l) patches," \
     "${#copied[@]} overlay files, ${#removed[@]} removed$([[ -f "$out/fna.commit" ]] && echo ", FNA $(cut -c1-9 "$out/fna.commit")")"
