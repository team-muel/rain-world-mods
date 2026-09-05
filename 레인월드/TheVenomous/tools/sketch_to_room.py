#!/usr/bin/env python3
"""
Convert a simple color sketch into a Rain World room planning draft.

Supported sketch colors:
- black line: solid ground or wall
- blue: water
- red: threat
- orange circle: exit
- green circle: entrance

The output is a planning draft for Rained work, not a finished Rain World room.
"""

from __future__ import annotations

import argparse
import json
import math
import re
import sys
from collections import deque
from dataclasses import asdict, dataclass
from pathlib import Path

from PIL import Image, ImageDraw


VENOMOUS_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_ROOT = VENOMOUS_ROOT / "drafts"
ROOM_NAME_PATTERN = re.compile(r"^[A-Z0-9_]+$")


@dataclass
class Component:
    kind: str
    pixel_count: int
    bbox: tuple[int, int, int, int]
    center: tuple[float, float]
    tile_bbox: tuple[int, int, int, int]
    tile_center: tuple[float, float]


PALETTE = {
    "solid": (24, 24, 24),
    "water": (40, 120, 255),
    "threat": (230, 40, 40),
    "exit": (255, 145, 20),
    "entrance": (30, 190, 70),
}

PREVIEW_COLORS = {
    "solid": (20, 20, 20, 255),
    "water": (30, 110, 230, 170),
    "threat": (230, 35, 35, 210),
    "exit": (255, 145, 20, 235),
    "entrance": (35, 190, 70, 235),
}

# Sketch lines often touch diagonally after sampling. Eight-way connectivity keeps
# one hand-drawn feature together instead of splitting it into several fragments.
NEIGHBORS = (
    (1, 0),
    (-1, 0),
    (0, 1),
    (0, -1),
    (1, 1),
    (1, -1),
    (-1, 1),
    (-1, -1),
)


def normalize_room_name(room: str) -> str:
    cleaned = room.strip().upper().replace("-", "_")
    if not cleaned or ROOM_NAME_PATTERN.fullmatch(cleaned) is None:
        raise ValueError(
            "room name must contain only letters, numbers, and underscores "
            "(example: FG_A04)"
        )
    return cleaned


def classify_pixel(r: int, g: int, b: int, alpha: int) -> str | None:
    if alpha < 32:
        return None

    brightness = (r + g + b) / 3
    if brightness < 70:
        return "solid"
    if r > 170 and 70 <= g <= 180 and b < 90:
        return "exit"
    if g > 120 and g > r + 25 and g > b + 20:
        return "entrance"
    if b > 120 and b > r + 35 and b > g + 20:
        return "water"
    if r > 150 and r > g + 45 and r > b + 45:
        return "threat"
    return None


def load_class_map(image: Image.Image, sample: int) -> tuple[list[list[str | None]], int, int]:
    rgba = image.convert("RGBA")
    width = math.ceil(rgba.width / sample)
    height = math.ceil(rgba.height / sample)
    class_map: list[list[str | None]] = [[None for _ in range(width)] for _ in range(height)]

    pixels = rgba.load()
    for gy in range(height):
        for gx in range(width):
            votes: dict[str, int] = {}
            start_x = gx * sample
            start_y = gy * sample
            end_x = min(start_x + sample, rgba.width)
            end_y = min(start_y + sample, rgba.height)
            for y in range(start_y, end_y):
                for x in range(start_x, end_x):
                    kind = classify_pixel(*pixels[x, y])
                    if kind is not None:
                        votes[kind] = votes.get(kind, 0) + 1
            if votes:
                kind, count = max(votes.items(), key=lambda item: item[1])
                total = (end_x - start_x) * (end_y - start_y)
                if count >= max(1, total // 12):
                    class_map[gy][gx] = kind

    return class_map, width, height


def find_components(class_map: list[list[str | None]], width: int, height: int, sample: int) -> list[Component]:
    seen = [[False for _ in range(width)] for _ in range(height)]
    components: list[Component] = []

    for y in range(height):
        for x in range(width):
            kind = class_map[y][x]
            if kind is None or seen[y][x]:
                continue

            queue: deque[tuple[int, int]] = deque([(x, y)])
            seen[y][x] = True
            cells: list[tuple[int, int]] = []

            while queue:
                cx, cy = queue.popleft()
                cells.append((cx, cy))
                for dx, dy in NEIGHBORS:
                    nx = cx + dx
                    ny = cy + dy
                    if 0 <= nx < width and 0 <= ny < height and not seen[ny][nx] and class_map[ny][nx] == kind:
                        seen[ny][nx] = True
                        queue.append((nx, ny))

            if should_keep_component(kind, len(cells)):
                min_x = min(c[0] for c in cells)
                max_x = max(c[0] for c in cells)
                min_y = min(c[1] for c in cells)
                max_y = max(c[1] for c in cells)
                center_x = sum(c[0] for c in cells) / len(cells)
                center_y = sum(c[1] for c in cells) / len(cells)
                components.append(
                    Component(
                        kind=kind,
                        pixel_count=len(cells) * sample * sample,
                        bbox=(min_x * sample, min_y * sample, (max_x + 1) * sample, (max_y + 1) * sample),
                        center=((center_x + 0.5) * sample, (center_y + 0.5) * sample),
                        tile_bbox=(min_x, min_y, max_x, max_y),
                        tile_center=(round(center_x, 2), round(center_y, 2)),
                    )
                )

    return components


def should_keep_component(kind: str, cells: int) -> bool:
    if kind in ("exit", "entrance"):
        return cells >= 2
    if kind == "threat":
        return cells >= 2
    return cells >= 4


def make_preview(source: Image.Image, class_map: list[list[str | None]], width: int, height: int, sample: int, components: list[Component]) -> Image.Image:
    preview = source.convert("RGBA")
    overlay = Image.new("RGBA", preview.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)

    for y in range(height):
        for x in range(width):
            kind = class_map[y][x]
            if kind is None:
                continue
            draw.rectangle(
                (x * sample, y * sample, min((x + 1) * sample, preview.width), min((y + 1) * sample, preview.height)),
                fill=PREVIEW_COLORS[kind],
            )

    preview = Image.alpha_composite(preview, overlay)
    draw = ImageDraw.Draw(preview)
    for idx, comp in enumerate(components, 1):
        color = PREVIEW_COLORS[comp.kind]
        draw.rectangle(comp.bbox, outline=color[:3] + (255,), width=2)
        draw.text((comp.bbox[0] + 3, comp.bbox[1] + 3), f"{idx}:{comp.kind}", fill=(255, 255, 255, 255))

    return preview.convert("RGB")


def write_outputs(room: str, image_path: Path, output_dir: Path, sample: int) -> None:
    room_name = normalize_room_name(room)

    # Pillow opens images lazily. Copy the decoded image while the file handle is
    # open so the rest of the pipeline does not keep the source file locked.
    with Image.open(image_path) as opened:
        opened.load()
        source = opened.convert("RGBA")

    class_map, grid_width, grid_height = load_class_map(source, sample)
    components = find_components(class_map, grid_width, grid_height, sample)

    room_dir = output_dir / room_name
    room_dir.mkdir(parents=True, exist_ok=True)

    data = {
        "room": room_name,
        "source_image": str(image_path),
        "image_size": {"width": source.width, "height": source.height},
        "analysis_grid": {"width": grid_width, "height": grid_height, "sample_pixels": sample},
        "legend": {
            "solid": "black line: ground or wall",
            "water": "blue area: water",
            "threat": "red area: threat",
            "exit": "orange circle: exit",
            "entrance": "green circle: entrance",
        },
        "components": [asdict(comp) for comp in components],
        "counts": count_components(components),
    }

    (room_dir / "analysis.json").write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    (room_dir / "README.md").write_text(make_readme(room_name, data), encoding="utf-8")
    preview = make_preview(source, class_map, grid_width, grid_height, sample, components)
    preview.save(room_dir / "preview.png")

    print(f"Created sketch draft: {room_dir}")
    print("- analysis.json")
    print("- README.md")
    print("- preview.png")
    print("")
    print("Detected:")
    for key, value in data["counts"].items():
        print(f"- {key}: {value}")


def count_components(components: list[Component]) -> dict[str, int]:
    counts = {key: 0 for key in PALETTE}
    for comp in components:
        counts[comp.kind] = counts.get(comp.kind, 0) + 1
    return counts


def make_readme(room: str, data: dict) -> str:
    lines = [
        f"# {room.upper()} Sketch Draft",
        "",
        "This is an auto-generated planning draft from a simple color sketch.",
        "",
        "## Detected Elements",
        "",
    ]
    for key, value in data["counts"].items():
        lines.append(f"- {key}: {value}")
    lines.extend(
        [
            "",
            "## Rained Notes",
            "",
            "- Use black components as the main solid terrain/wall guide.",
            "- Use blue components as water placement guide.",
            "- Use red components as threat placement guide. Pick actual creatures/hazards manually in Rained or world files.",
            "- Use orange components as exit locations.",
            "- Use green components as entrance/player-entry locations.",
            "- Open `preview.png` to see detected regions over the original sketch.",
            "",
            "## Component List",
            "",
            "| # | Kind | Tile Center | Tile Box | Pixels |",
            "|---|------|-------------|----------|--------|",
        ]
    )
    for idx, comp in enumerate(data["components"], 1):
        lines.append(
            f"| {idx} | {comp['kind']} | {tuple(comp['tile_center'])} | {tuple(comp['tile_bbox'])} | {comp['pixel_count']} |"
        )
    lines.append("")
    return "\n".join(lines)


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Convert a color sketch into a room planning draft")
    parser.add_argument("image", help="input PNG/JPG sketch")
    parser.add_argument("room", help="room name, for example FG_A04")
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT_ROOT), help="output folder")
    parser.add_argument("--sample", type=int, default=8, help="pixel sampling size; lower is more detailed")
    args = parser.parse_args(argv)

    image_path = Path(args.image).resolve()
    if not image_path.exists():
        print(f"Image not found: {image_path}", file=sys.stderr)
        return 1
    if args.sample < 1:
        print("--sample must be at least 1", file=sys.stderr)
        return 1

    try:
        write_outputs(args.room, image_path, Path(args.output).resolve(), args.sample)
    except ValueError as exc:
        print(str(exc), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))