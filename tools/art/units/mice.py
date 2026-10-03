"""
The worker mice and the cheese hunter as upright sprite sheets (UI/UnitSheets layout):
  builder       - hard hat, orange vest, hammer; act = hammering (up, down)
  farmer        - straw hat, blue overalls, a carrot; act = picking (down, up)
  scout         - green cap, binoculars on a strap; act = looking through them
  cheese_hunter - red headband, boxing gloves; act = punching (wind up, hit)

    python tools/art/units/mice.py && python tools/art/restyle.py <the files it lists>
Writes Units/<name>_sheet.png (7x3 cells of 48 px) and Units/<name>.png (the idle front cell, for the UI).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

WRITTEN = []


def write(name, cell_fn):
    sheet = unit_sheet(cell_fn)
    save(sheet, f"Units/{name}_sheet.png")
    save(cell_fn("toward", "idle", 0), f"Units/{name}.png")
    WRITTEN.extend([f"Units/{name}_sheet.png", f"Units/{name}.png"])


# ---- hats: drawn on the head centre (hx, hy) ----

def hard_hat(d, hx, hy, facing):
    if facing == "side":
        d.chord((hx - 9, hy - 12, hx + 7, hy + 2), 180, 360, fill=YELLOW)
        d.rectangle((hx - 9, hy - 5, hx + 11, hy - 4), fill=tint(YELLOW, -0.25))
    else:
        d.chord((hx - 9, hy - 13, hx + 9, hy + 3), 180, 360, fill=YELLOW)
        d.rectangle((hx - 11, hy - 5, hx + 11, hy - 4), fill=tint(YELLOW, -0.25))
        d.line((hx, hy - 12, hx, hy - 6), fill=tint(YELLOW, 0.4))


def straw_hat(d, hx, hy, facing):
    straw = (226, 190, 110)
    d.ellipse((hx - 14, hy - 9, hx + 14, hy - 2), fill=tint(straw, -0.15))
    d.chord((hx - 7, hy - 15, hx + 7, hy - 1), 180, 360, fill=straw)
    d.rectangle((hx - 7, hy - 9, hx + 7, hy - 7), fill=RED)


def cap(d, hx, hy, facing):
    green = (80, 150, 90)
    d.chord((hx - 9, hy - 12, hx + 9, hy + 2), 180, 360, fill=green)
    if facing == "side":
        d.rectangle((hx + 4, hy - 6, hx + 12, hy - 4), fill=tint(green, -0.3))
    elif facing == "toward":
        d.ellipse((hx - 7, hy - 7, hx + 7, hy - 3), fill=tint(green, -0.3))
    d.ellipse((hx - 1, hy - 13, hx + 1, hy - 11), fill=tint(green, 0.3))


def headband(d, hx, hy, facing):
    d.rectangle((hx - 9, hy - 6, hx + 8, hy - 4), fill=RED)
    if facing != "toward":
        tail_x = hx - 9 if facing == "side" else hx + 2
        d.line((tail_x, hy - 5, tail_x - 4, hy), fill=RED, width=2)


# ---- held things and actions ----

def hammer(d, x, y, facing, pose, step):
    up = pose == "act" and step == 0
    head = (x + 1, y - 7) if up else (x + 4, y - 3)
    d.line((x, y, head[0], head[1]), fill=WOOD, width=2)
    box(d, (head[0] - 3, head[1] - 3, head[0] + 3, head[1] + 1), GREY, radius=1, bevel=1)


def hammering(info):
    hx, hy = info["head"]
    return {"hand": (hx + 8, hy - 4)} if info["step"] == 0 else {"hand": (info["cx"] + 11, info["hip"] - 1)}


def carrot(d, x, y, facing, pose, step):
    d.polygon([(x - 1, y), (x + 3, y), (x + 1, y + 6)], fill=ORANGE)
    d.line((x + 1, y, x - 1, y - 3), fill=GREEN)
    d.line((x + 1, y, x + 3, y - 3), fill=GREEN)


def picking(info):
    return {"hand": (info["cx"] + 6, FEET - 3)} if info["step"] == 0 else {"hand": (info["cx"] + 8, info["hip"] - 10)}


def binoculars(d, x, y, facing, pose, step):
    if pose == "act":
        return  # drawn at the eyes by `looking`
    d.rectangle((x - 2, y - 1, x + 3, y + 2), fill=DARK)


def looking(info):
    hx, hy = info["head"]
    return {"hand": (hx + 6, hy + 2), "back_hand": (hx - 6, hy + 2)}


def binocular_eyes(d, info):
    if info["pose"] != "act" or info["facing"] == "away":
        return
    hx, hy = info["head"]
    xs = (hx + 6, hx + 10) if info["facing"] == "side" else (hx - 4, hx + 4)
    for x in xs:
        d.ellipse((x - 3, hy - 4, x + 3, hy + 2), fill=DARK)
        d.ellipse((x - 1, hy - 2, x + 1, hy), fill=(120, 190, 240))


def glove(d, x, y, facing, pose, step):
    disc(d, x, y, 4, RED)


def punching(info):
    cx, hip = info["cx"], info["hip"]
    if info["facing"] == "side":
        return {"hand": (cx + 4, hip - 9)} if info["step"] == 0 else {"hand": (cx + 16, hip - 8)}
    return {"hand": (cx + 5, hip - 9)} if info["step"] == 0 else {"hand": (cx + 2, hip - 4)}


def back_glove(d, info):
    # the other glove: on the back hand (the kit only draws the front one)
    if info["facing"] == "side":
        return
    cx, hip = info["cx"], info["hip"]
    swing = {"walk": 0}.get(info["pose"], 0)
    disc(d, cx - 9, hip - 3 + swing, 4, RED)


def main():
    write("builder", lambda f, p, s: upright_mouse(f, p, s, hat=hard_hat, shirt=ORANGE, held=hammer, act=hammering))
    write("farmer", lambda f, p, s: upright_mouse(f, p, s, hat=straw_hat, shirt=BLUE, held=carrot, act=picking))
    write("scout", lambda f, p, s: upright_mouse(f, p, s, hat=cap, shirt=(80, 150, 90), held=binoculars, act=looking,
                                                 extra=binocular_eyes))
    write("cheese_hunter", lambda f, p, s: upright_mouse(f, p, s, hat=headband, shirt=YELLOW, held=glove, act=punching,
                                                         extra=back_glove))
    print(" ".join(WRITTEN))


if __name__ == "__main__":
    main()
