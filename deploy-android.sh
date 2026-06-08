#!/usr/bin/env bash
# Build, sign, and deploy the Android app to a connected device,
# bypassing Rider (workaround for RIDER-137704 "Unable to evaluate
# deployment properties").
#
# Local app data (OAuth tokens, beneficiaries, payment history) under
# files/TrueLayerMobile/ is backed up before install and restored after,
# so it survives even when Android has to fully reinstall the package
# (e.g. signature mismatch from an earlier Rider install). The app is
# DEBUGGABLE, so `adb run-as` can read/write its private dir without root.
#
# Usage:
#   ./deploy-android.sh               # backup + build + install + restore + launch
#   ./deploy-android.sh --no-preserve # skip the data backup/restore
#   ./deploy-android.sh apk           # only build the signed APK, print its path
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
DATADIR="TrueLayerMobile"          # under the app's files/ dir
PRESERVE=1

# --- apk-only mode -----------------------------------------------------------
if [[ "${1:-}" == "apk" ]]; then
  echo "==> Building signed APK ($CONFIG)..."
  dotnet build "$PROJ" -c "$CONFIG" -t:SignAndroidPackage --no-incremental
  echo "==> APK: $APK"
  exit 0
fi

[[ "${1:-}" == "--no-preserve" ]] && PRESERVE=0

# --- temp backup file + cleanup ----------------------------------------------
BACKUP="$(mktemp -t tl-appdata.XXXXXX.tar)"
cleanup() {
  rm -f "$BACKUP"
  adb shell rm -f "/data/local/tmp/$(basename "$BACKUP")" >/dev/null 2>&1 || true
}
trap cleanup EXIT

# --- require a connected, authorized device ----------------------------------
echo "==> Checking for a connected device..."
adb start-server >/dev/null 2>&1 || true
if ! adb devices | awk 'NR>1 && $2=="device"{found=1} END{exit !found}'; then
  echo "ERROR: no authorized device found. Run 'adb devices -l'."
  echo "  - Enable USB debugging and tap 'Allow' on the phone."
  echo "  - Or connect over Wi-Fi: adb connect <phone-ip>:5555"
  exit 1
fi

is_installed() { adb shell pm list packages 2>/dev/null | grep -q "^package:$APPID$"; }

# --- 1. backup app-private data ----------------------------------------------
HAVE_BACKUP=0
if [[ "$PRESERVE" == "1" ]] && is_installed; then
  echo "==> Backing up app data ($DATADIR)..."
  # exec-out gives a clean binary stream (no tty translation).
  if adb exec-out run-as "$APPID" tar c -C files "$DATADIR" > "$BACKUP" 2>/dev/null \
     && [[ -s "$BACKUP" ]]; then
    # Validate the captured stream is a real tar before we rely on it; this also
    # localizes failures (bad backup here vs. bad restore later).
    if tar tf "$BACKUP" >/dev/null 2>&1; then
      HAVE_BACKUP=1
      echo "    saved $(wc -c < "$BACKUP") bytes, $(tar tf "$BACKUP" | wc -l | tr -d ' ') entries"
    else
      echo "    WARNING: captured stream is NOT a valid tar — restore will be skipped."
      echo "    first 64 bytes:"; od -An -c -N 64 "$BACKUP"
    fi
  else
    echo "    no existing data to back up (skipping)"
  fi
fi

# --- 2. build signed APK -----------------------------------------------------
echo "==> Building signed APK ($CONFIG)..."
dotnet build "$PROJ" -c "$CONFIG" -t:SignAndroidPackage

# --- 3. install (with reinstall-safe fallback) -------------------------------
echo "==> Installing..."
if ! adb install -r "$APK"; then
  echo "    'install -r' failed (likely signature mismatch); uninstalling + reinstalling."
  adb uninstall "$APPID" || true
  adb install "$APK"
fi

# --- 4. restore app-private data ---------------------------------------------
if [[ "$HAVE_BACKUP" == "1" ]]; then
  echo "==> Restoring app data..."
  REMOTE="/data/local/tmp/$(basename "$BACKUP")"
  adb push "$BACKUP" "$REMOTE" >/dev/null
  adb shell run-as "$APPID" mkdir -p files
  # An app (untrusted_app SELinux domain) generally can't read shell-owned files
  # in /data/local/tmp, so `run-as ... tar xf "$REMOTE"` reads nothing. Instead
  # let the shell user `cat` the archive into the app's tar over a pipe. The pipe
  # and quotes below are deliberately parsed by the *device* shell (single arg),
  # unlike a nested `sh -c "...&&..."` whose quoting adb would flatten away.
  adb shell "cat '$REMOTE' | run-as '$APPID' tar x -C files"
  adb shell rm -f "$REMOTE" >/dev/null 2>&1 || true
  echo "    restored $DATADIR (tokens, beneficiaries, payment history)"
fi

# --- 5. launch ---------------------------------------------------------------
echo "==> Launching $APPID..."
adb shell monkey -p "$APPID" -c android.intent.category.LAUNCHER 1 >/dev/null

echo "==> Done. Tail logs with:  adb logcat -s mono-stdout:* DOTNET:* AndroidRuntime:E"
