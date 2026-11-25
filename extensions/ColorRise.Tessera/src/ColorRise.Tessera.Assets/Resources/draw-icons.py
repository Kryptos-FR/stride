# Draws Tessera's icons: white glyphs on a transparent ground for the asset thumbnails (as Stride's own), the same glyphs
# in a blue that reads on a light and on a dark ground for the templates of "Add asset", a small dark glyph with a light
# edge for the component. A tile set is a block of three by three squares; a tile map is the same grid with some
# squares left out, like a small map. Run it from this folder: python3 draw-icons.py
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

BLUE = (47, 143, 208)
grid("TileSetThumbnail.png", ALL)
grid("TileMapThumbnail.png", MAP)
component("TileMapComponent.png")
grid("../Templates/Assets/.sdtpl/TileSet.png", ALL, 48, BLUE)
grid("../Templates/Assets/.sdtpl/TileMap.png", MAP, 48, BLUE)
