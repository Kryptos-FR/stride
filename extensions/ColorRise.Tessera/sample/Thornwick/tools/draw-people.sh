#!/usr/bin/env bash
# Draws Resources/Kenney/RpgUrbanPack/people.png from the pack's sheet with ImageMagick: 288 by 64 pixels, six people side
# by side, each a block of three columns by four rows of 16 pixel tiles. The rows of a block, from the top, face down, left,
# right and up; its columns are a step, standing, the other step. The pack has them the other way: a column for each side
# (left, down, up, right) and a row for each pose (standing, a step, the other step), in columns 23 to 26 of its sheet. The
# outer line of each figure takes the colour of the outlines of Tiny Town, so that the people sit on its tiles.
set -euo pipefail
cd "$(dirname "$0")/../Thornwick.Game/Resources/Kenney/RpgUrbanPack"

# The column of the pack for each row of a block (down, left, right, up), and its row for each column (a step, standing, the other step)
sides=(24 23 26 25)
poses=(1 0 2)
# The outer line of a tile: its pixels that touch a transparent one, or the edge of the tile, on one of their four sides
line=(\( +clone -fill '#3f2631' -colorize 100 \) \( -clone 0 -alpha extract -virtual-pixel black -morphology EdgeIn Diamond:1 -threshold 50% \) -geometry +0+0 -composite)

parts=()
for figure in 0 1 2 3 4 5; do
    for row in 0 1 2 3; do
        for column in 0 1 2; do
            x=$((sides[row] * 16))
            y=$(((figure * 3 + poses[column]) * 16))
            parts+=(\( tilemap_packed.png -crop "16x16+$x+$y" +repage "${line[@]}" \) -geometry "+$(((figure * 3 + column) * 16))+$((row * 16))" -composite)
        done
    done
done

convert -size 288x64 xc:none "${parts[@]}" -depth 8 +set date:create +set date:modify +set date:timestamp PNG32:people.png
