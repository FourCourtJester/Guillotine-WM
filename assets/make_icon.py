"""Draws the Guillotine icon and writes assets/guillotine.ico plus PNG previews.

Run from the repository root:  python3 assets/make_icon.py
The design: a game window whose title bar has just been sliced off by a slanted blade.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent
S = 1024  # drawing resolution; every size is downsampled from this
SIZES = [256, 128, 64, 48, 40, 32, 24, 20, 16]


def lerp(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def vertical_gradient(size, top, bottom):
    img = Image.new("RGBA", size)
    draw = ImageDraw.Draw(img)
    for y in range(size[1]):
        draw.line([(0, y), (size[0], y)], fill=lerp(top, bottom, y / max(1, size[1] - 1)) + (255,))
    return img


def masked(fill, mask):
    layer = Image.new("RGBA", mask.size, (0, 0, 0, 0))
    layer.paste(fill, (0, 0), mask)
    return layer


def rounded_mask(box, radius, size=(S, S)):
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).rounded_rectangle(box, radius=radius, fill=255)
    return mask


def polygon_mask(points, size=(S, S)):
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask


def draw_icon(small=False):
    """small=True drops fine detail and thickens shapes for 24 px and below."""
    u = S / 256  # design units: a 256 grid
    p = lambda *xy: [v * u for v in xy]
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    # Tile, with a faint rim so it still separates from a dark taskbar
    tile = rounded_mask(p(8, 8, 248, 248), 52 * u)
    img.alpha_composite(masked(Image.new("RGBA", (S, S), (255, 255, 255, 46)), tile))
    inner = rounded_mask(p(12, 12, 244, 244), 48 * u)
    img.alpha_composite(masked(vertical_gradient((S, S), (48, 57, 76), (20, 24, 34)), inner))

    # Client area: the part that is kept, exact size
    client_box = p(40, 124, 216, 220) if small else p(44, 128, 212, 218)
    img.alpha_composite(masked(vertical_gradient((S, S), (96, 156, 255), (52, 104, 230)), rounded_mask(client_box, 14 * u)))

    # Severed title bar, knocked up and tilted
    bar = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    bar_box = p(40, 44, 216, 80) if small else p(44, 52, 212, 84)
    bar.alpha_composite(masked(vertical_gradient((S, S), (156, 166, 184), (116, 126, 146)), rounded_mask(bar_box, 12 * u)))
    if not small:
        bd = ImageDraw.Draw(bar)
        for x in (166, 186):
            bd.ellipse(p(x, 62, x + 12, 74), fill=(70, 78, 96, 255))
    bar = bar.rotate(8, resample=Image.BICUBIC, center=(128 * u, 68 * u), translate=(-4 * u, -4 * u))
    img.alpha_composite(bar)

    # Blade: a steel wedge whose cutting edge slants down to the right, just above the client area
    if small:
        body, edge_pts = p(16, 88, 240, 88, 240, 118, 16, 104), p(16, 98, 240, 112, 240, 118, 16, 104)
    else:
        body, edge_pts = p(16, 92, 240, 92, 240, 120, 16, 108), p(16, 104, 240, 116, 240, 120, 16, 108)
    blade = polygon_mask(body)
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    shadow.paste((0, 0, 0, 120), (0, 0), blade)
    shadow = shadow.transform((S, S), Image.AFFINE, (1, 0, 0, 0, 1, -5 * u)).filter(ImageFilter.GaussianBlur(5 * u))
    img.alpha_composite(shadow)
    img.alpha_composite(masked(vertical_gradient((S, S), (226, 232, 241), (150, 160, 178)), blade))
    # bright bevel along the cutting edge
    img.alpha_composite(masked(Image.new("RGBA", (S, S), (255, 255, 255, 255)), polygon_mask(edge_pts)))

    # Clip everything to the tile so nothing sticks out past the rounded corners
    clipped = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    clipped.paste(img, (0, 0), tile)
    return clipped


def main():
    big, small = draw_icon(), draw_icon(small=True)
    images = [(small if n <= 24 else big).resize((n, n), Image.LANCZOS) for n in SIZES]
    images[0].save(OUT / "guillotine.ico", sizes=[(n, n) for n in SIZES], append_images=images[1:])
    images[0].save(OUT / "guillotine-256.png")

    # Preview sheet of every size on light and dark backgrounds (not shipped)
    sheet = Image.new("RGBA", (sum(SIZES) + 20 * len(SIZES) + 20, 2 * 276), (243, 243, 243, 255))
    ImageDraw.Draw(sheet).rectangle([0, 276, sheet.width, sheet.height], fill=(32, 32, 32, 255))
    x = 20
    for n, im in zip(SIZES, images):
        sheet.alpha_composite(im, (x, 10))
        sheet.alpha_composite(im, (x, 286))
        x += n + 20
    sheet.save(OUT.parent / ".icon-preview.png")


if __name__ == "__main__":
    main()
