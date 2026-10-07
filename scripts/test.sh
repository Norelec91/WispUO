#!/bin/bash
#
# Runs WispUO's tests:
#
#   bash scripts/test.sh [--roundtrip]
#
#   - ClassicUO's unit tests, with the distribution's own (tests/ClassicUO.UnitTests/Distro)
#   - the launcher's tests (tests/ClassicUO.Launcher.Tests)
#   - ClassicAssist's headless tests, with WindowKeeper's
#
# --roundtrip also checks that the distribution survives being assembled and exported
# again: prepare.sh --force then update-patches.sh must leave classicuo/ and
# classicassist/ as they are. Stage or commit them first, the check compares with git.

set -euo pipefail

root_dir="$(cd -P -- "$(dirname -- "$0")/.." && pwd -P)"
roundtrip=0

for arg in "$@"; do
  case "$arg" in
    --roundtrip) roundtrip=1 ;;
    *) echo "unknown option: $arg" >&2; exit 1 ;;
  esac
done

case "$(uname -s)" in
  MINGW* | CYGWIN* | MSYS*)
    [[ -x "/c/Program Files/dotnet/dotnet.exe" ]] && export PATH="/c/Program Files/dotnet:$PATH"
    ;;
esac

failed=()

run() {
  local name="$1"
  shift
  echo "== $name"
  if ! "$@"; then
    failed+=("$name")
  fi
}

if [[ $roundtrip -eq 1 ]]; then
  if ! git -C "$root_dir" diff --quiet -- classicuo classicassist; then
    echo "classicuo/ or classicassist/ has unstaged changes: stage them first" >&2
    exit 1
  fi

  bash "$root_dir/scripts/prepare.sh" --force
  bash "$root_dir/scripts/update-patches.sh"

  if ! git -C "$root_dir" diff --quiet -- classicuo classicassist; then
    git -C "$root_dir" diff --stat -- classicuo classicassist
    failed+=("roundtrip")
  fi
else
  bash "$root_dir/scripts/prepare.sh"
fi

cuo="$root_dir/work/ClassicUO"
ca="$root_dir/work/ClassicAssist"

run "ClassicUO" dotnet test "$cuo/tests/ClassicUO.UnitTests/ClassicUO.UnitTests.csproj" -nologo -v quiet
run "launcher" dotnet test "$cuo/tests/ClassicUO.Launcher.Tests/ClassicUO.Launcher.Tests.csproj" -nologo -v quiet
run "ClassicAssist" dotnet test "$ca/ClassicAssist.HeadlessTests/ClassicAssist.HeadlessTests.csproj" -nologo -v quiet

if [[ ${#failed[@]} -gt 0 ]]; then
  echo "FAILED: ${failed[*]}" >&2
  exit 1
fi

echo "all tests passed"
