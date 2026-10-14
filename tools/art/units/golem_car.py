"""
The brick golem (upright, 80 px cells: stomping walk, act = slamming a fist, slam = both hands up, then down) and the RC car (a vehicle:
seen from above pointing east and turned by the game; 2 frames of wheels turning and the antenna wobbling).

    python tools/art/units/golem_car.py && python tools/art/restyle.py <the files it lists>
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

G = 80                  # bigger than every other unit, but at most ~60 px wide: it still fits a 1-tile gap
G_FEET = G - 6
BRICK = (96, 170, 84)
STOMP = [(4, 0, 0), (0, 0, -2), (0, 4, 0), (0, 0, -2)]   # (left lift, right lift, bob)
POSES = [("idle", 0)] + [("walk", i) for i in range(4)] + [("act", i) for i in range(2)] + [("slam", i) for i in range(2)]


def brick(d, x0, y0, x1, y1, color=BRICK, studs_on=True):
    box(d, (x0, y0, x1, y1), color, radius=1, bevel=2)
    if studs_on and x1 - x0 >= 8:
        for x in range(x0 + 4, x1 - 2, 6):
            stud(d, x, y0 + 3, color, 1)


def golem(facing, pose, step):
    """Taller than before (legs 15, body 30, head 15), hardly wider. slam 0 = both hands raised over the head,
    slam 1 = both fists down on the ground."""
    img, d = canvas(G, G)
    lift_l, lift_r, bob = STOMP[step] if pose == "walk" else (0, 0, 0)
    if pose == "slam" and step == 1:
        bob = 3  # crouched into the slam
    cx = G // 2
    hip = G_FEET - 15 + bob
    body_top, head_top = hip - 30, hip - 45
    side = facing == "side"
    # legs: two brick pillars
    if side:
        brick(d, cx - 7, hip, cx + 2, G_FEET - lift_l, tint(BRICK, -0.2), False)
        brick(d, cx - 2, hip, cx + 7, G_FEET - lift_r, BRICK, False)
    else:
        brick(d, cx - 12, hip, cx - 3, G_FEET - lift_l, BRICK, False)
        brick(d, cx + 3, hip, cx + 12, G_FEET - lift_r, BRICK, False)
    shoulder_y = body_top + 4
    # where the fists go: (back fist, front fist); None = the back arm hangs as before
    if pose == "slam" and step == 0:
        fists = [(cx - 16, head_top - 7), (cx + 16, head_top - 7)] if not side else [(cx + 3, head_top - 8), (cx + 9, head_top - 6)]
    elif pose == "slam":
        fists = [(cx - 23, G_FEET - 4), (cx + 23, G_FEET - 4)] if not side else [(cx + 14, G_FEET - 4), (cx + 21, G_FEET - 4)]
    elif pose == "act":
        front = (cx + 12, shoulder_y - 14) if step == 0 else (cx + 18, hip + 4)
        fists = [None, front if side else (front[0] + 6, front[1])]
    else:
        fists = [None, (cx + 8, hip - 2) if side else (cx + 22, hip)]
    # the back arm first (behind the body)
    if fists[0] is None:
        if side:
            brick(d, cx - 4, shoulder_y, cx + 4, hip - 2, tint(BRICK, -0.25), False)
        else:
            brick(d, cx - 25, shoulder_y + 2, cx - 16, hip + 2 - bob, BRICK, False)
    else:
        sx = cx - (4 if side else 13)
        d.line((sx, shoulder_y + 2, *fists[0]), fill=tint(BRICK, -0.25), width=7)
        fx, fy = fists[0]
        brick(d, fx - 6, fy - 5, fx + 6, fy + 5, tint(BRICK, -0.15), False)
    # body: a big brick with studs; the head: a smaller one with glowing eyes
    if side:
        brick(d, cx - 11, body_top, cx + 9, hip + 2)
        brick(d, cx - 7, head_top, cx + 9, body_top)
        d.rectangle((cx + 4, head_top + 5, cx + 6, head_top + 9), fill=YELLOW)
    else:
        brick(d, cx - 16, body_top, cx + 16, hip + 2)
        brick(d, cx - 10, head_top, cx + 10, body_top)
        if facing == "toward":
            for ex in (cx - 6, cx + 4):
                d.rectangle((ex, head_top + 5, ex + 2, head_top + 9), fill=YELLOW)
            d.line((cx - 3, head_top + 11, cx + 3, head_top + 11), fill=INK)
    # the front arm and fist on top
    fx, fy = fists[1]
    d.line((cx + (2 if side else 13), shoulder_y + 2, fx, fy), fill=tint(BRICK, -0.1), width=7)
    brick(d, fx - 6, fy - 5, fx + 6, fy + 5, tint(BRICK, 0.1), False)
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
    save(unit_sheet(golem, columns=len(POSES), cell=G, poses=POSES), "Units/golem_sheet.png")
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
