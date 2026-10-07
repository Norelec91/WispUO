#!/bin/bash
#
# Builds ClassicUO for the machine it runs on.
#
#   bash scripts/build.sh [options]
#
# Just a dispatcher, so there is one command to remember. Every platform gets the
# same kind of output, bin/dist, and every option goes through untouched.
#
# Packaging is a separate step on purpose: scripts/make-macos-app.sh turns the
# build into a bin/WispUO.app.
#
# See scripts/README.md for the options.

set -euo pipefail

script_dir="$(cd -P -- "$(dirname -- "$0")" && pwd -P)"

case "$(uname -s)" in
  Darwin | Linux | MINGW* | CYGWIN* | MSYS*)
    exec bash "$script_dir/build-naot.sh" "$@"
    ;;
  *)
    echo "Unsupported platform: $(uname -s)" >&2
    exit 1
    ;;
esac
