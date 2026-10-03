"""
The claw crane, seen from above (facing east before turning): a yellow cab on four stilts with a red winch,
the rail it runs out over the field (one piece per tile, with stilt feet beside it) and the claw on its
trolley, open and closed. View/CraneView lays the rail out and moves the claw.

    python tools/art/logistics/crane.py && python tools/art/restyle.py <the files it lists>
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

STEEL = (150, 160, 186)
WRITTEN = []


def out(img, rel):
    save(img, rel)
    WRITTEN.append(rel)


def stilt(d, x, y):
    """A stilt seen from above: a little square post with a darker foot."""
    box(d, (x - 3, y - 3, x + 3, y + 3), tint(STEEL, -0.15), radius=1, bevel=1)


def beams(d, x0, x1):
    """The rail: two steel beams with ties between them, along y = 26 and 38."""
    for y in (25, 37):
        d.rectangle((x0, y, x1, y + 2), fill=STEEL)
        d.line((x0, y, x1, y), fill=tint(STEEL, 0.4))
    for x in range(x0 + 4, x1, 10):
        d.line((x, 27, x, 37), fill=tint(STEEL, -0.3), width=2)


def base():
    img, d = canvas(64, 64)
    for x, y in ((10, 10), (54, 10), (10, 54), (54, 54)):
        stilt(d, x, y)
    beams(d, 40, 63)                                   # the rail starts from the front of the cab
    box(d, (12, 14, 44, 50), YELLOW, radius=4)         # the cab
    studs(d, (14, 16, 30, 48), YELLOW, step=8, r=2)
    disc(d, 36, 32, 7, RED)                            # the winch drum
    d.line((29, 32, 43, 32), fill=tint(RED, -0.4), width=2)
    # the output is at the back (west): a little striped hatch on that side instead
    d.rectangle((0, 25, 5, 39), fill=tint(YELLOW, -0.25))
    for y in range(26, 39, 4):
        d.line((1, y, 4, y + 2), fill=INK)
    out(img, "Buildings/claw_crane.png")


def rail():
    img, d = canvas(64, 64)
    beams(d, 0, 63)
    for y in (18, 46):
        stilt(d, 32, y)                                # the stilts beside the rail, mid tile
    out(img, "Parts/crane_rail.png")


def claw(closed):
    img, d = canvas(28, 28)
    box(d, (8, 6, 20, 22), tint(STEEL, -0.1), radius=2, bevel=1)   # the trolley on the rail
    d.line((14, 14, 14, 14), fill=INK)
    spread = 4 if closed else 10
    for dx, dy in ((spread, 0), (-spread // 2, spread - 2), (-spread // 2, -(spread - 2))):
        d.line((14, 14, 14 + dx, 14 + dy), fill=tint(STEEL, 0.3), width=3)
        disc(d, 14 + dx, 14 + dy, 2, tint(STEEL, -0.35), shine=False)
    disc(d, 14, 14, 3, RED, shine=False)
    out(img, f"Parts/claw_{'closed' if closed else 'open'}.png")


if __name__ == "__main__":
    base()
    rail()
    claw(False)
    claw(True)
    print(" ".join(WRITTEN))
