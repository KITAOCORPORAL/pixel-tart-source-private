from __future__ import annotations

import argparse
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def font(size: int, bold: bool = False):
    paths = [
        Path(r"C:\Windows\Fonts\msyhbd.ttc" if bold else r"C:\Windows\Fonts\msyh.ttc"),
        Path(r"C:\Windows\Fonts\segoeui.ttf"),
    ]
    for path in paths:
        if path.exists():
            return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output")
    args = parser.parse_args()
    files = sorted(Path(args.input).glob("[0-9][0-9]_*.png"))
    if not files:
        raise SystemExit("no UI review screenshots found")

    columns, thumb_w, thumb_h = 4, 420, 236
    label_h, gap, margin, header = 42, 16, 24, 82
    rows = math.ceil(len(files) / columns)
    width = margin * 2 + columns * thumb_w + (columns - 1) * gap
    height = header + margin + rows * (thumb_h + label_h + gap)
    sheet = Image.new("RGB", (width, height), "#101216")
    draw = ImageDraw.Draw(sheet)
    draw.text((margin, 18), "像素蛋挞 2.3.0 RC5 实操第二轮 UI 收口", fill="#F4F5F7", font=font(28, True))
    draw.text((margin, 54), "真实 WPF UI Review · 21 个固定场景 · 1280/1600/1920 · 深浅/高对比度 · 150%/200%", fill="#AEB4BE", font=font(16))

    for index, path in enumerate(files):
        row, column = divmod(index, columns)
        x = margin + column * (thumb_w + gap)
        y = header + margin + row * (thumb_h + label_h + gap)
        with Image.open(path) as source:
            image = source.convert("RGB")
            image.thumbnail((thumb_w, thumb_h), Image.Resampling.LANCZOS)
            frame = Image.new("RGB", (thumb_w, thumb_h), "#1C1F25")
            frame.paste(image, ((thumb_w - image.width) // 2, (thumb_h - image.height) // 2))
            sheet.paste(frame, (x, y))
        draw.rectangle((x, y, x + thumb_w - 1, y + thumb_h - 1), outline="#363A43", width=1)
        draw.text((x + 6, y + thumb_h + 8), path.stem.replace("_", " "), fill="#D8DCE3", font=font(14))

    output = Path(args.output) if args.output else Path(args.input) / "像素蛋挞_2.3.0_RC5实操第二轮UI收口总览.png"
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output, "PNG", optimize=True)


if __name__ == "__main__":
    main()
