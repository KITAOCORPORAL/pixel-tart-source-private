from __future__ import annotations

import argparse
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


def font(size: int, bold: bool = False):
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
    files = sorted(path for path in Path(args.input).glob("[0-9][0-9]_*.png"))
    if len(files) != 18:
        raise SystemExit(f"expected 18 screenshots, found {len(files)}")

    columns, thumb_width, thumb_height = 3, 520, 300
    label_height, gap, margin, header = 46, 18, 26, 92
    rows = 6
    width = margin * 2 + columns * thumb_width + (columns - 1) * gap
    height = header + margin + rows * (thumb_height + label_height) + (rows - 1) * gap + margin
    sheet = Image.new("RGB", (width, height), "#101216")
    draw = ImageDraw.Draw(sheet)
    draw.text((margin, 20), "像素蛋挞 2.3.0 阶段C · 现场监看 UI 总览", fill="#F4F5F7", font=font(31, True))
    draw.text((margin, 60), "18 个隔离场景 · 真实 WPF 渲染 · 深色 / 浅色 / 高对比度 / 1280 / 150% DPI", fill="#AEB3BC", font=font(16))

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
        draw.text((x + 8, y + thumb_height + 10), path.stem.replace("_", " "), fill="#D9DCE2", font=font(16))

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output, format="PNG", optimize=True)


if __name__ == "__main__":
    main()
