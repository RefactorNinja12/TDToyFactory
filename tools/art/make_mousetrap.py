"""
Draws the mousetrap obstacle: a big wooden mousetrap lying flat on the floor, seen from above: a wooden
board with grain, the metal snap bar and coil springs, and a wedge of cheese on the trigger plate.

    python tools/art/make_mousetrap.py && python tools/art/restyle.py

Writes tools/art/source/Obstacles/mousetrap.png (192x192: the toy covers 3x2 tiles; ObstacleView draws
every toy one tile taller, so it sits in the bottom 128 px). restyle.py maps it to the game palette and
adds the black outline.
"""
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "source", "Obstacles", "mousetrap.png")

WOOD = (200, 152, 96)
WOOD_DARK = (156, 110, 66)
WOOD_LIGHT = (222, 182, 126)
METAL = (196, 200, 212)
METAL_DARK = (112, 118, 136)
CHEESE = (242, 200, 84)
CHEESE_DARK = (204, 156, 54)
PLATE = (170, 172, 184)


def main():
    img = Image.new("RGBA", (192, 192), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    board = (8, 72, 184, 184)

    # thin flat shadow, then the board
    d.rounded_rectangle((11, 76, 187, 188), radius=8, fill=(0, 0, 0, 80))
    d.rounded_rectangle(board, radius=8, fill=WOOD)
    px = img.load()
    for y in range(board[1], board[3] + 1):
        for x in range(board[0], board[2] + 1):
            if px[x, y][3] == 255 and px[x, y][:3] == WOOD:
                # grain along the board, and a lit top edge
                if (y * 7 + (x // 23) * 3) % 11 == 0:
                    px[x, y] = (*WOOD_DARK, 255)
                elif y < board[1] + 4:
                    px[x, y] = (*WOOD_LIGHT, 255)

    # the snap bar: a wire loop over the left half, held by two coil springs in the middle
    d.rectangle((24, 88, 96, 168), outline=METAL, width=4)
    d.line((96, 88, 96, 168), fill=METAL_DARK, width=2)
    for cy in (104, 152):
        d.ellipse((90, cy - 12, 114, cy + 12), outline=METAL, width=3)
        d.ellipse((96, cy - 6, 108, cy + 6), outline=METAL_DARK, width=2)
    # the hold-down wire to the trigger
    d.line((108, 128, 140, 128), fill=METAL, width=3)

    # trigger plate with a wedge of cheese
    d.rounded_rectangle((132, 104, 176, 152), radius=4, fill=PLATE)
    d.polygon([(138, 146), (172, 146), (172, 112)], fill=CHEESE)
    d.line((138, 146, 172, 112), fill=CHEESE_DARK, width=2)
    for hx, hy in ((160, 136), (166, 124), (152, 141)):
        d.ellipse((hx - 3, hy - 3, hx + 3, hy + 3), fill=CHEESE_DARK)
    img.save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
