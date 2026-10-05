#!/bin/sh
# Android POC driver: build | install | run | logs | all
# Usage:
#   ./android/scripts/android-poc.sh build
#   ./android/scripts/android-poc.sh install [--clean-data]
#   ./android/scripts/android-poc.sh run
#   ./android/scripts/android-poc.sh logs
#   ./android/scripts/android-poc.sh all
set -eu
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

COMMAND="${1:-}"
case "$COMMAND" in
    build)
        sh "$SCRIPT_DIR/build-android-poc.sh"
        ;;
    install)
        shift
        sh "$SCRIPT_DIR/install-android-poc.sh" "$@"
        ;;
    run)
        sh "$SCRIPT_DIR/run-android-poc.sh"
        ;;
    logs)
        sh "$SCRIPT_DIR/collect-android-logs.sh"
        ;;
    all)
        sh "$SCRIPT_DIR/build-android-poc.sh"
        sh "$SCRIPT_DIR/install-android-poc.sh"
        sh "$SCRIPT_DIR/run-android-poc.sh" &
        RUNNER=$!
        sleep 20
        sh "$SCRIPT_DIR/collect-android-logs.sh"
        kill "$RUNNER" 2>/dev/null || true
        ;;
    *)
        echo "Usage: $0 {build|install|run|logs|all}" >&2
        exit 2
        ;;
esac
