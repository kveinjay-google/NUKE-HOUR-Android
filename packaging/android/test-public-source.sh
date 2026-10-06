#!/bin/sh
set -eu
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
cd "$ROOT"
. android/scripts/common.sh
mkdir -p artifacts/public-source-tests
python3 packaging/android/run_bounded.py --seconds 600 --log artifacts/public-source-tests/dotnet.log -- \
  "$DOTNET" test engine/OpenRA.Test/OpenRA.Test.csproj -c Release -m:4 -p:UseSharedCompilation=false \
  --filter 'AiDifficulty|BuildableTechExpansion|AndroidUpdatePolicy|ImportProgressEstimate|AndroidThermalFramePolicyTest|ReloadableSheetTest|DirectSheetPixelsTest|ChromePngDecoderTest|AndroidVideoPixelsTest|SecretLab|NeutralTech|BattleFortress|ProductionQueue|RuntimeResourceSampler|AndroidFailure|LoadMemoryPolicy|ClassicColorGridContainsExactlyNineTouchSwatchesWithoutCustomMixer' --nologo
for pattern in test_android_resource_release_gate.py test_interface_style_public_manifest.py test_nukehour_fixed_color_policy.py test_android_home_artwork_policy.py; do
  python3 -m unittest discover -s packaging/tests -p "$pattern"
done
