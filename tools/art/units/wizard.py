"""
The wizard: a cute old mouse druid (grey face, big ears, white beard, a pink tail; a layered green leaf cloak with a
hood and twig antlers; a twisted staff with a glowing orb). Drawn with PixelLab (character "Wizard Mouse Druid", 64 px,
low top-down) and kept in tools/art/pixellab/wizard/: idle, a walk of 4 and a cast (staff raised, then thrust
forward) for south, north and east, laid out as the game's upright sheet (kit.pixellab_upright). The pixel art is
finished, so restyle copies it as it is (restyle.AS_IS).

    python tools/art/units/wizard.py && python tools/art/restyle.py <the files it lists>
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import write_pixellab_upright  # noqa: E402

if __name__ == "__main__":
    print(" ".join(write_pixellab_upright("wizard")))
