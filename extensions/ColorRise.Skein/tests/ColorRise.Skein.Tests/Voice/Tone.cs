namespace ColorRise.Skein.Tests.Voice;

/// <summary>
/// Writes a sine tone as a WAV file, the sound of a test's voice-over clip.
/// </summary>
internal static class Tone
{
    public static byte[] Wave(double seconds, double frequency, int sampleRate = 22050)
    {
        var samples = (int)(seconds * sampleRate);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + samples * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(samples * 2);
        for (var i = 0; i < samples; i++)
        {
            writer.Write((short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * short.MaxValue * 0.3));
        }

        writer.Flush();
        return stream.ToArray();
    }
}
