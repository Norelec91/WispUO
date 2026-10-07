#!/bin/bash
#
# Builds the source trees WispUO compiles: upstream with the distribution on top.
#
#   bash scripts/prepare.sh [--force] [classicuo|classicassist]
#
# work/ClassicUO is upstream ClassicUO at the commit the submodule pins, then,
# one commit each so the history shows what came from where:
#
#   1. the files in classicuo/remove.txt removed
#   2. classicuo/patches applied with git am
#   3. classicuo/overlay copied in
#
# work/ClassicAssist is made the same way from classicassist/.
#
# ClassicUO also moves FNA to the commit in classicuo/fna.commit, in a commit of
# its own before those.
#
# The tree is reused: a second run only resets it, and nothing happens when
# neither upstream nor the distribution changed since the last one (--force
# redoes it anyway). Build outputs (bin, obj) survive the reset.
#
# To change the distribution, work in work/ClassicUO or work/ClassicAssist,
# commit there, then run scripts/update-patches.sh to bring the changes back
# into patches and overlay.

set -euo pipefail

root_dir="$(cd -P -- "$(dirname -- "$0")/.." && pwd -P)"
source "$root_dir/scripts/stamp.sh"
force=0
components=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --force) force=1; shift ;;
    classicuo|classicassist) components+=("$1"); shift ;;
    *) echo "unknown option: $1" >&2; exit 1 ;;
  esac
done

[[ ${#components[@]} -gt 0 ]] || components=(classicuo classicassist)

prepare() {
  local component="$1" name="$2"
  local spec="$root_dir/$component"
  local src="$root_dir/upstream/$name"
  local dst="$root_dir/work/$name"

  if [[ ! -e "$src/.git" ]]; then
    git -C "$root_dir" submodule update --init "upstream/$name"
  fi

  local base
  base="$(git -C "$src" rev-parse HEAD)"

  # what the tree is made of (stamp.sh)
  local stamp
  stamp="$(spec_stamp "$root_dir" "$component" "$base")"

  if [[ $force -eq 0 && -f "$dst/.git/wispuo-stamp" && "$(cat "$dst/.git/wispuo-stamp")" == "$stamp" ]]; then
    echo "$name: up to date"
    return
  fi

  echo "$name: preparing from $(git -C "$src" log -1 --format='%h %s' "$base")"

  if [[ ! -d "$dst/.git" ]]; then
    mkdir -p "$root_dir/work"
    git clone -q --no-checkout "$src" "$dst"
  fi

  g() { git -C "$dst" -c core.autocrlf=false -c core.eol=lf "$@"; }

  g config user.name "WispUO prepare"
  g config user.email "prepare@wispuo.invalid"
  g am --abort >/dev/null 2>&1 || true
  g fetch -q "$src"
  g checkout -q -f --detach "$base"
  g reset -q --hard "$base"
  # ignored files (bin, obj) stay: builds remain incremental
  g clean -q -ffd

  if [[ -f "$spec/fna.commit" ]]; then
    g update-index --cacheinfo "160000,$(cat "$spec/fna.commit"),external/FNA"
  fi

  if [[ -f "$dst/.gitmodules" ]]; then
    g submodule sync -q --recursive
    g submodule update -q --init --recursive --force
  fi

  if [[ -f "$spec/fna.commit" ]]; then
    g commit -q -m "WispUO: FNA at $(cut -c1-9 "$spec/fna.commit")"
  fi

  if [[ -f "$spec/remove.txt" ]]; then
    while IFS= read -r path; do
      [[ -n "$path" ]] && g rm -q -r --ignore-unmatch -- "$path"
    done < "$spec/remove.txt"
    g commit -q -m "WispUO: remove what the distribution drops"
  fi

  shopt -s nullglob
  local patches=("$spec"/patches/*.patch)
  shopt -u nullglob

  if [[ ${#patches[@]} -gt 0 ]]; then
    if ! g am -q --3way --committer-date-is-author-date "${patches[@]}"; then
      echo "$name: a patch does not apply any more; resolve it in $dst (git am --continue)," >&2
      echo "then run scripts/update-patches.sh $component." >&2
      exit 1
    fi
  fi

  if [[ -d "$spec/overlay" ]]; then
    cp -a "$spec/overlay/." "$dst/"
  fi

  g add -A

  # executable bits as WispUO's index has them (Windows loses them on disk)
  while IFS= read -r path; do
    g update-index --chmod=+x -- "${path#"$component/overlay/"}"
  done < <(git -C "$root_dir" ls-files -s -- "$component/overlay" | awk -F'\t' '$1 ~ /^100755 / { print $2 }')

  g commit -q --allow-empty -m "WispUO: overlay"

  echo "$stamp" > "$dst/.git/wispuo-stamp"
  echo "$name: ready, $(g rev-list --count "$base..HEAD") commits on top of upstream"
}

for component in "${components[@]}"; do
  case "$component" in
    classicuo) prepare classicuo ClassicUO ;;
    classicassist) prepare classicassist ClassicAssist ;;
  esac
done
