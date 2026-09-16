#!/usr/bin/env python3
"""
Generates the tiling textures the grey-box level uses.

Same reasoning as the audio: original, licence-free, tiny, and tweakable by
changing a number rather than by finding a different download. They are not
trying to look photoreal - the goal is that a player can tell at a glance which
surface is floor, which is worktop and which is wall.

    python3 tools/assets/generate_textures.py

Writes 256x256 seamless PNGs into unity/Assets/AngryGuy/Resources/Textures/,
loadable with Resources.Load<Texture2D>("Textures/<name>").
"""

import os

import numpy as np
from PIL import Image

SIZE = 256
OUT = os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "unity", "Assets", "AngryGuy", "Resources", "Textures",
)

rng = np.random.default_rng(11)


def grid():
    y, x = np.mgrid[0:SIZE, 0:SIZE]
    return x, y


def tileable_noise(octaves=4, base=8):
    """Sum of smoothed random grids at increasing frequency, wrapped so it tiles."""
    out = np.zeros((SIZE, SIZE))
    amplitude = 1.0
    for o in range(octaves):
        res = base * (2 ** o)
        small = rng.random((res, res))
        # np.tile then resize gives wrap-around continuity.
        img = Image.fromarray((small * 255).astype(np.uint8)).resize((SIZE, SIZE), Image.BICUBIC)
        out += np.asarray(img, dtype=float) / 255.0 * amplitude
        amplitude *= 0.5
    out -= out.min()
    return out / max(out.max(), 1e-9)


def save(name, rgb):
    os.makedirs(OUT, exist_ok=True)
    img = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), mode="RGB")
    path = os.path.join(OUT, name + ".png")
    img.save(path, optimize=True)
    print("  {:<16} {:>6} bytes".format(name, os.path.getsize(path)))


def colourise(mask, dark, light):
    dark = np.array(dark, dtype=float)
    light = np.array(light, dtype=float)
    return dark[None, None, :] + (light - dark)[None, None, :] * mask[:, :, None]


# ----------------------------------------------------------------- textures

def kitchen_tile():
    """Commercial kitchen floor: square tiles, dark grout, subtle grime."""
    x, y = grid()
    cell = SIZE // 4
    gx = (x % cell) / cell
    gy = (y % cell) / cell

    grout = 0.06
    is_grout = (gx < grout) | (gy < grout) | (gx > 1 - grout) | (gy > 1 - grout)

    grime = tileable_noise(octaves=4, base=8) * 0.18
    mask = 0.78 - grime
    mask[is_grout] = 0.25

    # A little per-tile variation so it does not read as a flat pattern.
    tile_id = (x // cell) + (y // cell) * 7
    variation = ((tile_id * 37) % 11) / 11.0 * 0.07
    mask = np.clip(mask - variation, 0, 1)

    return colourise(mask, (74, 72, 70), (206, 203, 196))


def wall_paint():
    """Scuffed painted plaster."""
    base = tileable_noise(octaves=5, base=4)
    scuff = np.clip(tileable_noise(octaves=3, base=16) - 0.55, 0, 1) * 0.5
    mask = np.clip(0.72 + base * 0.12 - scuff, 0, 1)
    return colourise(mask, (96, 98, 104), (196, 198, 202))


def worktop_steel():
    """Brushed stainless: strong horizontal streaking."""
    # Built from wrapped noise squashed horizontally, so the streaks tile cleanly
    # instead of leaving a visible seam down one edge.
    streak = tileable_noise(octaves=3, base=4)
    streak = np.repeat(streak[:, :1], SIZE, axis=1) * 0.5 + streak * 0.5
    fine = tileable_noise(octaves=2, base=64) * 0.15
    mask = np.clip(0.62 + streak * 0.28 + fine - 0.07, 0, 1)
    return colourise(mask, (120, 124, 130), (214, 218, 224))


def wood_floor():
    """Dining room boards, running along X."""
    x, y = grid()
    plank_h = SIZE // 8

    # One horizontal offset per ROW (1-D), so every plank shows a different slice
    # of the same wrapped grain and the boards do not look cloned.
    row_offset = ((np.arange(SIZE) // plank_h) * 53) % SIZE

    grain = tileable_noise(octaves=4, base=16)
    shifted = np.take_along_axis(grain, (x + row_offset[:, None]) % SIZE, axis=1)

    edge = (y % plank_h) < 2
    mask = np.clip(0.55 + shifted * 0.35, 0, 1)
    mask[edge] = 0.28

    return colourise(mask, (84, 56, 34), (176, 130, 84))


def fabric():
    """Booth upholstery / soft furnishing."""
    x, y = grid()
    weave = (np.sin(x * np.pi / 3.0) * np.sin(y * np.pi / 3.0) + 1) * 0.5
    fuzz = tileable_noise(octaves=3, base=32) * 0.25
    mask = np.clip(0.5 + weave * 0.2 + fuzz - 0.1, 0, 1)
    return colourise(mask, (58, 40, 52), (128, 96, 118))


TEXTURES = {
    "kitchen_tile": kitchen_tile,
    "wall_paint": wall_paint,
    "worktop_steel": worktop_steel,
    "wood_floor": wood_floor,
    "fabric": fabric,
}


if __name__ == "__main__":
    print("Generating textures into", OUT)
    for name, fn in TEXTURES.items():
        save(name, fn())
    print("done -", len(TEXTURES), "textures")
