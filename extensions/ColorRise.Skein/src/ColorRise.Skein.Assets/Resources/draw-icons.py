# Draws Skein's icons: white glyphs on a transparent ground for the asset thumbnails (as Stride's own), the same glyphs
# in a blue that reads on a light and on a dark ground for the templates of "Add asset", a small dark glyph with a light
# edge for the components. Run it from this folder: python3 draw-icons.py
from PIL import Image, ImageDraw

S = 8  # supersampling

def bubble(draw, box, tail, fill):
    x0, y0, x1, y1 = [v * S for v in box]
    r = int(min(x1 - x0, y1 - y0) * 0.22)
    draw.rounded_rectangle([x0, y0, x1, y1], radius=r, fill=fill)
    draw.polygon([(p[0] * S, p[1] * S) for p in tail], fill=fill)

def lines(draw, rows, fill):
    for (x0, y, x1, h) in rows:
        draw.rounded_rectangle([x0 * S, (y - h / 2) * S, x1 * S, (y + h / 2) * S], radius=int(h * S / 2), fill=fill)

def finish(image, size, path):
    image.resize((size, size), Image.LANCZOS).save(path)

def script(path, size=128, ink=(255, 255, 255)):
    im = Image.new("RGBA", (128 * S, 128 * S), ink + (0,))
    d = ImageDraw.Draw(im)
    full, clear = ink + (255,), ink + (0,)
    bubble(d, (28, 34, 100, 82), [(42, 80), (42, 98), (60, 80)], full)
    lines(d, [(38, 47, 90, 6), (38, 58, 90, 6), (38, 69, 74, 6)], clear)
    finish(im, size, path)

def project(path, size=128, ink=(255, 255, 255)):
    im = Image.new("RGBA", (128 * S, 128 * S), ink + (0,))
    d = ImageDraw.Draw(im)
    full, clear = ink + (255,), ink + (0,)
    # A bubble behind, cut around the one in front
    bubble(d, (46, 28, 104, 66), [(88, 64), (88, 78), (74, 64)], full)
    bubble(d, (20, 46, 84, 90), [(34, 88), (34, 104), (50, 88)], clear)
    bubble(d, (24, 50, 80, 86), [(36, 84), (36, 99), (50, 84)], full)
    lines(d, [(33, 62, 71, 5), (33, 73, 60, 5)], clear)
    finish(im, size, path)

def component(path):
    im = Image.new("RGBA", (16 * S, 16 * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    light, dark = (246, 246, 246, 255), (66, 66, 66, 255)
    bubble(d, (0.5, 1, 15.5, 12), [(3, 11.5), (3, 15.5), (8, 11.5)], light)
    bubble(d, (1.5, 2, 14.5, 11), [(4, 10.5), (4, 13.6), (7, 10.5)], dark)
    lines(d, [(4, 5, 12, 1.2), (4, 8, 10, 1.2)], light)
    finish(im, 16, path)

BLUE = (47, 143, 208)
script("YarnScriptThumbnail.png")
project("YarnProjectThumbnail.png")
component("DialogueComponent.png")
script("../Templates/Assets/.sdtpl/YarnScript.png", 48, BLUE)
project("../Templates/Assets/.sdtpl/YarnProject.png", 48, BLUE)
