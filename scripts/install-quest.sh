#!/usr/bin/env bash
# Installs an APK from Builds/ onto the connected Quest over adb and launches it.
# Usage: scripts/install-quest.sh [path/to/app.apk]   (defaults to Builds/MetaXRAudioDemo.apk)
set -euo pipefail

cd "$(dirname "$0")/.."
apk="${1:-Builds/MetaXRAudioDemo.apk}"

[[ -f "$apk" ]] || { echo "APK not found: $apk (build it via Tools > MetaAudioSDK Demo > Build Quest APK)" >&2; exit 1; }
command -v adb >/dev/null || { echo "adb not found. Install it with: brew install android-platform-tools" >&2; exit 1; }

devices="$(adb devices | tail -n +2)"
if [[ "$devices" == *unauthorized* ]]; then
  echo "The Quest has not authorized this computer yet." >&2
  echo "Put the headset on and accept \"Allow USB debugging\" (tick \"Always allow from this computer\"), then run this again." >&2
  exit 1
fi
if [[ "$devices" != *$'\tdevice'* ]]; then
  echo "No Quest found over adb." >&2
  echo "Turn on Developer Mode for the headset in the Meta Horizon app, then reconnect the USB cable." >&2
  exit 1
fi

echo "Installing $apk ..."
adb install -r "$apk"

# Read the package name from the APK with aapt from Unity's bundled Android SDK, then launch the app.
aapts=(/Applications/Unity/Hub/Editor/*/PlaybackEngines/AndroidPlayer/SDK/build-tools/*/aapt)
aapt="${aapts[${#aapts[@]}-1]}"
if [[ ! -x "$aapt" ]]; then
  echo "Installed. Start it on the headset from Library > Unknown Sources."
  exit 0
fi
package="$("$aapt" dump badging "$apk" | sed -n "s/^package: name='\([^']*\)'.*/\1/p")"
echo "Launching $package ..."
adb shell monkey -p "$package" -c android.intent.category.LAUNCHER 1 >/dev/null
echo "Done. Logs: adb logcat -s Unity"
