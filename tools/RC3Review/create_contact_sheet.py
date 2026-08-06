from __future__ import annotations

import argparse
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def load_font(size: int, bold: bool = False):
    candidates = [
        Path(r"C:\Windows\Fonts\msyhbd.ttc" if bold else r"C:\Windows\Fonts\msyh.ttc"),
        Path(r"C:\Windows\Fonts\segoeuib.ttf" if bold else r"C:\Windows\Fonts\segoeui.ttf"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size=size)
    return ImageFont.load_default()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    files = sorted(Path(args.input).glob("[0-9][0-9]_*.png"))
    if len(files) != 44:
        raise SystemExit(f"expected 44 screenshots, found {len(files)}")

    columns, rows = 4, math.ceil(len(files) / 4)
    thumb_width, thumb_height, label_height = 430, 245, 42
    gap, margin, header = 15, 24, 92
    width = margin * 2 + columns * thumb_width + (columns - 1) * gap
    height = header + margin + rows * (thumb_height + label_height) + (rows - 1) * gap + margin
    sheet = Image.new("RGB", (width, height), "#101216")
    draw = ImageDraw.Draw(sheet)
    draw.text((margin, 18), "像素蛋挞 2.3.0 RC3 · 日历、资料预览与摄影收支 UI 总览", fill="#F4F5F7", font=load_font(29, True))
    draw.text((margin, 58), "44 个隔离场景 · 真实 WPF 运行时 · 合成资料 · 自动布局检查 · 最高 200% DPI", fill="#AEB3BC", font=load_font(15))
    for index, path in enumerate(files):
        row, column = divmod(index, columns)
        x = margin + column * (thumb_width + gap)
        y = header + margin + row * (thumb_height + label_height + gap)
        with Image.open(path) as source:
            image = source.convert("RGB")
            image.thumbnail((thumb_width, thumb_height), Image.Resampling.LANCZOS)
            frame = Image.new("RGB", (thumb_width, thumb_height), "#24272D")
            frame.paste(image, ((thumb_width - image.width) // 2, (thumb_height - image.height) // 2))
            sheet.paste(frame, (x, y))
        draw.rectangle((x, y, x + thumb_width - 1, y + thumb_height - 1), outline="#3A3E46", width=1)
        draw.text((x + 7, y + thumb_height + 9), path.stem.replace("_", " "), fill="#D9DCE2", font=load_font(13))

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output, format="PNG", optimize=True)


if __name__ == "__main__":
    main()
