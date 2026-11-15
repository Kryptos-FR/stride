using System.Globalization;
using Yarn.Markup;

namespace ColorRise.Skein.UI;

/// <summary>
/// How a <see cref="Typewriter"/> shows a line.
/// </summary>
public enum TypewriterMode
{
    /// <summary>
    /// All at once.
    /// </summary>
    Instant,

    /// <summary>
    /// Letter by letter.
    /// </summary>
    Letter,

    /// <summary>
    /// Word by word.
    /// </summary>
    Word,
}

/// <summary>
/// Shows the text of a line little by little: letter by letter or word by word at a speed, holding where the markup has
/// <c>[pause=500/]</c> (milliseconds), and telling the other markers as the text passes them.
/// </summary>
public sealed class Typewriter
{
    /// <summary>
    /// The marker that holds the text: <c>[pause=500/]</c>, in milliseconds.
    /// </summary>
    public const string PauseMarker = "pause";

    // What a step may fall short by and still count: sixty steps of a sixtieth are a second
    private const double Tolerance = 1e-9;

    private readonly string text;
    private readonly int[] unitEnds;
    private readonly MarkupAttribute[] markers;
    private readonly double secondsPerUnit;
    private int shownUnits;
    private int nextMarker;
    private double typed;
    private double pauseLeft;

    /// <summary>
    /// Makes a typewriter at the start of a text.
    /// </summary>
    /// <param name="text">The text with its markup read, as <see cref="LocalizedLine.TextWithoutCharacterName"/>.</param>
    /// <param name="mode">Letter by letter, word by word, or all at once.</param>
    /// <param name="unitsPerSecond">Letters or words per second; zero or less shows all at once.</param>
    public Typewriter(MarkupParseResult text, TypewriterMode mode, float unitsPerSecond)
    {
        this.text = text.Text ?? "";
        markers = [.. (text.Attributes ?? []).OrderBy(a => a.Position)];
        if (mode == TypewriterMode.Instant || unitsPerSecond <= 0)
        {
            unitEnds = [];
        }
        else
        {
            unitEnds = mode == TypewriterMode.Word ? WordEnds(this.text) : LetterEnds(this.text);
            secondsPerUnit = 1.0 / unitsPerSecond;
        }
    }

    /// <summary>
    /// The text passed a marker other than <see cref="PauseMarker"/>: a game's own inline events
    /// (<c>[shake/]</c>, <c>[sound name=bell/]</c>). Each marker is told once, <see cref="Complete"/> tells the rest.
    /// </summary>
    public event Action<MarkupAttribute>? MarkerReached;

    /// <summary>
    /// The text shown so far.
    /// </summary>
    public string VisibleText => text[..VisibleLength];

    /// <summary>
    /// How many characters of the text are shown.
    /// </summary>
    public int VisibleLength { get; private set; }

    /// <summary>
    /// Whether all the text is shown and its last pause is over.
    /// </summary>
    public bool IsComplete => VisibleLength == text.Length && nextMarker == markers.Length && pauseLeft <= 0;

    /// <summary>
    /// Lets time pass.
    /// </summary>
    /// <param name="elapsed">The time since the last call.</param>
    /// <returns>The text shown now.</returns>
    public string Advance(TimeSpan elapsed)
    {
        if (unitEnds.Length == 0 && VisibleLength < text.Length)
        {
            Complete();
            return text;
        }

        var time = Math.Max(elapsed.TotalSeconds, 0);
        while (true)
        {
            PassMarkers();
            if (pauseLeft > 0)
            {
                var held = Math.Min(time, pauseLeft);
                pauseLeft -= held;
                time -= held;
                if (pauseLeft > Tolerance)
                    break;

                pauseLeft = 0;
                continue;
            }

            if (shownUnits == unitEnds.Length)
                break;

            var needed = secondsPerUnit - typed;
            if (time + Tolerance < needed)
            {
                typed += time;
                break;
            }

            time = Math.Max(time - needed, 0);
            typed = 0;
            VisibleLength = unitEnds[shownUnits++];
        }

        return VisibleText;
    }

    /// <summary>
    /// Shows all the text now, pauses skipped: what "hurry up" asks.
    /// </summary>
    public void Complete()
    {
        VisibleLength = text.Length;
        shownUnits = unitEnds.Length;
        typed = 0;
        PassMarkers();
        pauseLeft = 0;
    }

    /// <summary>
    /// Takes the markers the shown text reached: a pause adds its time, another marker is told.
    /// </summary>
    private void PassMarkers()
    {
        while (nextMarker < markers.Length && markers[nextMarker].Position <= VisibleLength)
        {
            var marker = markers[nextMarker++];
            if (marker.Name != PauseMarker)
                MarkerReached?.Invoke(marker);
            else if (marker.TryGetProperty(PauseMarker, out float milliseconds) && milliseconds > 0)
                pauseLeft += milliseconds / 1000.0;
        }
    }

    private static int[] LetterEnds(string text)
    {
        // A letter is what a reader sees as one: an emoji, a letter with its accents
        var starts = StringInfo.ParseCombiningCharacters(text);
        var ends = new int[starts.Length];
        for (var i = 0; i < starts.Length; i++)
        {
            ends[i] = i + 1 < starts.Length ? starts[i + 1] : text.Length;
        }

        return ends;
    }

    private static int[] WordEnds(string text)
    {
        var ends = new List<int>();
        var inWord = false;
        for (var i = 0; i < text.Length; i++)
        {
            var isSpace = char.IsWhiteSpace(text[i]);
            if (inWord && isSpace)
                ends.Add(i);
            inWord = !isSpace;
        }

        // The last word, or the spaces that end the text, close it
        if (ends.Count == 0 || ends[^1] != text.Length)
        {
            if (inWord || ends.Count == 0)
                ends.Add(text.Length);
            else
                ends[^1] = text.Length;
        }

        return [.. ends];
    }
}
