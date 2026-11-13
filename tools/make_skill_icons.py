"""Draws the Sailing, Ranching and Mining skill icons (64x64) in SnakeSkills/icons/.

Simple shapes in the same flat style as landscaping.png and restore.png. Drawn at 4x and scaled
down for smooth edges. Run tools/make_icon.py afterwards to redraw the package icon.
"""
from pathlib import Path

from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent.parent
S = 4  # supersampling


def canvas():
    img = Image.new("RGBA", (64 * S, 64 * S), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def p(*xy):
    return [v * S for v in xy]


def save(img, name):
    img.resize((64, 64), Image.LANCZOS).save(root / "SnakeSkills" / "icons" / f"{name}.png")


WOOD = (150, 95, 50, 255)
WOOD_DARK = (95, 58, 30, 255)
OUTLINE = (40, 30, 20, 255)


def sailing():
    img, d = canvas()
    # mast and square sail with stripes
    d.rectangle(p(30, 6, 33, 44), fill=WOOD_DARK)
    d.polygon(p(14, 10, 50, 10, 47, 36, 17, 36), fill=(235, 228, 205, 255), outline=OUTLINE)
    for x0 in (20, 32, 44):
        d.polygon(p(x0 - 3, 10, x0 + 3, 10, x0 + 2.5, 36, x0 - 2.5, 36), fill=(185, 45, 40, 255))
    # hull
    d.polygon(p(4, 40, 60, 40, 52, 51, 12, 51), fill=WOOD, outline=OUTLINE)
    d.line(p(7, 44, 57, 44), fill=WOOD_DARK, width=S * 2)
    # waves
    for y in (54, 59):
        for x in range(2, 62, 12):
            d.arc(p(x, y - 3, x + 12, y + 3), 200, 340, fill=(70, 140, 205, 255), width=S * 2)
    save(img, "sailing")


def ranching():
    img, d = canvas()
    # fence: two posts and two rails
    for x in (8, 52):
        d.rectangle(p(x, 26, x + 5, 60), fill=WOOD, outline=OUTLINE)
    for y in (34, 48):
        d.rectangle(p(4, y, 60, y + 5), fill=WOOD, outline=OUTLINE)
    # heart above
    d.ellipse(p(19, 4, 33, 18), fill=(205, 45, 55, 255))
    d.ellipse(p(31, 4, 45, 18), fill=(205, 45, 55, 255))
    d.polygon(p(20, 13, 44, 13, 32, 28), fill=(205, 45, 55, 255))
    save(img, "ranching")


def mining():
    img, d = canvas()
    # ore lump
    d.polygon(p(6, 50, 12, 36, 26, 30, 42, 33, 56, 42, 58, 56, 10, 58), fill=(120, 118, 112, 255), outline=OUTLINE)
    for box in ((16, 42, 22, 48), (30, 37, 37, 43), (42, 46, 49, 52), (24, 50, 29, 55)):
        d.ellipse(p(*box), fill=(200, 120, 60, 255))
    # pickaxe: handle and head
    d.line(p(18, 30, 46, 4), fill=WOOD, width=S * 4)
    d.polygon(p(34, 2, 44, 0, 56, 10, 62, 22, 50, 12, 40, 8), fill=(170, 175, 185, 255), outline=OUTLINE)
    save(img, "mining")


sailing()
ranching()
mining()
