#!/bin/sh
# Install the Android POC APK on the attached device/emulator.
# Does NOT clear app data unless --clean-data is passed.
set -eu
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# shellcheck source=common.sh
. "$SCRIPT_DIR/common.sh"

require_adb_device
require_apk

CLEAN=""
for arg in "$@"; do
    case "$arg" in
        --clean-data) CLEAN=1 ;;
    esac
done

if [ -n "$CLEAN" ]; then
    echo "Clearing app data..."
    adb shell pm clear "$POC_PACKAGE" >/dev/null
fi

echo "Installing $POC_APK ..."
adb install -r "$POC_APK"
