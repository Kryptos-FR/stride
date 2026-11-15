using ColorRise.Skein.Assets.Localisation;

namespace ColorRise.Skein.Tests.Localisation;

/// <summary>
/// Strings files are Yarn Spinner's CSV: what its tools write, Skein reads, and the other way round.
/// </summary>
public sealed class YarnStringsCsvTests
{
    private const string Script = """
        title: Start
        ---
        Odile: The post is late. #line:post #mood:worried
        Odile: No id here.
        -> Wait, "friend". #line:wait
            Odile: Then we wait. #line:then
        Odile: The post is late. #shadow:post
        ===
        """;

    [Fact]
    public void The_rows_of_the_base_language_are_the_lines_with_an_id_of_their_own()
    {
        var rows = YarnStringsCsv.RowsOf(Scripts.Compile(Script), "en");

        Assert.Equal(["line:post", "line:wait", "line:then"], rows.Select(r => r.Id));
        var post = rows[0];
        Assert.Equal("en", post.Language);
        Assert.Equal("Odile: The post is late.", post.Text);
        Assert.Equal("Test.yarn", post.File);
        Assert.Equal("Start", post.Node);
        Assert.Equal(3, post.LineNumber);
        Assert.Equal(YarnStringsCsv.LockOf("Odile: The post is late."), post.Lock);
        Assert.Equal("Line metadata: mood:worried", post.Comment);
        Assert.Equal("", rows[1].Comment);
    }

    [Fact]
    public void The_lock_of_a_text_is_the_one_of_Yarn_Spinners_tools()
    {
        // The first eight digits of the SHA-256 of the text
        Assert.Equal("185f8db3", YarnStringsCsv.LockOf("Hello"));
        Assert.Equal("e3b0c442", YarnStringsCsv.LockOf(""));
    }

    [Fact]
    public void A_file_is_written_with_the_columns_of_Yarn_Spinner_and_quotes_where_needed()
    {
        var rows = YarnStringsCsv.RowsOf(Scripts.Compile(Script), "en");

        var csv = YarnStringsCsv.Write(rows);

        var lines = csv.Split("\r\n");
        Assert.Equal("language,id,text,file,node,lineNumber,lock,comment", lines[0]);
        Assert.Equal($"en,line:post,Odile: The post is late.,Test.yarn,Start,3,{rows[0].Lock},Line metadata: mood:worried", lines[1]);
        Assert.Equal($"en,line:wait,\"Wait, \"\"friend\"\".\",Test.yarn,Start,5,{rows[1].Lock},", lines[2]);
        Assert.Equal("", lines[^1]);
        Assert.Equal(5, lines.Length);
    }

    [Fact]
    public void A_text_with_commas_quotes_line_breaks_and_spaces_at_its_ends_comes_back_the_same()
    {
        YarnStringRow[] rows =
        [
            new() { Language = "fr", Id = "line:a", Text = "Attends, « ami », dit-elle : \"non\".", File = "Dialogue/Act 1.yarn", Node = "Start", LineNumber = 12, Lock = "0a1b2c3d", Comment = "two\nlines" },
            new() { Language = "fr", Id = "line:b", Text = "  spaces  ", LineNumber = 3 },
            new() { Language = "fr", Id = "line:c", Text = "" },
            new() { Language = "fr", Id = "line:d", Text = "\"" },
        ];

        var read = YarnStringsCsv.Read(YarnStringsCsv.Write(rows));

        Assert.Equal(rows, read);
    }

    [Fact]
    public void A_file_of_another_tool_reads_by_the_names_of_its_columns()
    {
        // Columns in another order, one missing, one unknown, Unix line ends, a byte order mark, a blank line
        var csv = "﻿id,Text,language,notes,lineNumber\nline:a,\"Bonjour, toi\",fr,checked,7\n\nline:b,Salut,fr,,x\n";

        var rows = YarnStringsCsv.Read(csv);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new YarnStringRow { Language = "fr", Id = "line:a", Text = "Bonjour, toi", LineNumber = 7 }, rows[0]);
        Assert.Equal(new YarnStringRow { Language = "fr", Id = "line:b", Text = "Salut" }, rows[1]);
        Assert.Empty(YarnStringsCsv.Read(""));
    }

    [Fact]
    public void A_text_that_is_not_a_strings_file_is_refused_with_the_reason()
    {
        Assert.Contains("no 'id' column", Assert.Throws<FormatException>(() => YarnStringsCsv.Read("name,value\na,b\n")).Message);
        Assert.Contains("quote that is not closed", Assert.Throws<FormatException>(() => YarnStringsCsv.Read("id,text\nline:a,\"open\n")).Message);
    }

    [Fact]
    public void Updating_a_strings_file_adds_removes_and_marks_as_Yarn_Spinners_tools_do()
    {
        var before = YarnStringsCsv.RowsOf(Scripts.Compile("title: Start\n---\nOne. #line:one\nTwo. #line:two\nThree. #line:three\n===\n"), "en");
        YarnStringRow[] french =
        [
            before[0] with { Language = "fr", Text = "Un." },
            before[1] with { Language = "fr", Text = "Deux." },
            before[2] with { Language = "fr", Text = "Trois." },
        ];

        // One line changed, one deleted, one new, and the lines moved
        var after = YarnStringsCsv.RowsOf(Scripts.Compile("title: Start\n---\nFour. #line:four\nOne! #line:one\nThree. #line:three #mood:calm\n===\n"), "en");
        var update = YarnStringsUpdate.Update(french, after, "fr");

        Assert.True(update.Changed);
        Assert.Equal((1, 1, 1), (update.Added, update.Removed, update.NeedsUpdate));
        Assert.Equal(["line:four", "line:one", "line:three"], update.Rows.Select(r => r.Id));
        Assert.Equal(["", "(NEEDS UPDATE) Un.", "Trois."], update.Rows.Select(r => r.Text));
        Assert.All(update.Rows, r => Assert.Equal("fr", r.Language));
        Assert.Equal(after.Select(r => r.Lock), update.Rows.Select(r => r.Lock));
        Assert.Equal([3, 4, 5], update.Rows.Select(r => r.LineNumber));
        Assert.Equal("Line metadata: mood:calm", update.Rows[2].Comment);

        // Updated once more, nothing is left to do, and a line already marked is not marked twice
        var again = YarnStringsUpdate.Update(update.Rows, after, "fr");
        Assert.False(again.Changed);
        Assert.Equal(update.Rows, again.Rows);

        var changedAgain = YarnStringsCsv.RowsOf(Scripts.Compile("title: Start\n---\nFour. #line:four\nOne!! #line:one\nThree. #line:three #mood:calm\n===\n"), "en");
        var third = YarnStringsUpdate.Update(update.Rows, changedAgain, "fr");
        Assert.Equal("(NEEDS UPDATE) Un.", third.Rows[1].Text);
        Assert.Equal("", third.Rows[0].Text);
    }
}
