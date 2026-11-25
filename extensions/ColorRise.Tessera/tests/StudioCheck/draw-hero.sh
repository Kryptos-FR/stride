#!/usr/bin/env bash
# Draws StudioCheck.Game/Resources/hero.png with ImageMagick: 48 by 96 pixels, three columns by four rows of figures 16
# pixels wide and 24 high. The rows, from the top, face down, left, right and up, each with a body of its own colour (blue,
# green, red, purple) and a face on the side it looks at (two eyes, the left one, the right one, none). The columns are the
# left foot forward, both feet, the right foot forward: the foot forward reaches the bottom row, the other stops 2 pixels
# above it.
set -euo pipefail
cd "$(dirname "$0")"

colours=('#3060c0' '#30a050' '#c04040' '#8040c0')
draw=()
for row in 0 1 2 3; do
    for column in 0 1 2; do
        x=$((column * 16))
        y=$((row * 24))
        draw+=(-stroke none -fill '#f0c090' -draw "rectangle $((x + 5)),$((y + 1)) $((x + 10)),$((y + 6))")
        case $row in
            0) draw+=(-fill '#202020' -draw "point $((x + 6)),$((y + 3))" -draw "point $((x + 9)),$((y + 3))") ;;
            1) draw+=(-fill '#202020' -draw "point $((x + 5)),$((y + 3))") ;;
            2) draw+=(-fill '#202020' -draw "point $((x + 10)),$((y + 3))") ;;
            3) draw+=(-fill '#5a3a1a' -draw "rectangle $((x + 5)),$((y + 1)) $((x + 10)),$((y + 5))") ;;
        esac
        draw+=(-fill "${colours[$row]}" -draw "rectangle $((x + 4)),$((y + 7)) $((x + 11)),$((y + 17))")
        left=$((y + 23)); right=$((y + 23))
        [ "$column" = 0 ] && right=$((y + 21))
        [ "$column" = 2 ] && left=$((y + 21))
        draw+=(-fill '#303030' -draw "rectangle $((x + 5)),$((y + 18)) $((x + 6)),$left" -draw "rectangle $((x + 9)),$((y + 18)) $((x + 10)),$right")
    done
done

convert -size 48x96 xc:none +antialias "${draw[@]}" -depth 8 PNG32:StudioCheck.Game/Resources/hero.png
