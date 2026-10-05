#!/bin/sh
# Collect Android POC logs and a screenshot into artifacts/android-poc/.
set -eu
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# shellcheck source=common.sh
. "$SCRIPT_DIR/common.sh"

require_adb_device
mkdir -p "$POC_ARTIFACTS"
STAMP=$(date +%Y%m%d-%H%M%S)

adb logcat -d > "$POC_ARTIFACTS/logcat-$STAMP.log" 2>&1
adb exec-out screencap -p > "$POC_ARTIFACTS/screen-$STAMP.png" 2>/dev/null || true

# App-private engine logs are readable through run-as (debuggable build).
adb shell "run-as $POC_PACKAGE sh -c 'ls $POC_APP_FILES/openra/Logs' >/dev/null 2>&1" \
    && adb shell "run-as $POC_PACKAGE tar cf - -C $POC_APP_FILES/openra Logs" \
        | tar xf - -C "$POC_ARTIFACTS" 2>/dev/null || true

echo "Saved:"
echo "  $POC_ARTIFACTS/logcat-$STAMP.log"
echo "  $POC_ARTIFACTS/screen-$STAMP.png"
