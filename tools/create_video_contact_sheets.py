from __future__ import annotations

import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: create_video_contact_sheets.py <frames-directory> <output-directory>", file=sys.stderr)
        return 2

    frames_dir = Path(sys.argv[1]).resolve()
    output_dir = Path(sys.argv[2]).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    frames = sorted(frames_dir.glob("frame_*.png"))
    if not frames:
        raise RuntimeError(f"No frames found in {frames_dir}")

    columns, rows = 4, 3
    thumb_width, thumb_height = 480, 270
    label_height = 28
    margin = 12
    page_size = columns * rows
    font = ImageFont.load_default()

    for page_index in range(math.ceil(len(frames) / page_size)):
        page_frames = frames[page_index * page_size : (page_index + 1) * page_size]
        sheet = Image.new(
            "RGB",
            (
                columns * thumb_width + (columns + 1) * margin,
                rows * (thumb_height + label_height) + (rows + 1) * margin,
            ),
            "#111216",
        )
        draw = ImageDraw.Draw(sheet)
        for index, frame_path in enumerate(page_frames):
            row, column = divmod(index, columns)
            left = margin + column * (thumb_width + margin)
            top = margin + row * (thumb_height + label_height + margin)
            with Image.open(frame_path) as frame:
                frame = frame.convert("RGB")
                frame.thumbnail((thumb_width, thumb_height), Image.Resampling.LANCZOS)
                frame_left = left + (thumb_width - frame.width) // 2
                frame_top = top + (thumb_height - frame.height) // 2
                sheet.paste(frame, (frame_left, frame_top))
            label = frame_path.stem.removeprefix("frame_").removesuffix("s")
            seconds = float(label)
            timestamp = f"{int(seconds // 60):02d}:{int(seconds % 60):02d}"
            draw.text((left + 4, top + thumb_height + 6), timestamp, fill="#F4F4F5", font=font)

        start = page_index * page_size
        end = start + len(page_frames) - 1
        output = output_dir / f"contact_{start:03d}-{end:03d}s.png"
        sheet.save(output, optimize=True)
        print(output)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
