"""Generate the DeskOrder app icon (no external assets)."""
from PIL import Image, ImageDraw, ImageFilter
import math
import os

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "assets")
S = 1024


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def rounded_mask(size, radius):
    m = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(m)
    d.rounded_rectangle([0, 0, size - 1, size - 1], radius=radius, fill=255)
    return m


def gradient(size, c1, c2, diagonal=True):
    img = Image.new("RGB", (size, size), c1)
    px = img.load()
    for y in range(size):
        for x in range(size):
            t = (x + y) / (2.0 * (size - 1)) if diagonal else y / (size - 1)
            px[x, y] = lerp(c1, c2, t)
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    radius = 220
    mask = rounded_mask(S, radius)

    base = gradient(S, (18, 25, 44), (7, 9, 16))
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    # soft ambient glows
    glow = Image.new("RGB", (S, S), (7, 9, 16))
    gd = ImageDraw.Draw(glow)
    gd.ellipse([-260, -300, 780, 720], fill=(44, 66, 140))
    gd.ellipse([420, 380, 1260, 1220], fill=(22, 96, 92))
    glow = glow.filter(ImageFilter.GaussianBlur(180))
    img.paste(Image.composite(glow, base, Image.new("L", (S, S), 120)), (0, 0))

    draw = ImageDraw.Draw(img)

    # equalizer bars: three rounded columns with gradient
    bars = [(292, 300, 176, 390), (424, 210, 176, 480), (556, 350, 176, 340)]
    for i, (x, y, w, h) in enumerate(bars):
        top = lerp((124, 155, 255), (87, 224, 200), i / 2.0)
        bottom = lerp((72, 98, 205), (43, 160, 150), i / 2.0)
        bar = Image.new("RGB", (w, h))
        bd = ImageDraw.Draw(bar)
        for yy in range(h):
            bd.line([(0, yy), (w, yy)], fill=lerp(top, bottom, yy / float(h)))
        bm = Image.new("L", (w, h), 0)
        ImageDraw.Draw(bm).rounded_rectangle([0, 0, w - 1, h - 1], radius=w // 2, fill=255)
        img.paste(bar, (x, y), bm)

    # dock shelf
    shelf = Image.new("RGBA", (640, 150), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shelf)
    sd.rounded_rectangle([0, 0, 639, 149], radius=75, fill=(255, 255, 255, 28),
                         outline=(255, 255, 255, 70), width=4)
    img.alpha_composite(shelf, (192, 724))

    # three dots on the shelf
    for i, col in enumerate([(255, 255, 255, 235), (87, 224, 200, 235), (124, 155, 255, 235)]):
        cx = 320 + i * 130
        d = ImageDraw.Draw(img)
        d.ellipse([cx - 26, 774, cx + 26, 826], fill=col)

    # border
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([2, 2, S - 3, S - 3], radius=radius, outline=(255, 255, 255, 36), width=5)

    img.putalpha(mask)
    img.save(os.path.join(OUT, "app-256.png"))

    sizes = [256, 128, 64, 48, 32, 16]
    img.save(os.path.join(OUT, "app.ico"), format="ICO", sizes=[(s, s) for s in sizes])
    print("icon written", os.path.join(OUT, "app.ico"))


if __name__ == "__main__":
    main()
