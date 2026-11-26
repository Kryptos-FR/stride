#!/usr/bin/env bash
# Draws Resources/Kenney/TinyTown/water.png from the pack's sheet with ImageMagick: 48 by 64 pixels, three by four tiles of
# 16 pixels. Rows 0 to 2 are a pond in nine pieces: the pack's patch of earth with its grass banks (columns 0 to 2, rows 1
# to 3 of its sheet), the earth turned to the blue of the pack's well and its specks to the light blue. The middle piece has
# two short waves. Row 3 has the middle piece twice more, its waves 4 and 8 pixels lower, for an animated water; its third
# tile is empty.
set -euo pipefail
cd "$(dirname "$0")/../Thornwick.Game/Resources/Kenney/TinyTown"

convert -size 48x64 xc:none +antialias \
    \( tilemap_packed.png -crop 48x48+0+16 +repage -fill '#009adc' -opaque '#eaa56c' -fill '#76e4ff' -opaque '#cf8254' -opaque '#fec99c' \) \
    -geometry +0+0 -composite \
    -stroke none -fill '#009adc' -draw 'rectangle 0,48 31,63' \
    -fill '#76e4ff' \
    -draw 'rectangle 19,20 22,20' -draw 'rectangle 25,26 28,26' \
    -draw 'rectangle 3,56 6,56' -draw 'rectangle 9,62 12,62' \
    -draw 'rectangle 19,60 22,60' -draw 'rectangle 25,50 28,50' \
    -depth 8 +set date:create +set date:modify +set date:timestamp PNG32:water.png
