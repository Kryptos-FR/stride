using ColorRise.Tessera.Assets;

namespace ColorRise.Tessera.Tests.Assets;

public sealed class TileChunkTextTests
{
    private const string EmptyLine = ". . . . . . . . . . . . . . . .";
    private static readonly string EmptyChunk = string.Concat(Enumerable.Repeat(EmptyLine + "\n", 16));

    [Fact]
    public void An_empty_chunk_is_sixteen_lines_of_dots()
        => Assert.Equal(EmptyChunk, TileChunkText.Encode(new TileCell[256]));

    [Fact]
    public void The_first_line_is_the_top_row()
    {
        var cells = new TileCell[256];
        cells[15 * 16 + 2] = new TileCell(1, TileId.FromGrid(1, 3, 4));
        cells[15 * 16 + 3] = new TileCell(1, TileId.FromGrid(1, 3, 4), TileFlip.Horizontal);
        cells[0] = new TileCell(2, TileId.FromNumber(1, 17), TileFlip.Diagonal | TileFlip.Vertical);
        var lines = TileChunkText.Encode(cells).Split('\n');
        Assert.Equal(". . 1/1:3,4 1/1:3,4h . . . . . . . . . . . .", lines[0]);
        Assert.Equal("2/1:#17dv . . . . . . . . . . . . . . .", lines[15]);
        Assert.Equal("", lines[16]);
    }

    [Fact]
    public void All_the_flips_are_written_d_then_h_then_v()
    {
        var cells = new TileCell[256];
        for (var flip = 0; flip < 8; flip++)
        {
            cells[15 * 16 + flip] = new TileCell(1, TileId.FromGrid(1, 0, 0), (TileFlip)flip);
        }
        var tokens = TileChunkText.Encode(cells).Split('\n')[0].Split(' ').Take(8).Select(token => token["1/1:0,0".Length..]);
        Assert.Equal(["", "h", "v", "hv", "d", "dh", "dv", "dhv"], tokens);
    }

    [Fact]
    public void Decode_reads_back_what_encode_wrote()
    {
        var random = new Random(12345);
        var cells = new TileCell[256];
        for (var i = 0; i < cells.Length; i++)
        {
            var source = random.Next(1, 256);
            var tile = random.Next(2) == 0
                ? TileId.FromGrid(source, random.Next(TileId.MaxColumn + 1), random.Next(TileId.MaxRow + 1))
                : TileId.FromNumber(source, random.Next(TileId.MaxNumber + 1));
            cells[i] = new TileCell((ushort)random.Next(65536), tile, (TileFlip)random.Next(8));
        }

        var decoded = new TileCell[256];
        TileChunkText.Decode(TileChunkText.Encode(cells), decoded);
        Assert.Equal(cells, decoded);
    }

    [Theory]
    [InlineData(2, "line 3", ". . . .")]
    [InlineData(0, "token 17", EmptyLine + " .")]
    [InlineData(5, "<<<<<<<", "<<<<<<< HEAD")]
    [InlineData(1, "1/1:3,4x", "1/1:3,4x . . . . . . . . . . . . . . .")]
    [InlineData(1, "0/1:3,4", "0/1:3,4 . . . . . . . . . . . . . . .")]
    [InlineData(1, "1/", "1/ . . . . . . . . . . . . . . .")]
    public void A_bad_line_is_refused_and_named(int index, string expectedInMessage, string badLine)
    {
        var lines = Enumerable.Repeat(EmptyLine, 16).ToArray();
        lines[index] = badLine;
        var cells = new TileCell[256];
        Assert.False(TileChunkText.TryDecode(string.Concat(lines.Select(line => line + "\n")), cells, out var error));
        Assert.Contains(expectedInMessage, error);
        Assert.Contains($"line {index + 1}", error);
    }

    [Fact]
    public void Decode_throws_a_format_exception_with_the_error()
    {
        var error = Assert.Throws<FormatException>(() => TileChunkText.Decode(". . . .\n", new TileCell[256]));
        Assert.Contains("16", error.Message);
    }

    [Fact]
    public void A_refused_text_leaves_the_cells_as_they_were()
    {
        var cells = new TileCell[256];
        cells[3] = new TileCell(1, TileId.FromGrid(1, 0, 0));
        var text = "1/1:0,0 " + EmptyChunk[2..^(EmptyLine.Length + 1)] + "x\n";
        Assert.False(TileChunkText.TryDecode(text, cells, out _));
        Assert.Equal(new TileCell(1, TileId.FromGrid(1, 0, 0)), cells[3]);
        Assert.True(cells[15 * 16].IsEmpty);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(17)]
    public void Fifteen_or_seventeen_lines_are_refused(int count)
    {
        Assert.False(TileChunkText.TryDecode(string.Concat(Enumerable.Repeat(EmptyLine + "\n", count)), new TileCell[256], out var error));
        Assert.Contains(count.ToString(), error);
    }

    [Fact]
    public void Windows_line_ends_are_read()
    {
        var cells = new TileCell[256];
        cells[15 * 16 + 2] = new TileCell(1, TileId.FromGrid(1, 3, 4), TileFlip.Diagonal);
        var decoded = new TileCell[256];
        TileChunkText.Decode(TileChunkText.Encode(cells).Replace("\n", "\r\n"), decoded);
        Assert.Equal(cells, decoded);
    }

    [Fact]
    public void A_text_without_the_last_line_end_is_read()
        => Assert.True(TileChunkText.TryDecode(EmptyChunk[..^1], new TileCell[256], out _));

    [Fact]
    public void A_cell_with_a_slot_and_no_tile_is_written_empty()
    {
        var cells = new TileCell[256];
        cells[15 * 16] = new TileCell(3, default);
        Assert.Equal(EmptyChunk, TileChunkText.Encode(cells));
    }
}
