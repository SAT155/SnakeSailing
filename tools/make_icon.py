"""Redraws thunderstore/SnakeSkills/icon.png (256x256) from the three skill icons."""
from pathlib import Path

from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent.parent
icons = [Image.open(root / "SnakeSkills" / "icons" / f"{n}.png").convert("RGBA") for n in ("sailing", "ranching", "mining")]
img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
ImageDraw.Draw(img).rounded_rectangle([4, 4, 251, 251], radius=36, fill=(222, 204, 160, 255), outline=(70, 120, 55, 255), width=8)
for icon, pos in zip(icons, [(72, 20), (18, 122), (126, 122)]):
    img.alpha_composite(icon.resize((112, 112), Image.LANCZOS), pos)
img.save(root / "thunderstore" / "SnakeSkills" / "icon.png")
