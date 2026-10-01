"""
Draws the lollipop obstacle: a big swirl lollipop lying flat on the floor, seen from straight above:
a round candy with a spiral of stripes and a sticky shine, and a paper stick.

    python tools/art/make_lollipop.py && python tools/art/restyle.py

Writes tools/art/source/Obstacles/lollipop.png (320x256: the toy covers 5x3 tiles; ObstacleView draws
every toy one tile taller, so it sits in the bottom 192 px). restyle.py maps it to the game palette and
adds the black outline.
"""
import math
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "source", "Obstacles", "lollipop.png")

STRIPES = [(232, 104, 112), (236, 216, 186), (190, 58, 78), (236, 216, 186)]  # pink, cream, red, cream
RIM = (150, 44, 64)
SHINE = (250, 240, 226)
STICK = (226, 220, 208)
STICK_DARK = (170, 164, 156)

CX, CY, R = 100, 160, 84          # the candy
STICK_Y0, STICK_Y1, STICK_X1 = 150, 170, 312


def main():
    img = Image.new("RGBA", (320, 256), (0, 0, 0, 0))
    px = img.load()

    def candy(x, y):
        return (x - CX) ** 2 + (y - CY) ** 2 <= R * R

    def stick(x, y):
        if not (CX < x <= STICK_X1 and STICK_Y0 <= y <= STICK_Y1):
            return False
        # rounded end
        return x < STICK_X1 - 10 or (x - (STICK_X1 - 10)) ** 2 + (y - (STICK_Y0 + STICK_Y1) / 2) ** 2 <= 100

    # thin flat shadow under both
    for y in range(256):
        for x in range(320):
            if not (candy(x, y) or stick(x, y)) and x >= 2 and y >= 3 and (candy(x - 2, y - 3) or stick(x - 2, y - 3)):
                px[x, y] = (0, 0, 0, 80)

    for y in range(256):
        for x in range(320):
            if stick(x, y) and not candy(x, y):
                c = STICK_DARK if y >= STICK_Y1 - 4 else STICK
                if (x // 14) % 2 == 0 and y == STICK_Y0 + 3:
                    c = SHINE  # glossy paper
                px[x, y] = (*c, 255)
            if candy(x, y):
                dx, dy = x - CX, y - CY
                r = math.hypot(dx, dy)
                # a spiral: the stripe index turns with the angle and moves out with the radius
                a = (math.atan2(dy, dx) + math.pi) / (2 * math.pi)
                t = (a * 4 + r / 22.0) % 1.0
                c = STRIPES[int(t * len(STRIPES)) % len(STRIPES)]
                if r > R - 5:
                    c = RIM
                # sticky shine: a light crescent up to the left
                sx, sy = x - (CX - 30), y - (CY - 34)
                if 150 <= sx * sx + sy * sy <= 420 and sx + sy < -6:  # only the upper-left half: a crescent
                    c = SHINE
                px[x, y] = (*c, 255)
    img.save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
