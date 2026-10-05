#!/bin/sh
# Launch the Android POC app and stream its main log channels.
set -eu
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# shellcheck source=common.sh
. "$SCRIPT_DIR/common.sh"

require_adb_device

echo "Launching $POC_PACKAGE ..."
adb shell am force-stop "$POC_PACKAGE" || true
adb logcat -c || true
adb shell am start -n "$POC_PACKAGE/$POC_ACTIVITY"
echo "--- streaming logs (Ctrl-C to stop) ---"
adb logcat -s OpenRA.Android:V OpenRA.Host:V OpenRA.SdlPoc:V OpenRA.HostBridge:V DOTNET:V SDL:V
