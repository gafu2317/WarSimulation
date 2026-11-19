#!/bin/sh
set -eu

# Blender 5.0 can pass a null Metal device name to strstr during startup.
if ! metal_status=$(/usr/bin/osascript -l JavaScript <<'JXA'
ObjC.import("Metal");
const device = $.MTLCreateSystemDefaultDevice();
const name = ObjC.unwrap(device.name);
typeof name === "string" && name.length > 0 ? "METAL_OK" : "METAL_UNAVAILABLE";
JXA
) || [ "$metal_status" != "METAL_OK" ]; then
    printf '%s\n' 'MetalのGPU名を取得できないため、Blenderの起動を中止した。起動済みのBlenderか、macOSのターミナルで実行する。' >&2
    exit 1
fi

exec /Applications/Blender.app/Contents/MacOS/Blender --background "$@"
