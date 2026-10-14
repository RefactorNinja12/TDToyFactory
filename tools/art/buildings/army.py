"""
Army factories (2x2 = 128 px, output east):
  soldier - a toy pirate ship seen from the side, sails full of wind, bobbing on the water; cannons puff while it builds
  golem   - a toy crane stacking ABC blocks: the hook lowers a block, a mouse swings a mallet
  car     - a slot-car track round a paint booth: two cars lap it, a mouse waves the chequered flag
  dojo    - a pagoda seen from the side (three curved red roofs, gold spire) on a gravel yard; parts: the pagoda, the
            sensei mouse by the door (looks right and left, shouts now and then), a trainee, lanterns, the sliding door
  wizard  - a tall tower of stacked toy blocks seen from the side, a pointy roof with a star on top, on a mossy yard;
            parts: the tower, the star (turns), sparks between the roof points, a flickering window, a flash at the door

    python tools/art/buildings/army.py && python tools/art/restyle.py <the files it lists>
"""
import os
import sys

from PIL import ImageFont

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

WRITTEN = []


def out(img, rel):
    save(img, rel)
    WRITTEN.append(rel)


def _billow(d, x, top, bottom, width, bulge, color):
    """A square sail full of wind from behind (the left): the right edge bulges out in a curve."""
    pts = [(x, top), (x + width, top)]
    steps = 8
    for k in range(1, steps):
        t = k / steps
        pts.append((x + width + bulge * 4 * t * (1 - t), top + (bottom - top) * t))
    pts += [(x + width, bottom), (x, bottom)]
    d.polygon(pts, fill=tint(color, -0.15))
    d.polygon([(px - 2 if i > 1 and i < len(pts) - 2 else px, py) for i, (px, py) in enumerate(pts)], fill=color)
    d.line((x - 2, top, x + width + 2, top), fill=tint(WOOD, -0.3), width=2)       # the yard
    d.line((x - 2, bottom, x + width + 2, bottom), fill=tint(WOOD, -0.3), width=1)


def soldier_factory():
    """
    The pirate ship the plastic pirate mice are built in. The base is the water with a little pier at the
    output (it turns with the building); the ship itself is a part seen from the side, bow to the right,
    sails full of wind, floating up and down (it never turns, only mirrors when facing west).
    """
    img, d = canvas(128, 128)
    water = (70, 130, 200)
    baseplate(img, d, color=water)
    for x, y in ((10, 12), (44, 8), (92, 14), (14, 112), (64, 118), (100, 106), (8, 60), (110, 84)):
        d.arc((x, y, x + 12, y + 6), 200, 340, fill=tint(water, 0.35), width=1)
    box(d, (106, 56, 127, 72), tint(WOOD, 0.1), radius=1, bevel=1)          # the pier to the output
    for x in (110, 116, 122):
        d.line((x, 57, x, 71), fill=tint(WOOD, -0.25))
    chute(d, 128, 128)
    out(img, "Buildings/factory_soldier.png")

    ship, s_ = part(116, 100)
    sail = (240, 236, 222)
    # masts (behind the sails) and the crow's nest
    s_.line((54, 64, 54, 4), fill=tint(WOOD, -0.35), width=3)
    s_.line((82, 64, 82, 20), fill=tint(WOOD, -0.35), width=3)
    box(s_, (48, 14, 60, 19), tint(WOOD, -0.1), radius=1, bevel=1)
    # the flag streaming in the wind, then the sails
    s_.polygon([(55, 2), (66, 0), (72, 4), (66, 8), (55, 8)], fill=(60, 50, 60))
    s_.point((61, 4), fill=WHITE)
    _billow(s_, 40, 22, 54, 26, 8, sail)
    _billow(s_, 72, 28, 52, 18, 6, sail)
    disc(s_, 54, 37, 5, INK, shine=False)                        # skull on the main sail
    s_.line((48, 45, 60, 49), fill=INK, width=2)
    s_.line((48, 49, 60, 45), fill=INK, width=2)
    # the jib from the fore mast to the bowsprit
    s_.line((100, 58, 114, 50), fill=tint(WOOD, -0.3), width=2)
    s_.polygon([(84, 24), (112, 50), (86, 54)], fill=sail)
    # hull: stern castle on the left, pointed bow on the right
    hull = [(6, 46), (30, 46), (32, 60), (100, 58), (108, 56), (98, 72), (86, 84), (28, 86), (14, 76), (8, 62)]
    s_.polygon(hull, fill=tint(WOOD, -0.25))
    s_.polygon([(8, 48), (29, 48), (31, 61), (99, 60), (104, 58), (96, 70), (85, 81), (29, 83), (16, 74), (10, 62)],
               fill=WOOD)
    for y in (66, 72, 78):
        s_.line((16, y, 94 - (y - 66), y), fill=tint(WOOD, -0.2))
    s_.line((10, 62, 104, 60), fill=YELLOW, width=1)               # gold trim along the rail
    s_.line((14, 68, 98, 66), fill=RED, width=3)                   # the red stripe
    for x in (40, 56, 72):
        s_.ellipse((x - 4, 71, x + 4, 79), fill=INK)               # cannon ports
        s_.ellipse((x - 2, 73, x + 2, 77), fill=GREY)
    for x in (12, 20):
        s_.rectangle((x, 51, x + 4, 56), fill=YELLOW)              # lit stern windows
    # the pirate mouse at the wheel on the stern castle
    disc(s_, 24, 41, 4, MOUSE, shine=False)
    for ex in (21, 27):
        s_.ellipse((ex - 2, 35, ex + 2, 39), fill=MOUSE)
    s_.polygon([(18, 38), (24, 33), (30, 38), (24, 40)], fill=(60, 50, 60))
    s_.ellipse((33, 41, 41, 49), outline=tint(WOOD, -0.35), width=2)  # the wheel
    out(ship, "Parts/pirate_ship.png")

    front, f = part(124, 16)            # water in front of the hull's bottom
    f.rectangle((0, 6, 123, 15), fill=water)
    for x in range(0, 124, 12):
        f.arc((x, 2, x + 12, 10), 180, 360, fill=tint(water, 0.4), width=2)
    out(front, "Parts/ship_water.png")


def abc_block(d, x, y, size, color, letter):
    box(d, (x, y, x + size, y + size), color, radius=2)
    font = ImageFont.load_default()
    d.text((x + size // 2 - 3, y + size // 2 - 6), letter, fill=WHITE, font=font)


def golem_workshop():
    img, d = canvas(128, 128)
    baseplate(img, d)
    chute(d, 128, 128)
    # the stack the crane builds
    abc_block(d, 50, 74, 22, RED, "A")
    abc_block(d, 74, 74, 22, BLUE, "B")
    abc_block(d, 98, 86, 16, GREEN, "D")
    # the crane: a lattice mast in the corner, the boom along the top
    box(d, (12, 12, 26, 110), YELLOW, radius=2)
    for y in range(16, 108, 8):
        d.line((14, y, 24, y + 6), fill=tint(YELLOW, -0.4))
    box(d, (12, 12, 104, 22), YELLOW, radius=2)
    for x in range(16, 102, 8):
        d.line((x, 14, x + 6, 20), fill=tint(YELLOW, -0.4))
    box(d, (66, 22, 80, 28), tint(GREY, -0.1), radius=1, bevel=1)   # the trolley
    # the builder mouse with its mallet (a part)
    mouse(img, 38, 100, 1.4, hat="hard", facing=-45)
    out(img, "Buildings/factory_golem.png")

    hook, h = part(24, 40)              # rope from the top (under the trolley), the block at the bottom
    h.line((12, 0, 12, 18), fill=tint(GREY, 0.2), width=2)
    h.polygon([(8, 18), (16, 18), (12, 22)], fill=GREY)
    abc_block(h, 3, 21, 18, YELLOW, "C")
    out(hook, "Parts/crane_hook.png")

    mallet, m = part(24, 24)            # centre = the mouse's paw
    m.line((12, 12, 12, 2), fill=WOOD, width=2)
    box(m, (6, 0, 18, 6), RED, radius=1, bevel=1)
    out(mallet, "Parts/mallet.png")


def car_factory():
    img, d = canvas(128, 128)
    baseplate(img, d)
    chute(d, 128, 128)
    # the oval track with kerbs and a dashed middle line
    d.ellipse((10, 18, 118, 110), fill=tint(GREY, -0.35))
    d.ellipse((30, 38, 98, 90), fill=BASE)
    for i in range(0, 360, 20):
        d.arc((12, 20, 116, 108), i, i + 10, fill=RED, width=2)
    d.arc((20, 28, 108, 100), 0, 360, fill=WHITE, width=1)
    # the paint booth in the middle, a chequered finish flag stand
    box(d, (44, 48, 84, 80), BLUE, radius=3)
    d.rectangle((50, 54, 78, 74), fill=tint(BLUE, 0.35))
    d.line((52, 64, 76, 64), fill=WHITE, width=2)
    mouse(img, 112, 104, 1.3, hat="cap", facing=-135, hat_color=RED)
    out(img, "Buildings/factory_car.png")

    for name, color in (("race_car_red", RED), ("race_car_yellow", YELLOW)):
        car, c = part(18, 12)           # pointing east (the way it drives)
        box(c, (1, 1, 16, 10), color, radius=3, bevel=1)
        c.rectangle((7, 3, 11, 8), fill=(120, 190, 240))
        for x, y in ((3, 0), (13, 0), (3, 10), (13, 10)):
            c.rectangle((x - 1, y, x + 1, y + 1), fill=INK)
        out(car, f"Parts/{name}.png")

    flag, f = part(20, 20)              # centre = the paw holding the stick
    f.line((10, 10, 10, 1), fill=GREY, width=1)
    for i in range(3):
        for j in range(2):
            f.rectangle((11 + i * 2, 1 + j * 2, 12 + i * 2, 2 + j * 2), fill=WHITE if (i + j) % 2 else INK)
    out(flag, "Parts/race_flag.png")


LACQUER = (196, 52, 52)
ROBE = (90, 70, 120)


GRAVEL = (170, 160, 140)
STONE = (150, 150, 160)
PAPER = (240, 232, 210)


def eave(d, cx, top, half_top, half_base, base, tip_lift=5):
    """A curved pagoda roof from the side: narrow at the top, sweeping out to upturned tips at the eave."""
    dark = tint(LACQUER, -0.35)
    pts = [(cx - half_top, top), (cx + half_top, top), (cx + half_base - 4, base - 3), (cx + half_base + 2, base - 3 - tip_lift),
           (cx + half_base, base), (cx - half_base, base), (cx - half_base - 2, base - 3 - tip_lift), (cx - half_base + 4, base - 3)]
    d.polygon(pts, fill=dark)
    d.polygon([(cx - half_top, top + 1), (cx + half_top, top + 1), (cx + half_base - 5, base - 4), (cx - half_base + 5, base - 4)],
              fill=LACQUER)
    d.line((cx - half_base, base - 1, cx + half_base, base - 1), fill=YELLOW, width=2)   # gold trim along the eave
    for x in (cx - half_base - 1, cx + half_base + 1):
        disc(d, x, base - 4 - tip_lift, 2, YELLOW, shine=False)


def storey(d, x0, y0, x1, y1):
    """One floor's wall from the side: red pillars with paper screens between them."""
    d.rectangle((x0, y0, x1, y1), fill=PAPER)
    for x in range(x0, x1 + 1, max(6, (x1 - x0) // 4)):
        d.rectangle((x, y0, x + 2, y1), fill=tint(LACQUER, -0.15))
    d.rectangle((x1 - 2, y0, x1, y1), fill=tint(LACQUER, -0.15))


def profile_mouse(robe, held=None, beard=False, w=22, h=30):
    """A mouse standing upright seen from the side, facing right: robe, head with an ear, snout, pink nose."""
    img, d = canvas(w, h)
    d.line((6, h - 9, 1, h - 4), fill=TAIL, width=2)                                  # the tail behind
    box(d, (5, 13, 16, h - 3), robe, radius=4, bevel=1)                                # robe
    d.rectangle((6, h - 3, 9, h - 1), fill=PINK)                                      # feet
    d.rectangle((12, h - 3, 15, h - 1), fill=PINK)
    disc(d, 9, 5, 4, MOUSE, shine=False)                                               # the ear
    d.ellipse((6, 4, 9 + 3, 8), fill=PINK)
    d.ellipse((7, 4, 18, 14), fill=MOUSE)                                              # head
    d.polygon([(15, 7), (21, 10), (15, 12)], fill=MOUSE)                               # snout
    disc(d, 20, 10, 1, PINK, shine=False)
    d.point((14, 8), fill=INK)                                                         # eye
    if beard:
        d.polygon([(13, 12), (19, 12), (15, 19)], fill=WHITE)
    if held:
        held(d)
    return img


def dojo():
    """
    The samurai dojo: the ground (gravel with stepping stones and a path to the output, turns with the building) and
    the pagoda itself as a part seen from the side, like the pirate ship: three curved red roofs, paper storeys, a gold
    spire. Parts in front: the sensei by the door (looks right and left, shouts now and then), a trainee with a wooden
    sword, the sliding door, lanterns on the eaves.
    """
    img, d = canvas(128, 128)
    baseplate(img, d, color=GRAVEL)
    for x, y in ((18, 20), (40, 106), (98, 22), (20, 92), (100, 100)):
        d.ellipse((x, y, x + 10, y + 7), fill=STONE)                                   # stepping stones
    box(d, (64, 58, 127, 70), STONE, radius=1, bevel=1)                                # the path to the output
    chute(d, 128, 128)
    out(img, "Buildings/dojo.png")

    pagoda, p_ = part(100, 116)
    p_.line((50, 0, 50, 16), fill=YELLOW, width=2)                                     # the spire with its rings
    for y in (4, 8, 12):
        p_.line((47, y, 53, y), fill=YELLOW, width=2)
    storey(p_, 34, 26, 66, 40)
    eave(p_, 50, 14, 6, 30, 28)
    storey(p_, 24, 52, 76, 70)
    eave(p_, 50, 38, 16, 42, 54)
    storey(p_, 14, 84, 86, 110)
    eave(p_, 50, 68, 26, 50, 86)
    p_.rectangle((40, 90, 60, 110), fill=tint(DARK, -0.2))                             # the doorway behind the door
    box(p_, (6, 108, 94, 115), STONE, radius=1, bevel=1)                               # the stone base
    out(pagoda, "Parts/pagoda.png")

    def stick(d2):
        d2.line((19, 12, 21, 29), fill=WOOD, width=2)

    out(profile_mouse(ROBE, held=stick, beard=True), "Parts/sensei.png")

    def wooden_sword(d2):
        d2.line((15, 17, 21, 2), fill=tint(WOOD, 0.25), width=3)

    out(profile_mouse(WHITE, held=wooden_sword), "Parts/dojo_trainee.png")

    bubble, bd = canvas(20, 18)
    bd.rounded_rectangle((1, 1, 18, 13), radius=4, fill=WHITE)
    bd.polygon([(4, 12), (8, 12), (3, 17)], fill=WHITE)
    bd.rectangle((8, 3, 11, 8), fill=RED)                                              # "!"
    bd.rectangle((8, 10, 11, 11), fill=RED)
    out(bubble, "Parts/sensei_shout.png")

    lantern, ld = canvas(10, 14)
    ld.line((5, 0, 5, 3), fill=INK)
    box(ld, (1, 3, 8, 13), (246, 140, 60), radius=3, bevel=1)                          # a paper lantern on a cord
    out(lantern, "Parts/dojo_lantern.png")

    door, dd = canvas(12, 20)
    box(dd, (0, 0, 11, 19), PAPER, radius=1, bevel=1)                                  # a paper sliding door
    for y in (5, 10, 15):
        dd.line((1, y, 10, y), fill=WOOD)
    dd.line((6, 1, 6, 18), fill=WOOD)
    out(door, "Parts/dojo_door.png")


TOWER_BLUE = (70, 110, 214)
TOWER_PURPLE = (140, 90, 200)
MOSS = (110, 150, 110)
GLOW = (150, 220, 255)


def wizard_tower():
    """
    The wizard tower: a mossy yard with a path to the output (turns with the building), and the tower itself as a part
    seen from the side, much taller than its 2x2 footprint: toy blocks stacked in blue and purple, narrowing upwards,
    a round window, a door, a balcony and a pointy purple roof. Parts: the star on the tip (turns while it works), sparks
    jumping between the roof points, the window flickering blue, a flash at the door when a wizard comes out.
    """
    img, d = canvas(128, 128)
    baseplate(img, d, color=MOSS)
    for x, y in ((16, 18), (100, 24), (22, 96), (98, 100)):
        d.ellipse((x, y, x + 9, y + 6), fill=STONE)                                    # stepping stones
    box(d, (64, 58, 127, 70), STONE, radius=1, bevel=1)                                # the path to the output
    chute(d, 128, 128)
    out(img, "Buildings/wizard_tower.png")

    tower, t_ = part(72, 170)
    cx = 36
    # stacked blocks, wider at the bottom, alternating colours, studs on each
    y, rows = 168, [(28, 22), (26, 20), (24, 20), (22, 18), (20, 18)]
    for i, (half, h) in enumerate(rows):
        color = TOWER_BLUE if i % 2 == 0 else TOWER_PURPLE
        box(t_, (cx - half, y - h, cx + half, y), color, radius=2, bevel=2)
        for sx in range(cx - half + 5, cx + half - 2, 8):
            stud(t_, sx, y - h + 3, color, 2)
        y -= h
    # the balcony under the roof
    box(t_, (cx - 25, y - 4, cx + 25, y + 2), WOOD, radius=1, bevel=1)
    for x in range(cx - 23, cx + 24, 6):
        t_.line((x, y - 10, x, y - 4), fill=tint(WOOD, -0.2), width=2)
    t_.line((cx - 25, y - 10, cx + 25, y - 10), fill=tint(WOOD, -0.2), width=2)
    # the pointy roof, its tip bent a little (the star sits on the tip, a part)
    roof_base = y - 10
    t_.polygon([(cx - 26, roof_base), (cx + 26, roof_base), (cx + 4, 12), (cx - 2, 8)], fill=TOWER_PURPLE)
    t_.polygon([(cx - 26, roof_base), (cx - 8, roof_base), (cx - 2, 8)], fill=tint(TOWER_PURPLE, -0.2))
    for k, ry in enumerate(range(roof_base - 12, 20, -12)):                            # little stars on the roof
        t_.point((cx - 6 + (k % 2) * 10, ry), fill=YELLOW)
    # the round window (dark: the glow is a part) and the door
    t_.ellipse((cx - 8, 76, cx + 8, 92), fill=tint(DARK, -0.1))
    t_.ellipse((cx - 8, 76, cx + 8, 92), outline=tint(WOOD, -0.2), width=2)
    t_.rounded_rectangle((cx - 9, 140, cx + 9, 167), radius=8, fill=tint(WOOD, -0.25))
    t_.line((cx, 142, cx, 167), fill=tint(WOOD, -0.45))
    disc(t_, cx + 5, 155, 1, YELLOW, shine=False)
    out(tower, "Parts/wizard_tower.png")

    star_img, sd = canvas(18, 18)
    sd.polygon([(9, 0), (11, 7), (18, 9), (11, 11), (9, 18), (7, 11), (0, 9), (7, 7)], fill=YELLOW)
    sd.point((9, 9), fill=WHITE)
    out(star_img, "Parts/tower_star.png")

    spark, kd = canvas(12, 16)
    kd.polygon([(7, 0), (1, 9), (5, 9), (3, 15), (11, 5), (7, 5), (9, 0)], fill=(250, 240, 150))  # a little bolt
    out(spark, "Parts/tower_spark.png")

    window, wd = canvas(14, 14)
    wd.ellipse((0, 0, 13, 13), fill=GLOW)
    wd.ellipse((3, 3, 10, 10), fill=WHITE)
    out(window, "Parts/tower_window.png")

    flash, fd = canvas(26, 30)
    fd.ellipse((0, 2, 25, 29), fill=GLOW)
    fd.ellipse((6, 8, 19, 23), fill=WHITE)
    out(flash, "Parts/tower_flash.png")


if __name__ == "__main__":
    soldier_factory()
    golem_workshop()
    car_factory()
    dojo()
    wizard_tower()
    print(" ".join(WRITTEN))
