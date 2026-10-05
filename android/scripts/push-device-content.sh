#!/bin/sh
# Push the non-APK runtime content onto a connected device/emulator after
# installing the POC APK.
#
# The engine runtime database (global mix database.dat) is now packaged inside
# the APK and copied automatically by the app on first launch - it is NOT
# pushed here. What still must be pushed (all developer/repo content, no retail
# content inside Git):
#   - engine/glsl
#   - engine/mods/{common,common-content,ts,ts-content}
#   - mods/ra2, mods/ra2-content
# And, optionally, the developer's own retail RA2 files:
#   ./android/scripts/push-device-content.sh /path/to/retail/content/dir
set -eu
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# shellcheck source=common.sh
. "$SCRIPT_DIR/common.sh"

require_adb_device
POC_APP_FILES="/data/user/0/$POC_PACKAGE/files"
adb shell "run-as $POC_PACKAGE mkdir -p $POC_APP_FILES/openra/engine/mods $POC_APP_FILES/openra/mods $POC_APP_FILES/openra/Content/ra2"

TARBALL=$(mktemp -t openra-device-content.XXXXXX.tar.gz)
trap 'rm -f "$TARBALL"' EXIT

cd "$REPOSITORY_ROOT"
tar czf "$TARBALL" engine/glsl engine/mods/common engine/mods/common-content engine/mods/ts engine/mods/ts-content mods/ra2 mods/ra2-content
echo "Pushing engine runtime + RA2 mod content..."
# Extract directly under openra/. The runtime database creates openra/engine
# before this script runs, so moving a freshly extracted engine directory into
# it would incorrectly produce openra/engine/engine and hide the GLSL files.
cat "$TARBALL" | adb shell "run-as $POC_PACKAGE sh -c 'mkdir -p $POC_APP_FILES/openra && cd $POC_APP_FILES/openra && tar xzf -'"

RETAIL="${1:-}"
if [ -n "$RETAIL" ] && [ -d "$RETAIL" ]; then
    echo "Pushing retail content from: $RETAIL"
    tar czf "$TARBALL" -C "$RETAIL" .
    cat "$TARBALL" | adb shell "run-as $POC_PACKAGE sh -c 'cd $POC_APP_FILES/openra/Content/ra2 && tar xzf -'"
else
    echo "No retail content dir given (pass as first argument when available)."
fi
echo "Content push complete."
