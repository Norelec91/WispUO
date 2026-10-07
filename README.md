# WispUO

A distribution of [ClassicUO](https://github.com/ClassicUO/ClassicUO), the open
source Ultima Online client, with a launcher holding the list of shards and
the [ClassicAssist](https://github.com/Reetus/ClassicAssist.Avalonia) assistant,
on Windows, Linux and macOS. The Ultima Online game files are not included:
each shard points at your own copy.

WispUO is not ClassicUO and not ClassicAssist: bugs found here belong here.

## Building

    git clone --recursive https://github.com/Norelec91/WispUO.git
    bash scripts/build.sh

The result is `bin/dist`: `WispUO.exe` (Windows) or `./WispUO` (Linux, macOS)
opens the launcher; `bin/dist` carries the .NET runtime ClassicAssist needs. On
macOS the build is universal, Apple Silicon and Intel, unless `--arch` picks one. On
Linux the build also makes `bin/WispUO.flatpak`, which uses its own runtime, when
`flatpak-builder` is installed. Options are passed to ClassicUO's build (`--arch`,
`--no-flatpak`, ...); its prerequisites are in `classicuo/overlay/scripts/README.md`
(the .NET 10 SDK, and the C++ tools NativeAOT links with).

## How it is made

Upstream stays untouched, in two submodules pinned to a commit:

    upstream/ClassicUO        github.com/ClassicUO/ClassicUO
    upstream/ClassicAssist    github.com/Reetus/ClassicAssist.Avalonia

and, for each, what the distribution adds:

    classicuo/patches/        one patch per change to an upstream file
    classicuo/overlay/        files upstream does not have, and the native libraries
    classicuo/remove.txt      upstream files the distribution drops
    classicuo/fna.commit      the FNA commit the client builds with
    classicassist/            the same for ClassicAssist (nothing yet)

`scripts/prepare.sh` puts them together in `work/ClassicUO` and
`work/ClassicAssist`: upstream at the pinned commit, the removals, the patches
as commits, the overlay. `scripts/build.sh` prepares, builds ClassicAssist into
`bin/dist/Data/Plugins/ClassicAssist` and builds ClassicUO into `bin/dist`.

Changes to upstream files are kept to a few lines: the code lives in files of
the distribution's own (`*.Distro.cs`, or named after the feature) and upstream's
files only call into it, so new upstream versions merge easily.

## Changing it

Work in `work/ClassicUO` or `work/ClassicAssist`, commit there, then

    bash scripts/update-patches.sh

brings the changes back: each commit touching upstream files becomes a patch,
new files go to the overlay.

To follow upstream, move the submodule to a newer commit and run
`scripts/prepare.sh`: if a patch no longer applies, resolve it in `work/`
(`git am --continue`) and run `scripts/update-patches.sh`.

## Tests

    bash scripts/test.sh [--roundtrip]

runs ClassicUO's unit tests with the distribution's own (in
`tests/ClassicUO.UnitTests/Distro`), the launcher's (`tests/ClassicUO.Launcher.Tests`)
and ClassicAssist's headless tests. `--roundtrip` also checks that assembling and
exporting the distribution again changes nothing in `classicuo/` and
`classicassist/`.

## Licenses

ClassicUO is under the BSD 2-Clause license, ClassicAssist under the GNU GPL
version 3; the patches and overlays follow the license of the component they
change. A build of WispUO carries ClassicAssist, so whoever distributes it
must make its source available: this repository, with its submodules, is that
source.
