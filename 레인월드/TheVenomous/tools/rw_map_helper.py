#!/usr/bin/env python3
"""
Rain World region helper for The Venomous.

This tool does not replace Rained. It helps after Rained export:
- validates region and room file naming
- checks world connections against available room files
- adds a room line to a region world file
- copies the editable mod folder into the installed Rain World mod folder
"""

from __future__ import annotations

import argparse
import re
import shutil
import sys
from dataclasses import dataclass
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
VENOMOUS_ROOT = Path(__file__).resolve().parents[1]
MOD_ROOT = VENOMOUS_ROOT / "mod"
DEFAULT_INSTALL_ROOT = Path(
    r"D:\SteamLibrary\steamapps\common\Rain World\RainWorld_Data\StreamingAssets\mods\stwam.starter"
)


@dataclass
class RoomLine:
    name: str
    connections: list[str]
    tag: str | None
    raw: str
    line_no: int


def region_dir(region: str) -> Path:
    return MOD_ROOT / "world" / region.lower()


def room_dir(region: str) -> Path:
    return MOD_ROOT / "world" / f"{region.lower()}-rooms"


def world_file(region: str) -> Path:
    return region_dir(region) / f"world_{region.lower()}.txt"


def normalize_room_name(name: str, region: str) -> str:
    cleaned = name.strip().upper().replace("-", "_")
    prefix = region.upper() + "_"
    if not cleaned.startswith(prefix):
        cleaned = prefix + cleaned
    return cleaned


def unique_in_order(items: list[str]) -> list[str]:
    """Return values once, while keeping the order used in the world file."""
    seen: set[str] = set()
    result: list[str] = []
    for item in items:
        if item in seen:
            continue
        seen.add(item)
        result.append(item)
    return result


def expected_room_txt(room: str, region: str) -> Path:
    return room_dir(region) / f"{room.lower()}.txt"


def expected_settings(room: str, region: str) -> Path:
    return room_dir(region) / f"{room.lower()}_settings.txt"


def expected_pngs(room: str, region: str) -> list[Path]:
    folder = room_dir(region)
    return sorted(folder.glob(f"{room.lower()}_*.png"))


def parse_world(region: str) -> list[RoomLine]:
    path = world_file(region)
    if not path.exists():
        raise FileNotFoundError(f"world file not found: {path}")

    rooms: list[RoomLine] = []
    in_rooms = False
    for idx, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        stripped = line.strip()
        if stripped == "ROOMS":
            in_rooms = True
            continue
        if stripped == "END ROOMS":
            in_rooms = False
            continue
        if not in_rooms or not stripped or stripped.startswith("//"):
            continue

        parts = [part.strip() for part in stripped.split(":")]
        if not parts or not parts[0]:
            continue

        name = parts[0].upper()
        connections: list[str] = []
        tag = None
        if len(parts) > 1 and parts[1]:
            connections = [item.strip().upper() for item in parts[1].split(",") if item.strip()]
        if len(parts) > 2 and parts[2]:
            tag = parts[2].upper()
        rooms.append(RoomLine(name=name, connections=connections, tag=tag, raw=line, line_no=idx))

    return rooms


def validate(region: str) -> int:
    region = region.upper()
    errors: list[str] = []
    warnings: list[str] = []

    if not region_dir(region).exists():
        errors.append(f"region folder missing: {region_dir(region)}")
    if not room_dir(region).exists():
        errors.append(f"room folder missing: {room_dir(region)}")
    if not world_file(region).exists():
        errors.append(f"world file missing: {world_file(region)}")

    if errors:
        print_report(region, errors, warnings)
        return 1

    rooms = parse_world(region)
    room_names = {room.name for room in rooms}
    first_room_line: dict[str, int] = {}

    for room in rooms:
        # Duplicate declarations are easy to miss in a long region file and can
        # make connection debugging confusing, so report the second declaration.
        if room.name in first_room_line:
            errors.append(
                f"line {room.line_no}: duplicate room declaration: {room.name} "
                f"(first declared on line {first_room_line[room.name]})"
            )
        else:
            first_room_line[room.name] = room.line_no

        if "-" in room.name:
            errors.append(f"line {room.line_no}: use underscore, not hyphen: {room.name}")
        if not room.name.startswith(region + "_"):
            errors.append(f"line {room.line_no}: room must start with {region}_: {room.name}")

        txt = expected_room_txt(room.name, region)
        settings = expected_settings(room.name, region)
        pngs = expected_pngs(room.name, region)

        if not txt.exists():
            errors.append(f"{room.name}: missing room text file: {txt.name}")
        if not settings.exists():
            warnings.append(f"{room.name}: missing settings file: {settings.name}")
        if not pngs:
            warnings.append(f"{room.name}: no camera png found, expected {room.name.lower()}_1.png")

        if room.name in room.connections:
            errors.append(f"line {room.line_no}: room connects to itself: {room.name}")

        duplicate_connections = [
            name
            for name in unique_in_order(room.connections)
            if room.connections.count(name) > 1
        ]
        for duplicate in duplicate_connections:
            warnings.append(
                f"line {room.line_no}: duplicate connection can be removed: "
                f"{room.name} -> {duplicate}"
            )

        for other in room.connections:
            if other not in room_names:
                errors.append(f"line {room.line_no}: connection target is not in ROOMS: {room.name} -> {other}")

        if room.tag == "SHELTER" and not room.name.startswith(region + "_S"):
            warnings.append(f"{room.name}: marked SHELTER but name is not shelter-style {region}_S##")

    room_txt_names = {path.stem.upper() for path in room_dir(region).glob("*.txt") if not path.name.endswith("_settings.txt")}
    for txt_room in sorted(room_txt_names - room_names):
        warnings.append(f"{txt_room}: room file exists but is not listed in world_{region.lower()}.txt")

    print_report(region, errors, warnings)
    return 1 if errors else 0


def print_report(region: str, errors: list[str], warnings: list[str]) -> None:
    print(f"\nRain World map check: {region}")
    print("=" * 34)
    if not errors and not warnings:
        print("OK: region/world/room files look consistent.")
        return

    if errors:
        print("\nErrors")
        for item in errors:
            print(f"- {item}")
    if warnings:
        print("\nWarnings")
        for item in warnings:
            print(f"- {item}")


def add_room(region: str, room: str, connects: str, shelter: bool) -> int:
    region = region.upper()
    room_name = normalize_room_name(room, region)
    path = world_file(region)
    if not path.exists():
        raise FileNotFoundError(f"world file not found: {path}")

    rooms = parse_world(region)
    existing_names = {line.name for line in rooms}
    if room_name in existing_names:
        print(f"{room_name} is already listed in {path.name}.")
        return 0

    connection_names = unique_in_order(
        [normalize_room_name(item, region) for item in connects.split(",") if item.strip()]
    )

    # Validate requested links before editing the world file. A typo should not
    # leave a half-added room behind and force a manual repair afterwards.
    if room_name in connection_names:
        print(f"Cannot add {room_name}: a room cannot connect to itself.", file=sys.stderr)
        print("Nothing was changed.", file=sys.stderr)
        return 1

    missing_connections = [name for name in connection_names if name not in existing_names]
    if missing_connections:
        print(f"Cannot add {room_name}: unknown connection target(s):", file=sys.stderr)
        for name in missing_connections:
            print(f"- {name}", file=sys.stderr)
        print("Nothing was changed.", file=sys.stderr)
        return 1

    new_line = room_name
    if connection_names:
        new_line += " : " + ", ".join(connection_names)
    if shelter:
        if not connection_names:
            new_line += " : "
        new_line += " : SHELTER"

    lines = path.read_text(encoding="utf-8-sig").splitlines()
    insert_at = None
    for idx, line in enumerate(lines):
        if line.strip() == "END ROOMS":
            insert_at = idx
            break
    if insert_at is None:
        raise ValueError(f"{path.name} has no END ROOMS marker")

    lines.insert(insert_at, new_line)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Added {room_name} to {path}.")
    return validate(region)


def install_mod(target: Path) -> int:
    if not MOD_ROOT.exists():
        raise FileNotFoundError(f"editable mod folder not found: {MOD_ROOT}")
    if target.exists():
        shutil.rmtree(target)
    ignore = shutil.ignore_patterns("*.bak", "*.bak2", "*.bak3", "*.before-*", "*.user-backup-*")
    shutil.copytree(MOD_ROOT, target, ignore=ignore)
    print(f"Installed mod files to {target}")
    return 0


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="The Venomous Rain World map helper")
    sub = parser.add_subparsers(dest="cmd", required=True)

    check = sub.add_parser("check", help="validate a region and its room files")
    check.add_argument("region", nargs="?", default="FG")

    add = sub.add_parser("add-room", help="add a room to world_<region>.txt")
    add.add_argument("region")
    add.add_argument("room")
    add.add_argument("--connects", default="", help="comma-separated room connections")
    add.add_argument("--shelter", action="store_true", help="mark room as SHELTER")

    install = sub.add_parser("install", help="copy editable mod to Rain World mods folder")
    install.add_argument("--target", default=str(DEFAULT_INSTALL_ROOT))

    args = parser.parse_args(argv)
    if args.cmd == "check":
        return validate(args.region)
    if args.cmd == "add-room":
        return add_room(args.region, args.room, args.connects, args.shelter)
    if args.cmd == "install":
        return install_mod(Path(args.target))
    return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))