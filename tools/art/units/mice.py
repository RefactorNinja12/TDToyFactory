"""
The mice seen from above (nose east, turned by the game), on two legs:
  builder       - hard hat, orange vest, hammer; act = hammering (back, forward)
  farmer        - straw hat, blue overalls, a carrot; act = picking (reach down, lift)
  scout         - green cap, binoculars; act = looking through them
  cheese_hunter - red headband, boxing gloves; act = punching (wind up, hit)
  samurai       - red kabuto with a gold crest, dark gi, katana; act = sword drawn back, swept across (slash)

    python tools/art/units/mice.py && python tools/art/restyle.py <the files it lists>
Writes Units/<name>_sheet.png (one row of 7 cells, UI/UnitSheets) and Units/<name>.png (idle, for the UI).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

WRITTEN = []
HAT = (74, 52, 64)
STEEL = (170, 176, 192)


def write(name, cell_fn):
    save(strip_sheet(cell_fn), f"Units/{name}_sheet.png")
    save(cell_fn("idle", 0), f"Units/{name}.png")
    WRITTEN.extend([f"Units/{name}_sheet.png", f"Units/{name}.png"])


# ---- hats from above, on the head centre (hx, hy), nose east ----

def hard_hat(d, hx, hy):
    d.ellipse((hx - 6, hy - 5, hx + 4, hy + 5), fill=tint(YELLOW, -0.25))
    d.ellipse((hx - 5, hy - 4, hx + 3, hy + 4), fill=YELLOW)
    d.line((hx - 4, hy, hx + 2, hy), fill=tint(YELLOW, 0.45))


def straw_hat(d, hx, hy):
    straw = (226, 190, 110)
    d.ellipse((hx - 8, hy - 8, hx + 6, hy + 8), fill=tint(straw, -0.15))
    d.ellipse((hx - 5, hy - 5, hx + 3, hy + 5), outline=RED, width=2)
    d.ellipse((hx - 3, hy - 3, hx + 1, hy + 3), fill=straw)


def cap(d, hx, hy):
    green = (80, 150, 90)
    d.ellipse((hx + 1, hy - 3, hx + 7, hy + 3), fill=tint(green, -0.3))   # the visor, forward
    d.ellipse((hx - 5, hy - 5, hx + 3, hy + 5), fill=green)
    d.ellipse((hx - 1, hy - 1, hx + 1, hy + 1), fill=tint(green, 0.3))


def headband(d, hx, hy):
    d.ellipse((hx - 7, hy - 7, hx + 6, hy + 6), outline=RED, width=2)
    d.line((hx - 7, hy, hx - 12, hy - 3), fill=RED, width=2)
    d.line((hx - 7, hy, hx - 12, hy + 2), fill=RED, width=2)




# ---- held things and actions (hand = (x, y) of the right hand, the south side) ----

def hammer(d, x, y, pose, step):
    forward = pose == "act" and step == 1
    tip = (x + 8, y) if not forward else (x + 6, y - 2)
    d.line((x, y, *tip), fill=WOOD, width=2)
    box(d, (tip[0] - 1, tip[1] - 4, tip[0] + 3, tip[1] + 4), GREY, radius=1, bevel=1)


def hammering(info):
    bx, cy = info["body"]
    return {"hand": (bx - 2, cy + 12)} if info["step"] == 0 else {"hand": (bx + 14, cy + 8)}


def carrot(d, x, y, pose, step):
    d.polygon([(x, y - 2), (x, y + 2), (x + 7, y)], fill=ORANGE)
    d.line((x, y, x - 3, y - 2), fill=GREEN)
    d.line((x, y, x - 3, y + 2), fill=GREEN)


def picking(info):
    bx, cy = info["body"]
    return {"hand": (bx + 16, cy + 6)} if info["step"] == 0 else {"hand": (bx + 6, cy + 11)}


def binoculars(d, x, y, pose, step):
    if pose != "act":
        d.rectangle((x - 2, y - 1, x + 3, y + 2), fill=DARK)


def looking(info):
    hx, hy = info["head"]
    return {"hand": (hx + 2, hy + 6), "back_hand": (hx + 2, hy - 6)}


def binocular_eyes(d, info):
    if info["pose"] != "act":
        return
    hx, hy = info["head"]
    for side in (-1, 1):
        d.ellipse((hx + 6, hy + 3 * side - 2, hx + 12, hy + 3 * side + 2), fill=DARK)


def glove(d, x, y, pose, step):
    disc(d, x, y, 4, RED)


def punching(info):
    bx, cy = info["body"]
    return {"hand": (bx + 2, cy + 10)} if info["step"] == 0 else {"hand": (bx + 19, cy + 4)}


def back_glove(d, info):
    bx, cy = info["body"]
    swing = {0: -3, 2: 3}.get(info["step"], 0) if info["pose"] == "walk" else 0
    disc(d, bx + 3 + swing, cy - 10, 4, RED)








KABUTO = (176, 44, 52)
GI = (70, 96, 176)
GOLD = (236, 186, 64)


def kabuto(d, hx, hy):
    # Over the back of the head (the face stays free): neck guard, dome, the crest's two gold horns.
    cx = hx - 4
    d.ellipse((cx - 7, hy - 8, cx + 4, hy + 8), fill=tint(KABUTO, -0.3))
    d.ellipse((cx - 5, hy - 6, cx + 4, hy + 6), fill=KABUTO)
    d.polygon([(cx + 2, hy - 2), (cx + 9, hy - 9), (cx + 6, hy - 1)], fill=GOLD)
    d.polygon([(cx + 2, hy + 2), (cx + 9, hy + 9), (cx + 6, hy + 1)], fill=GOLD)


def katana(d, x, y, pose, step):
    # Blade direction by pose: held forward, drawn back over the shoulder, swept across to the front.
    dx, dy = {("act", 0): (-6, -13), ("act", 1): (14, 7)}.get((pose, step), (14, 4))
    d.line((x - dx // 4, y - dy // 4, x, y), fill=INK, width=3)                # the grip
    d.line((x, y, x + dx, y + dy), fill=WHITE, width=3)                        # the blade
    d.line((x, y, x + dx, y + dy), fill=STEEL, width=1)


def swinging(info):
    bx, cy = info["body"]
    return {"hand": (bx + 5, cy - 9)} if info["step"] == 0 else {"hand": (bx + 13, cy + 7)}


def slash(d, info):
    if info["pose"] != "act" or info["step"] != 1:
        return
    hx, hy = info["head"]
    d.arc((hx - 2, hy - 17, hx + 14, hy + 17), start=-75, end=75, fill=WHITE, width=3)


def main():
    write("builder", lambda p, s: topdown_mouse(p, s, hat=hard_hat, shirt=ORANGE, held=hammer, act=hammering))
    write("farmer", lambda p, s: topdown_mouse(p, s, hat=straw_hat, shirt=BLUE, held=carrot, act=picking))
    write("scout", lambda p, s: topdown_mouse(p, s, hat=cap, shirt=(80, 150, 90), held=binoculars, act=looking,
                                              extra=binocular_eyes))
    write("cheese_hunter", lambda p, s: topdown_mouse(p, s, hat=headband, shirt=YELLOW, held=glove, act=punching,
                                                      extra=back_glove))
    write("samurai", lambda p, s: topdown_mouse(p, s, hat=kabuto, shirt=GI, held=katana, act=swinging, extra=slash))
    print(" ".join(WRITTEN))


if __name__ == "__main__":
    main()
