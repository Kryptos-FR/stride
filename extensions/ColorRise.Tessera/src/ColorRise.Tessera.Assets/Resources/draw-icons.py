# Draws Tessera's icons: white glyphs on a transparent ground for the asset thumbnails (as Stride's own), the same glyphs
# in a blue that reads on a light and on a dark ground for the templates of "Add asset", a small dark glyph with a light
# edge for the component. A tile set is a block of three by three squares; a tile map is the same grid with some
# squares left out, like a small map; a flipbook is three small frames of a figure in a row, its legs in three steps of a
# walk. Run it from this folder: python3 draw-icons.py
from PIL import Image, ImageDraw

S = 8  # supersampling

# The squares of the grid, by column and row, from the top left; a map leaves some out
ALL = [(column, row) for row in range(3) for column in range(3)]
MAP = [(0, 0), (1, 0), (1, 1), (2, 1), (0, 2), (1, 2), (2, 2)]

def squares(draw, origin, cell, gap, radius, cells, fill):
    x0, y0 = origin
    for (column, row) in cells:
        left = x0 + column * (cell + gap)
        top = y0 + row * (cell + gap)
        draw.rounded_rectangle([left * S, top * S, (left + cell) * S, (top + cell) * S], radius=int(radius * S), fill=fill)

def finish(image, size, path):
    image.resize((size, size), Image.LANCZOS).save(path)

def grid(path, cells, size=128, ink=(255, 255, 255)):
    im = Image.new("RGBA", (128 * S, 128 * S), ink + (0,))
    d = ImageDraw.Draw(im)
    # Three squares of 26 with gaps of 7: 92 wide, centred
    squares(d, (18, 18), 26, 7, 4, cells, ink + (255,))
    finish(im, size, path)

def component(path):
    # Drawn on whole pixels, as 16 is too small to scale down to: each square of the map is a dark square of four
    # pixels in a light edge of one, and the edges of two squares side by side are one
    im = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    light, dark = (246, 246, 246, 255), (66, 66, 66, 255)
    for (column, row) in MAP:
        d.rectangle([column * 5, row * 5, column * 5 + 5, row * 5 + 5], fill=light)
    for (column, row) in MAP:
        d.rectangle([column * 5 + 1, row * 5 + 1, column * 5 + 4, row * 5 + 4], fill=dark)
    im.save(path)

# The legs of the figure in each frame, as the x of each foot from the middle of the frame, in parts of its width
STEPS = [(-0.08, 0.08), (-0.26, 0.26), (-0.2, 0.06)]

def frames(path, size=128, ink=(255, 255, 255)):
    im = Image.new("RGBA", (128 * S, 128 * S), ink + (0,))
    d = ImageDraw.Draw(im)
    # Three frames of 32 by 54 with gaps of 6: 108 wide, centred; a figure in each
    width, height, gap, top = 32, 54, 6, 37
    for index, (left_foot, right_foot) in enumerate(STEPS):
        left = 10 + index * (width + gap)
        d.rounded_rectangle([left * S, top * S, (left + width) * S, (top + height) * S], radius=4 * S, outline=ink + (255,), width=3 * S)
        middle = left + width / 2
        # The head, the body, then the legs from the hip to the feet
        d.ellipse([(middle - 5) * S, (top + 9) * S, (middle + 5) * S, (top + 19) * S], fill=ink + (255,))
        d.rounded_rectangle([(middle - 4) * S, (top + 21) * S, (middle + 4) * S, (top + 36) * S], radius=2 * S, fill=ink + (255,))
        for foot in (left_foot, right_foot):
            d.line([middle * S, (top + 34) * S, (middle + foot * width) * S, (top + 47) * S], fill=ink + (255,), width=4 * S)
    finish(im, size, path)

def flipbook_component(path):
    # Drawn on whole pixels, as 16 is too small for frames: the three figures of the frames side by side, dark, with a light
    # edge of one pixel around them, each with its legs in its step of the walk
    dark_pixels = set()
    legs = [((2, 3), (2, 3)), ((1, 4), (1, 4)), ((1, 3), (1, 4))]
    for index, (upper, lower) in enumerate(legs):
        left = index * 5 + 1
        dark_pixels |= {(left + x, y) for x in (1, 2) for y in (2, 3)}
        dark_pixels |= {(left + x, y) for x in range(4) for y in range(5, 9)}
        dark_pixels |= {(left + x, y) for x in upper for y in (9, 10)}
        dark_pixels |= {(left + x, y) for x in lower for y in (11, 12)}
    im = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    light, dark = (246, 246, 246, 255), (66, 66, 66, 255)
    for (x, y) in dark_pixels:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                if 0 <= x + dx < 16 and 0 <= y + dy < 16:
                    im.putpixel((x + dx, y + dy), light)
    for (x, y) in dark_pixels:
        im.putpixel((x, y), dark)
    im.save(path)

BLUE = (47, 143, 208)
grid("TileSetThumbnail.png", ALL)
grid("TileMapThumbnail.png", MAP)
component("TileMapComponent.png")
grid("../Templates/Assets/.sdtpl/TileSet.png", ALL, 48, BLUE)
grid("../Templates/Assets/.sdtpl/TileMap.png", MAP, 48, BLUE)
frames("FlipbookThumbnail.png")
flipbook_component("FlipbookComponent.png")
frames("../Templates/Assets/.sdtpl/Flipbook.png", 48, BLUE)
