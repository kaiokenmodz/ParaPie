#!/usr/bin/env bash
# Builds ParaPie.dll against the installed game.
# Usage: CSC=/path/to/roslyn/csc.exe ./build.sh [--install]
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd -W)"
GAME="${GAME:-C:/Program Files (x86)/Steam/steamapps/common/Paralives}"
M="$GAME/Paralives_Data/Managed"
B="$GAME/BepInEx/core"
CSC="${CSC:?set CSC to Roslyn csc.exe (Microsoft.Net.Compilers.Toolset/tasks/net472/csc.exe)}"
RSP="$HERE/build/build.rsp"
mkdir -p "$HERE/build"
{
  echo "/target:library /nostdlib+ /langversion:latest /optimize+ /deterministic /nologo /warn:4 /nowarn:1701,1702"
  [ -n "${DEFINE:-}" ] && echo "/define:$DEFINE"
  echo "/out:\"$HERE/build/ParaPie.dll\""
  for f in "$M"/*.dll; do echo "/r:\"$f\""; done
  echo "/r:\"$B/BepInEx.dll\""
  echo "/r:\"$B/0Harmony.dll\""
  for f in "$HERE"/src/*.cs; do echo "\"$f\""; done
} > "$RSP"
MSYS_NO_PATHCONV=1 "$CSC" /noconfig @"$(cygpath -w "$RSP")"
echo "Built $HERE/build/ParaPie.dll"
if [ "${1:-}" = "--install" ]; then
  mkdir -p "$GAME/BepInEx/plugins/ParaPie"
  cp "$HERE/build/ParaPie.dll" "$GAME/BepInEx/plugins/ParaPie/"
  echo "Installed to $GAME/BepInEx/plugins/ParaPie"
fi
