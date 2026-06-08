#!/usr/bin/env bash
# Build and deploy via dotnet -t:Install, which handles both assembly push
# (Fast Deployment) and APK install in one step — no manual adb install needed.
# Use this when EmbedAssembliesIntoApk is NOT set (default Debug behaviour).
#
# Usage:
#   ./deploy-android-install.sh        # build + install + launch (Debug)
#   CONFIG=Release ./deploy-android-install.sh
set -euo pipefail

export ANDROID_HOME="${ANDROID_HOME:-$HOME/Android/Sdk}"
export ANDROID_SDK_ROOT="${ANDROID_SDK_ROOT:-$ANDROID_HOME}"
export PATH="$PATH:$ANDROID_HOME/platform-tools"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ="$ROOT/src/MobileApp.Android/MobileApp.Android.csproj"
CONFIG="${CONFIG:-Debug}"
APPID="com.AntonioValentini.TrueMobile"

echo "==> Checking for a connected device..."
adb start-server >/dev/null 2>&1 || true
if ! adb devices | awk 'NR>1 && $2=="device"{found=1} END{exit !found}'; then
  echo "ERROR: no authorized device found. Run 'adb devices -l'."
  echo "  - Enable USB debugging and tap 'Allow' on the phone."
  echo "  - Or connect over Wi-Fi: adb connect <phone-ip>:5555"
  exit 1
fi

echo "==> Building + installing ($CONFIG)..."
dotnet build "$PROJ" -c "$CONFIG" -t:Install

echo "==> Launching $APPID..."
adb shell monkey -p "$APPID" -c android.intent.category.LAUNCHER 1 >/dev/null

echo "==> Done. Tail logs with:  adb logcat -s mono-stdout:* DOTNET:* AndroidRuntime:E"
