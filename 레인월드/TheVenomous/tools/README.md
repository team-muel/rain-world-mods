# The Venomous Map Helper

Small helper for building Rain World regions with Rained.

It does not draw rooms. Use Rained for the actual room art and geometry, then use this helper to catch common file and world-map mistakes before launching Rain World.

## Quick Use

Double-click:

- `check_fg.bat` - checks the current `FG` region.
- `install_mod.bat` - copies `TheVenomous/mod` into the Rain World installed mod folder.
- `sketch_to_room.bat image.png FG_A04` - reads a simple color sketch and creates a room planning draft.

## Commands

```bat
python rw_map_helper.py check FG
python rw_map_helper.py add-room FG FG_A04 --connects FG_A03
python rw_map_helper.py add-room FG FG_S01 --connects FG_A01 --shelter
python rw_map_helper.py install
python sketch_to_room.py sketch.png FG_A04
```

## What It Checks

- Region folder exists.
- Room folder exists.
- `world_fg.txt` exists.
- Every room listed in `ROOMS` has a matching `fg_*.txt` room file.
- Room connections point to rooms that are also listed.
- Missing `*_settings.txt` files.
- Missing camera PNG files.
- Rooms that are marked `SHELTER` but do not use a shelter-style name like `FG_S01`.
- Room files that exist but are not listed in the world file.

## Recommended Workflow

1. Make or edit the room in Rained.
2. Save/export the room into `TheVenomous/mod/world/fg-rooms`.
3. Run `check_fg.bat`.
4. Fix anything under `Errors`.
5. Run `install_mod.bat`.
6. Launch Rain World and test with Warp Menu.

## Sketch Colors

For `sketch_to_room.py`:

- Black line: ground or wall.
- Blue: water.
- Red: threat.
- Orange circle: exit.
- Green circle: entrance.

The sketch tool creates `analysis.json`, `README.md`, and `preview.png` under `TheVenomous/drafts/<ROOM_NAME>`. It is a planning aid for Rained, not a finished room exporter.
