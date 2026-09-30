"""
Draws the sock obstacle: a plain one-colour knitted sock lying flat on the floor, seen from straight
above (no front side showing), with a knitted texture (columns of small stitches pointing towards the
toe), a ribbed cuff and seams at the heel and toe.

    python tools/art/make_sock.py && python tools/art/restyle.py

Writes tools/art/source/Obstacles/sock.png (320x192: the toy covers 5x2 tiles; ObstacleView draws every
toy one tile taller, so the sock sits in the bottom 128 px and the top row stays empty). restyle.py
then maps it to the game palette and adds the black outline. The three wool shades are picked so they
land on three different palette colours, which keeps the knit visible.
"""
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "source", "Obstacles", "sock.png")

WOOL = (92, 118, 184)      # the one colour: blue wool
DARK = (58, 74, 128)       # the gaps between stitches
LIGHT = (128, 156, 214)    # the tops of the stitches
SEAM = (44, 54, 96)


def shape():
    """The flat sock's silhouette: leg to the left, the top sloping down over the instep to the toe."""
    mask = Image.new("L", (320, 192), 0)
    d = ImageDraw.Draw(mask)
    d.polygon([(22, 70), (150, 72), (200, 80), (240, 96), (276, 108), (276, 184), (170, 184),
               (150, 150), (22, 148)], fill=255)
    d.ellipse((12, 70, 34, 148), fill=255)       # rounded cuff end
    d.ellipse((136, 118, 210, 186), fill=255)    # heel
    d.ellipse((236, 104, 316, 186), fill=255)    # toe
    return mask


def main():
    mask = shape()
    m = mask.load()
    img = Image.new("RGBA", (320, 192), (0, 0, 0, 0))
    px = img.load()

    # A thin flat shadow: it lies right on the floor.
    for y in range(192):
        for x in range(320):
            if not m[x, y] and x >= 2 and y >= 3 and m[x - 2, y - 3]:
                px[x, y] = (0, 0, 0, 80)

    for y in range(192):
        for x in range(320):
            if not m[x, y]:
                continue
            if x < 50:
                # ribbed cuff: vertical ribs
                r = x % 5
                c = DARK if r == 0 else LIGHT if r in (2, 3) else WOOL
            else:
                # knit stitches: chevrons 6 px long and 8 px tall, pointing towards the toe (right)
                u, v = x % 6, y % 8
                bend = abs(v * 2 - 7) // 2          # 3,2,1,0,0,1,2,3
                if u == bend:
                    c = DARK
                elif u == bend + 1 or u == bend + 2:
                    c = LIGHT
                else:
                    c = WOOL
            px[x, y] = (*c, 255)

    d = ImageDraw.Draw(img)
    d.line((50, 72, 50, 146), fill=(*SEAM, 255), width=2)                  # top of the cuff
    d.arc((142, 122, 204, 182), 100, 250, fill=(*SEAM, 255), width=2)     # heel seam
    d.arc((242, 110, 310, 182), 110, 250, fill=(*SEAM, 255), width=2)     # toe seam
    img.save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
