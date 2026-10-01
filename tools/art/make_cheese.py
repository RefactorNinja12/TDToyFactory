"""
Draws the cheese art (then restyle.py maps it to the palette and outlines it):
  Resources/cheese.png       a wedge of cheese with holes (the deposit on the floor, and the raw cheese)
  Items/melted_cheese.png    a puddle of melted cheese with drips and a shine (the item on belts)
  Buildings/cheese_melter.png  the plastic melter recoloured: its molten plastic becomes molten cheese

    python tools/art/make_cheese.py && python tools/art/restyle.py
"""
import colorsys
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "source")

CHEESE = (244, 200, 82)
CHEESE_DARK = (206, 156, 52)
CHEESE_LIGHT = (255, 230, 140)
RIND = (226, 170, 60)


def wedge():
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((8, 46, 58, 58), fill=(0, 0, 0, 70))
    # top face (light) and front face (darker) of a wedge, seen a little from the front
    d.polygon([(10, 26), (54, 14), (56, 22), (12, 34)], fill=CHEESE_LIGHT)
    d.polygon([(12, 34), (56, 22), (56, 48), (12, 52)], fill=CHEESE)
    d.polygon([(10, 26), (12, 34), (12, 52), (8, 46)], fill=RIND)
    for x, y, r in ((24, 40, 4), (40, 34, 3), (46, 42, 3), (30, 30, 2), (44, 20, 2)):
        d.ellipse((x - r, y - r, x + r, y + r), fill=CHEESE_DARK)
    img.save(os.path.join(SRC, "Resources", "cheese.png"))


def puddle():
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((10, 34, 56, 54), fill=(0, 0, 0, 60))
    d.ellipse((8, 22, 56, 50), fill=CHEESE)
    d.ellipse((18, 16, 44, 36), fill=CHEESE)
    d.ellipse((12, 30, 52, 48), fill=CHEESE_DARK)
    d.ellipse((14, 26, 50, 44), fill=CHEESE)
    for x, y in ((16, 44), (30, 47), (44, 45)):  # drips
        d.ellipse((x - 3, y, x + 3, y + 7), fill=CHEESE_DARK)
    d.ellipse((22, 20, 32, 26), fill=CHEESE_LIGHT)  # shine
    d.ellipse((36, 30, 40, 33), fill=CHEESE_LIGHT)
    img.save(os.path.join(SRC, "Items", "melted_cheese.png"))


def melter():
    """The plastic melter, with every strongly coloured (non-green, non-grey) pixel turned cheese yellow."""
    img = Image.open(os.path.join(SRC, "Buildings", "extractor_plastic.png")).convert("RGBA")
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, l, s = colorsys.rgb_to_hls(r / 255, g / 255, b / 255)
            green = 0.2 < h < 0.45
            if s > 0.35 and not green:
                nr, ng, nb = colorsys.hls_to_rgb(0.12, l, min(1.0, s))
                px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    img.save(os.path.join(SRC, "Buildings", "cheese_melter.png"))


if __name__ == "__main__":
    wedge()
    puddle()
    melter()
    print("wrote cheese.png, melted_cheese.png, cheese_melter.png")
