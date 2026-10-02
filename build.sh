#!/bin/sh
# Builds ApocaHUD.dll against the game's own libraries (mono mcs). Usage: ./build.sh [out.dll]
M=${MANAGED:-/e/SteamLibrary/steamapps/common/Apocalypter/Apocalypter_Data/Managed}; B=${BEPCORE:-/e/SteamLibrary/steamapps/common/Apocalypter/BepInEx/core}
mcs -nostdlib -noconfig -target:library -langversion:7 -optimize+ -out:${1:-ApocaHUD.dll} \
  -r:$M/mscorlib.dll -r:$M/System.dll -r:$M/System.Core.dll -r:$M/netstandard.dll \
  -r:$B/BepInEx.dll \
  -r:$M/UnityEngine.dll -r:$M/UnityEngine.CoreModule.dll -r:$M/UnityEngine.UI.dll -r:$M/UnityEngine.UIModule.dll -r:$M/UnityEngine.TextRenderingModule.dll \
  Plugin.cs
