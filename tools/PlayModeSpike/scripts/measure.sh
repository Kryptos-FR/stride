#!/bin/bash
# Runs the measurement matrix of the spike, one game process at a time. Each host run ends its game.
export PATH=/root/.dotnet:$PATH DOTNET_ROOT=/root/.dotnet DISPLAY=:99 VK_DRIVER_FILES=/usr/share/vulkan/icd.d/lvp_icd.json VK_ICD_FILENAMES=/usr/share/vulkan/icd.d/lvp_icd.json
HOST="dotnet /home/user/stride/tools/PlayModeSpike/PlayHost/bin/Debug/net10.0/Stride.PlayMode.Spike.Host.dll"
AGENT=/home/user/agent-release/Stride.PlayMode.Spike.Hook.dll
OUT=/home/user/runs/final
mkdir -p $OUT
run() { name=$1; shift; echo "=== $name $(date +%T)"; rm -rf $OUT/$name; timeout 900 $HOST --out $OUT/$name --agent $AGENT "$@" > $OUT/$name.console.txt 2>&1; echo "exit $? $(date +%T)"; }
for g in FpsGame RpgGame; do
  GAME=/home/user/games/$g/Bin/Linux/ReleaseEngine/$g.Linux.dll
  run $g-baseline-1 --game $GAME --baseline --seconds 10
  run $g-session --game $GAME --seconds 10
  run $g-baseline-2 --game $GAME --baseline --seconds 10
  run $g-activity --game $GAME --seconds 10 --quick --click-ms 300
  run $g-unknown --game $GAME --seconds 3 --quick --no-game-assemblies
done
# The same session on the Debug-built engine, for comparison.
run FpsGame-session-debugengine --game /home/user/games/FpsGame/Bin/Linux/Debug/FpsGame.Linux.dll --agent /home/user/stride/tools/PlayModeSpike/PlayAgent/bin/Debug/net10.0/Stride.PlayMode.Spike.Hook.dll --seconds 10
echo DONE
