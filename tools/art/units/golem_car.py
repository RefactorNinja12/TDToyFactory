"""
The brick golem (upright, 64 px cells: stomping walk, act = slamming a fist) and the RC car (a vehicle:
seen from above pointing east and turned by the game; 2 frames of wheels turning and the antenna wobbling).

    python tools/art/units/golem_car.py && python tools/art/restyle.py <the files it lists>
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

G = 64
G_FEET = G - 6
BRICK = (96, 170, 84)
STOMP = [(4, 0, 0), (0, 0, -2), (0, 4, 0), (0, 0, -2)]   # (left lift, right lift, bob)


def brick(d, x0, y0, x1, y1, color=BRICK, studs_on=True):
    box(d, (x0, y0, x1, y1), color, radius=1, bevel=2)
    if studs_on and x1 - x0 >= 8:
        for x in range(x0 + 4, x1 - 2, 6):
            stud(d, x, y0 + 3, color, 1)


def golem(facing, pose, step):
    img, d = canvas(G, G)
    lift_l, lift_r, bob = STOMP[step] if pose == "walk" else (0, 0, 0)
    cx = G // 2
    hip = G_FEET - 12 + bob
    # legs: two brick pillars
    if facing == "side":
        brick(d, cx - 6, hip, cx + 2, G_FEET - lift_l, tint(BRICK, -0.2), False)
        brick(d, cx - 2, hip, cx + 6, G_FEET - lift_r, BRICK, False)
    else:
        brick(d, cx - 11, hip, cx - 3, G_FEET - lift_l, BRICK, False)
        brick(d, cx + 3, hip, cx + 11, G_FEET - lift_r, BRICK, False)
    # arms (the front one slams in the act frames)
    slam = pose == "act"
    arm_y = hip - 18
    if facing == "side":
        fist = (cx + 10, arm_y - 12) if slam and step == 0 else (cx + 14, hip + 2) if slam else (cx + 6, hip - 2)
        brick(d, cx - 4, arm_y, cx + 4, hip - 2, tint(BRICK, -0.25), False)
    else:
        fist = (cx + 18, arm_y - 10) if slam and step == 0 else (cx + 16, hip + 4) if slam else (cx + 18, hip)
        brick(d, cx - 22, arm_y + 2, cx - 14, hip + 2 - bob, BRICK, False)
    # body: a big brick with studs; the head: a smaller one with glowing eyes
    if facing == "side":
        brick(d, cx - 10, hip - 22, cx + 8, hip + 2)
        brick(d, cx - 6, hip - 34, cx + 8, hip - 22)
        if True:
            d.rectangle((cx + 4, hip - 30, cx + 6, hip - 27), fill=YELLOW)
    else:
        brick(d, cx - 14, hip - 22, cx + 14, hip + 2)
        brick(d, cx - 9, hip - 34, cx + 9, hip - 22)
        if facing == "toward":
            for ex in (cx - 5, cx + 3):
                d.rectangle((ex, hip - 30, ex + 2, hip - 27), fill=YELLOW)
            d.line((cx - 3, hip - 25, cx + 3, hip - 25), fill=INK)
    # the front arm and fist on top
    fx, fy = fist
    d.line((cx + (2 if facing == "side" else 12), arm_y + 2, fx, fy), fill=tint(BRICK, -0.1), width=6)
    brick(d, fx - 5, fy - 4, fx + 5, fy + 5, tint(BRICK, 0.1), False)
    return img


def rc_car(frame):
    img, d = canvas(48, 48)
    # wheels (tread lines move between the frames)
    for x0, y0 in ((12, 11), (30, 11), (12, 33), (30, 33)):
        d.rectangle((x0, y0, x0 + 8, y0 + 4), fill=DARK)
        for x in range(x0 + 1 + frame * 2, x0 + 8, 4):
            d.line((x, y0, x, y0 + 4), fill=tint(DARK, 0.35))
    # body, cockpit, front bumper
    box(d, (10, 15, 40, 33), GREEN, radius=4)
    box(d, (22, 19, 32, 29), GLASS, radius=2, bevel=1)
    d.rectangle((40, 18, 43, 30), fill=GREY)
    # antenna at the back, the tip wobbling
    tip = (6, 10 + frame * 3)
    d.line((14, 18, *tip), fill=GREY)
    disc(d, *tip, 2, RED, shine=False)
    return img


def main():
    written = []
    save(unit_sheet(golem, cell=G), "Units/golem_sheet.png")
    save(golem("toward", "idle", 0), "Units/golem.png")
    sheet = Image.new("RGBA", (96, 48), (0, 0, 0, 0))
    for frame in range(2):
        sheet.alpha_composite(rc_car(frame), (frame * 48, 0))
    save(sheet, "Units/rc_car_sheet.png")
    save(rc_car(0), "Units/rc_car.png")
    written += ["Units/golem_sheet.png", "Units/golem.png", "Units/rc_car_sheet.png", "Units/rc_car.png"]
    print(" ".join(written))


if __name__ == "__main__":
    main()
