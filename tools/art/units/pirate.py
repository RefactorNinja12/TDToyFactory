"""
The pirate (the plastic soldier): a cute mouse pirate (grey face, big ears, an eye patch, a pink tail; a black tricorn
with a skull, a red bandana, a striped shirt and a vest; flintlock pistols). Drawn with PixelLab (character "Pirate
Mouse", 64 px, low top-down) and kept in tools/art/pixellab/pirate/: idle, a walk of 4 and a shot (aim, then fire with a
flash or a puff) for south, north and east, laid out as the game's upright sheet (kit.pixellab_upright). The pixel art is
finished, so restyle copies it as it is (restyle.AS_IS).

    python tools/art/units/pirate.py && python tools/art/restyle.py <the files it lists>
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import write_pixellab_upright  # noqa: E402

if __name__ == "__main__":
    print(" ".join(write_pixellab_upright("pirate")))
