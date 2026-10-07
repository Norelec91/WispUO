# Sourced by prepare.sh and update-patches.sh.
#
# What a work tree is made of: the upstream commit and every file of the
# component's spec as it is on disk (committed or not), with WispUO's executable
# bits. prepare.sh records it in work/<Component>/.git/wispuo-stamp;
# update-patches.sh refuses to export from a tree made of another spec, which
# would put back what changed since.
#
#   spec_stamp <root dir> <component> <upstream commit>
spec_stamp() {
  local root="$1" component="$2" base="$3"

  { echo "$base";
    (cd "$root" && find "$component" -type f | LC_ALL=C sort | tee /dev/stderr | git hash-object --stdin-paths) 2>&1;
    git -C "$root" ls-files -s -- "$component" | awk '$1 == "100755"'; } | git hash-object --stdin
}
