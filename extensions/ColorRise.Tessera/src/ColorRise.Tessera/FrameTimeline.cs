namespace ColorRise.Tessera;

/// <summary>
/// Which frame of a list a time shows, for the animated tiles and the clips of flipbooks. A frame whose duration is not
/// above 0 is never shown.
/// </summary>
internal static class FrameTimeline
{
    // A time this close to the end of a frame is past it: a duration is a float, and 0.1 written in a file is a little more
    // than the double 0.1 a game adds up
    private const double Tolerance = 1e-6;

    /// <summary>
    /// How long one pass lasts: the durations above 0 added up; for a ping-pong, there and back, each end frame once.
    /// </summary>
    public static double PassLength<T>(IReadOnlyList<T> frames, Func<T, float> duration, FlipbookLoopMode mode)
    {
        var (pass, first, last, lasting) = (0.0, -1, -1, 0);
        for (var i = 0; i < frames.Count; i++)
        {
            var length = duration(frames[i]);
            if (length > 0)
            {
                pass += length;
                (first, last, lasting) = (first < 0 ? i : first, i, lasting + 1);
            }
        }
        if (mode == FlipbookLoopMode.PingPong && lasting > 2)
        {
            // Back from the one before the last to the one after the first
            pass += pass - duration(frames[first]) - duration(frames[last]);
        }
        return pass;
    }

    /// <summary>
    /// The index of the frame shown at a time, in seconds: -1 without a frame; the first frame when no frame lasts or the
    /// time is not finite. In a loop and a ping-pong a negative time counts back from the end of the pass; a <c>Once</c>
    /// shows its first frame that lasts before 0, and its last after its pass.
    /// </summary>
    public static int IndexAt<T>(IReadOnlyList<T> frames, Func<T, float> duration, double time, FlipbookLoopMode mode)
    {
        if (frames.Count == 0)
        {
            return -1;
        }
        var pass = PassLength(frames, duration, mode);
        if (!(pass > 0) || !double.IsFinite(time))
        {
            return 0;
        }

        double left;
        if (mode == FlipbookLoopMode.Once)
        {
            if (time >= pass - Tolerance)
            {
                return LastLasting(frames, duration);
            }
            left = Math.Max(time, 0);
        }
        else
        {
            left = time % pass;
            if (left < 0)
            {
                left += pass;
            }
            if (left >= pass - Tolerance)
            {
                left = 0;
            }
        }

        // There
        for (var i = 0; i < frames.Count; i++)
        {
            var length = duration(frames[i]);
            if (length > 0)
            {
                if (left < length - Tolerance)
                {
                    return i;
                }
                left -= length;
            }
        }
        if (mode != FlipbookLoopMode.PingPong)
        {
            // Rounding left a sliver past the last frame
            return LastLasting(frames, duration);
        }

        // And back, from the one before the last to the one after the first
        var (first, last) = (FirstLasting(frames, duration), LastLasting(frames, duration));
        var shown = last;
        for (var i = last - 1; i > first; i--)
        {
            var length = duration(frames[i]);
            if (length > 0)
            {
                shown = i;
                if (left < length - Tolerance)
                {
                    return i;
                }
                left -= length;
            }
        }
        return shown;
    }

    private static int LastLasting<T>(IReadOnlyList<T> frames, Func<T, float> duration)
    {
        for (var i = frames.Count - 1; i >= 0; i--)
        {
            if (duration(frames[i]) > 0)
            {
                return i;
            }
        }
        return 0;
    }

    private static int FirstLasting<T>(IReadOnlyList<T> frames, Func<T, float> duration)
    {
        for (var i = 0; i < frames.Count; i++)
        {
            if (duration(frames[i]) > 0)
            {
                return i;
            }
        }
        return 0;
    }
}
