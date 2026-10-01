"""
One small review picture of finished sprites: each picture at game size and twice as big, with its name.
Reads the restyled sprites (what the game shows).

    python tools/art/sheet.py OUT.png Buildings/assembler.png Parts/key.png ...
    python tools/art/sheet.py OUT.png --since 600   (everything restyled in the last 600 s)
"""
import os
import sys
import time
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SPRITES = os.path.join(ROOT, "factory-td", "Assets", "Sprites")
FLOOR = (58, 44, 38, 255)


def collect(args):
    if args and args[0] == "--since":
        cutoff = time.time() - float(args[1])
        found = []
        for folder, _, files in os.walk(SPRITES):
            for name in files:
                path = os.path.join(folder, name)
                if name.endswith(".png") and os.path.getmtime(path) >= cutoff:
                    found.append(os.path.relpath(path, SPRITES))
        return sorted(found)
    return args


def main():
    out, names = sys.argv[1], collect(sys.argv[2:])
    images = [(n, Image.open(os.path.join(SPRITES, n)).convert("RGBA")) for n in names]
    cells = [max(64, img.width * 2) + 8 for _, img in images]
    width = min(1100, max(200, sum(c + img.width + 8 for c, (_, img) in zip(cells, images))))
    rows, x, y, row_h, placed = [], 4, 4, 0, []
    for cell, (name, img) in zip(cells, images):
        need = img.width + 8 + img.width * 2 + 8
        if x + need > width:
            x, y, row_h = 4, y + row_h + 16, 0
        placed.append((x, y, name, img))
        x += need
        row_h = max(row_h, img.height * 2)
    sheet = Image.new("RGBA", (width, y + row_h + 20), FLOOR)
    d = ImageDraw.Draw(sheet)
    for x, y, name, img in placed:
        sheet.alpha_composite(img, (x, y))
        big = img.resize((img.width * 2, img.height * 2), Image.NEAREST)
        sheet.alpha_composite(big, (x + img.width + 6, y))
        d.text((x, y + img.height * 2 + 2), os.path.splitext(os.path.basename(name))[0][:22], fill=(230, 230, 240, 255))
    sheet.save(out)
    print(len(images), "pictures ->", out, sheet.size)


if __name__ == "__main__":
    main()
