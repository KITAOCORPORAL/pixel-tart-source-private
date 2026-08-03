from pathlib import Path
import sys

from PIL import Image, ImageDraw, ImageFont


def main() -> int:
    if len(sys.argv) != 3:
        raise SystemExit("Usage: create_ui_contact_sheet.py <screenshot-directory> <output-file>")

    source = Path(sys.argv[1])
    output = Path(sys.argv[2])
    images = sorted(source.glob("[0-9][0-9]_*.png"))
    if not images:
        raise RuntimeError("No UI screenshots found")

    columns = 3
    rows = (len(images) + columns - 1) // columns
    thumb_width, thumb_height = 520, 300
    label_height, margin = 32, 14
    sheet = Image.new("RGB", (columns * thumb_width + (columns + 1) * margin,
                               rows * (thumb_height + label_height) + (rows + 1) * margin), "#111216")
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()

    for index, image_path in enumerate(images):
        row, column = divmod(index, columns)
        left = margin + column * (thumb_width + margin)
        top = margin + row * (thumb_height + label_height + margin)
        with Image.open(image_path) as image:
            preview = image.convert("RGB")
            preview.thumbnail((thumb_width, thumb_height), Image.Resampling.LANCZOS)
            paste_left = left + (thumb_width - preview.width) // 2
            paste_top = top + (thumb_height - preview.height) // 2
            sheet.paste(preview, (paste_left, paste_top))
        draw.text((left + 4, top + thumb_height + 8), image_path.name, fill="#F4F4F5", font=font)

    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output, optimize=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
