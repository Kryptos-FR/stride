#!/usr/bin/env bash
# Draws StudioCheck.Game/Resources/town.png with ImageMagick: 64 by 48 pixels, four by three tiles of 16 pixels, each with a
# darker edge so that the grid shows. Row 0: grass, sand, water, wall. Row 1: roof, door, a tree and a flower on transparent.
# Row 2: the water twice more with its waves moved down, for an animated water, then a path and a bush to spare.
set -euo pipefail
cd "$(dirname "$0")"

convert -size 64x48 xc:none +antialias \
    -stroke '#2e6b1f' -fill '#4f9a35' -draw 'rectangle 0,0 15,15' \
    -stroke '#a08a4a' -fill '#d8c27a' -draw 'rectangle 16,0 31,15' \
    -stroke '#1d4f8c' -fill '#3b7fd0' -draw 'rectangle 32,0 47,15' \
    -stroke '#cfe6ff' -draw 'line 35,4 40,4' -draw 'line 39,10 44,10' \
    -stroke '#4a4a4a' -fill '#8a8a8a' -draw 'rectangle 48,0 63,15' \
    -stroke '#6b4a4a' -draw 'line 48,7 63,7' -draw 'line 55,0 55,7' -draw 'line 51,8 51,15' -draw 'line 59,8 59,15' \
    -stroke '#7a2020' -fill '#c0392b' -draw 'rectangle 0,16 15,31' \
    -stroke '#7a2020' -draw 'line 0,20 15,20' -draw 'line 0,24 15,24' -draw 'line 0,28 15,28' \
    -stroke '#3e2a14' -fill '#7b5230' -draw 'rectangle 16,16 31,31' -fill '#f1c40f' -stroke none -draw 'rectangle 27,24 28,25' \
    -stroke '#1e5a1e' -fill '#2f8f2f' -draw 'circle 39,22 39,17' -stroke none -fill '#6b4423' -draw 'rectangle 38,27 40,31' \
    -stroke none -fill '#e84393' -draw 'circle 55,23 55,21' -fill '#f1c40f' -draw 'point 55,23' -fill '#2f8f2f' -draw 'rectangle 55,26 55,31' \
    -stroke '#1d4f8c' -fill '#3b7fd0' -draw 'rectangle 0,32 15,47' -draw 'rectangle 16,32 31,47' \
    -stroke '#cfe6ff' -draw 'line 3,40 8,40' -draw 'line 7,46 12,46' -draw 'line 19,44 24,44' -draw 'line 23,34 28,34' \
    -stroke '#7a6a50' -fill '#b7a27a' -draw 'rectangle 32,32 47,47' -stroke '#8f7d5c' -draw 'line 32,39 47,39' \
    -stroke '#1e5a1e' -fill '#3f9f3f' -draw 'circle 55,41 55,35' \
    -depth 8 PNG32:StudioCheck.Game/Resources/town.png
