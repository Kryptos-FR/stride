# Third-party notices

Tessera's packages contain no code or art of a third party. They depend on Stride, which is under the MIT license, through its packages.

## The sample game's art

The sample game, Thornwick, uses art from three packs by Kenney (www.kenney.nl), each under Creative Commons Zero (CC0 1.0): http://creativecommons.org/publicdomain/zero/1.0/. The art is in the repository only, in the sample's folder; no package of Tessera contains it. Each folder below has the pack's own `License.txt`.

| Pack | Files taken | Where they are |
|---|---|---|
| Tiny Town 1.1, by Kenney (www.kenney.nl), Creative Commons Zero (CC0 1.0) | `Tilemap/tilemap_packed.png`, `License.txt` | `sample/Thornwick/Thornwick.Game/Resources/Kenney/TinyTown/` |
| Tiny Dungeon 1.0, by Kenney (www.kenney.nl), Creative Commons Zero (CC0 1.0) | `Tilemap/tilemap_packed.png`, `License.txt` | `sample/Thornwick/Thornwick.Game/Resources/Kenney/TinyDungeon/` |
| RPG Urban Pack 1.0, by Kenney (www.kenney.nl), Creative Commons Zero (CC0 1.0) | `Tilemap/tilemap_packed.png`, `License.txt` | `sample/Thornwick/Thornwick.Game/Resources/Kenney/RpgUrbanPack/` |

Each `tilemap_packed.png` is the pack's sheet of 16 by 16 tiles without spacing, the same bytes as in the pack's zip, and each `License.txt` the pack's own. The tile sets of the sample read Tiny Town's and Tiny Dungeon's sheets; the RPG Urban Pack's is there as the source of the people's picture only.

Two pictures beside the sheets are derived from them by scripts of the sample, and are under the same licence:

- `sample/Thornwick/Thornwick.Game/Resources/Kenney/RpgUrbanPack/people.png`, derived from the RPG Urban Pack's sheet by `sample/Thornwick/tools/draw-people.sh`: the pack's six people, laid out a row for each side they face, their outer line in the colour of Tiny Town's outlines.
- `sample/Thornwick/Thornwick.Game/Resources/Kenney/TinyTown/water.png`, derived from Tiny Town's sheet by `sample/Thornwick/tools/draw-water.sh`: a pond made of the pack's patch of earth and its grass banks, the earth turned to the blue of the pack's well, with waves.
