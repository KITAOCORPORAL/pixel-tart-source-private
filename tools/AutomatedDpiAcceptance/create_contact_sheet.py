from __future__ import annotations

import argparse
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


def load_font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
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
    files = sorted(Path(args.input).rglob("*.png"))
    if len(files) != 54:
        raise SystemExit(f"expected 54 screenshots, found {len(files)}")

    columns = 3
    thumb_width = 480
    thumb_height = 270
    label_height = 50
    gap = 18
    margin = 28
    header = 92
    rows = (len(files) + columns - 1) // columns
    width = margin * 2 + columns * thumb_width + (columns - 1) * gap
    height = header + margin + rows * (thumb_height + label_height) + (rows - 1) * gap + margin
    sheet = Image.new("RGB", (width, height), "#111318")
    draw = ImageDraw.Draw(sheet)
    title_font = load_font(30, bold=True)
    body_font = load_font(17)
    draw.text((margin, 22), "像素蛋挞 2.0.4 自动化逻辑 DPI 验收总览", fill="#F4F4F5", font=title_font)
    draw.text((margin, 59), "固定物理画布 2560×1440 · 125% / 150% / 200% · 非物理显示器人工截图", fill="#A8ABB2", font=body_font)

    for index, path in enumerate(files):
        row, column = divmod(index, columns)
        x = margin + column * (thumb_width + gap)
        y = header + margin + row * (thumb_height + label_height + gap)
        with Image.open(path) as image:
            image = image.convert("RGB")
            image.thumbnail((thumb_width, thumb_height), Image.Resampling.LANCZOS)
            frame = Image.new("RGB", (thumb_width, thumb_height), "#202226")
            frame.paste(image, ((thumb_width - image.width) // 2, (thumb_height - image.height) // 2))
            sheet.paste(frame, (x, y))
        draw.rectangle((x, y, x + thumb_width - 1, y + thumb_height - 1), outline="#303238", width=1)
        label = path.stem.replace("_Automated_", " · ").replace("_", " ")
        draw.text((x + 8, y + thumb_height + 10), label, fill="#D8DADE", font=body_font)

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output, format="PNG", optimize=True)


if __name__ == "__main__":
    main()
