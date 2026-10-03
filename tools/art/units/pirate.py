"""
The pirate mouse (UnitType.PlasticSoldier, "Piratmus"): tricorn hat, eye patch, striped shirt, belt and a
flintlock pistol. Act = aim, then fire (a puff at the muzzle).

    python tools/art/units/pirate.py && python tools/art/restyle.py <the files it lists>
Writes Units/pirate_sheet.png and Units/pirate.png (UI icon).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

HAT = (74, 52, 64)
STEEL = (170, 176, 192)


def tricorn(d, hx, hy, facing):
    if facing == "side":
        d.polygon([(hx - 11, hy - 6), (hx - 4, hy - 15), (hx + 6, hy - 13), (hx + 12, hy - 5)], fill=HAT)
    else:
        d.polygon([(hx - 13, hy - 7), (hx - 6, hy - 15), (hx + 6, hy - 15), (hx + 13, hy - 7), (hx, hy - 4)], fill=HAT)
        disc(d, hx, hy - 10, 2, WHITE, shine=False)   # the skull badge
    d.line((hx - 10, hy - 6, hx + 10, hy - 6), fill=tint(YELLOW, -0.2))


def pistol(d, x, y, facing, pose, step):
    aiming = pose == "act"
    if facing == "side":
        d.rectangle((x - 1, y, x + 1, y + 4), fill=WOOD)            # grip
        d.rectangle((x, y - 2, x + 8, y), fill=STEEL)               # barrel, pointing ahead
    elif aiming and facing == "toward":
        d.rectangle((x - 2, y - 1, x + 2, y + 3), fill=WOOD)
        disc(d, x, y + 1, 2, STEEL, shine=False)                    # the muzzle, at the camera
    else:
        d.rectangle((x - 1, y - 1, x + 1, y + 2), fill=WOOD)
        d.rectangle((x, y + 2, x + 2, y + 8), fill=STEEL)           # hanging down at the side


def aiming(info):
    cx, hip = info["cx"], info["hip"]
    if info["facing"] == "side":
        return {"hand": (cx + 12, hip - 9)}
    if info["facing"] == "toward":
        return {"hand": (cx + 5, hip - 6)}
    return {"hand": (cx + 7, hip - 11)}


def patch_and_puff(d, info):
    hx, hy = info["head"]
    facing = info["facing"]
    if facing == "toward":
        d.line((hx - 8, hy - 6, hx + 7, hy + 1), fill=INK)
        d.ellipse((hx + 2, hy - 3, hx + 6, hy + 1), fill=INK)
    elif facing == "side":
        d.ellipse((hx + 2, hy - 5, hx + 6, hy - 1), fill=INK)
        d.line((hx - 6, hy - 7, hx + 4, hy - 3), fill=INK)
    # belt with a gold buckle
    cx, hip = info["cx"], info["hip"]
    d.rectangle((cx - 7, hip - 4, cx + 6, hip - 3), fill=WOOD)
    if facing == "toward":
        d.rectangle((cx - 2, hip - 5, cx + 1, hip - 2), fill=YELLOW)
    # the shot: a puff at the muzzle
    if info["pose"] == "act" and info["step"] == 1:
        x, y = info["hand"]
        px, py = (x + 11, y - 2) if facing == "side" else (x, y + 6) if facing == "toward" else (x, y - 6)
        for dx, dy, r in ((0, 0, 3), (2, -2, 2), (-2, -1, 2)):
            d.ellipse((px + dx - r, py + dy - r, px + dx + r, py + dy + r), fill=WHITE)


def main():
    cell = lambda f, p, s: upright_mouse(f, p, s, hat=tricorn, shirt=WHITE, stripes=RED, held=pistol, act=aiming,  # noqa: E731
                                         extra=patch_and_puff)
    save(unit_sheet(cell), "Units/pirate_sheet.png")
    save(cell("toward", "idle", 0), "Units/pirate.png")
    print("Units/pirate_sheet.png Units/pirate.png")


if __name__ == "__main__":
    main()
