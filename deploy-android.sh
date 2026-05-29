#!/usr/bin/env bash
# Build, sign, and deploy the Android app to a connected device,
# bypassing Rider (workaround for RIDER-137704 "Unable to evaluate
# deployment properties").
#
# Usage:
#   ./deploy-android.sh            # build + install + launch (Debug)
#   ./deploy-android.sh apk        # only build the signed APK, print its path
#   CONFIG=Release ./deploy-android.sh
set -euo pipefail

export ANDROID_HOME="${ANDROID_HOME:-$HOME/Android/Sdk}"
export ANDROID_SDK_ROOT="${ANDROID_SDK_ROOT:-$ANDROID_HOME}"
export PATH="$PATH:$ANDROID_HOME/platform-tools"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ="$ROOT/src/MobileApp.Android/MobileApp.Android.csproj"
CONFIG="${CONFIG:-Debug}"
APPID="com.AntonioValentini.TrueMobile"
APK="$ROOT/src/MobileApp.Android/bin/$CONFIG/net10.0-android/${APPID}-Signed.apk"

if [[ "${1:-}" == "apk" ]]; then
  echo "==> Building signed APK ($CONFIG)..."
  dotnet build "$PROJ" -c "$CONFIG" -t:SignAndroidPackage
  echo "==> APK: $APK"
  exit 0
fi

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
