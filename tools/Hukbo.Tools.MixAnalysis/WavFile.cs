using System.Buffers.Binary;

namespace Hukbo.Tools.MixAnalysis;

/// <summary>
/// Minimal uncompressed PCM WAV reader and writer. Deliberately narrow: it
/// accepts exactly the format <c>SoundEffect.FromStream</c> accepts, so a file
/// this reader rejects is a file the game would also refuse to load.
/// </summary>
internal sealed record WavClip(
    string Name,
    int SampleRate,
    int Channels,
    float[] Samples)
{
    /// <summary>Frames, not samples: one frame carries every channel.</summary>
    public int FrameCount => Samples.Length / Channels;

    public double DurationSeconds => (double)FrameCount / SampleRate;
}

internal static class WavFile
{
    public static WavClip Read(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var name = Path.GetFileNameWithoutExtension(filePath);

        if (bytes.Length < 12 ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(0, 4)) != 0x52494646u ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) != 0x57415645u)
        {
            throw new InvalidDataException($"{name}: not a RIFF/WAVE file.");
        }

        var channels = 0;
        var sampleRate = 0;
        var bitsPerSample = 0;
        var offset = 12;
        ReadOnlySpan<byte> data = default;

        while (offset + 8 <= bytes.Length)
        {
            var chunkId = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            var chunkSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4, 4));
            var body = offset + 8;
            if (chunkSize < 0 || body + chunkSize > bytes.Length)
            {
                chunkSize = bytes.Length - body;
            }

            switch (chunkId)
            {
                case 0x666D7420u: // "fmt "
                    var format = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(body, 2));
                    if (format != 1)
                    {
                        throw new InvalidDataException(
                            $"{name}: format tag {format} is not uncompressed PCM.");
                    }

                    channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(body + 2, 2));
                    sampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(body + 4, 4));
                    bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(body + 14, 2));
                    break;

                case 0x64617461u: // "data"
                    data = bytes.AsSpan(body, chunkSize);
                    break;

                default:
                    break;
            }

            offset = body + chunkSize + (chunkSize & 1);
        }

        if (channels == 0 || sampleRate == 0)
        {
            throw new InvalidDataException($"{name}: no usable fmt chunk.");
        }

        if (bitsPerSample != 16)
        {
            throw new InvalidDataException(
                $"{name}: {bitsPerSample}-bit samples; only 16-bit is supported.");
        }

        var sampleCount = data.Length / 2;
        var samples = new float[sampleCount];
        for (var index = 0; index < sampleCount; index++)
        {
            var value = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(index * 2, 2));
            samples[index] = value / 32768f;
        }

        return new WavClip(name, sampleRate, channels, samples);
    }

    /// <summary>
    /// Writes a float buffer as 16-bit PCM. Values outside [-1, 1] are hard
    /// clipped, which is exactly what the audio device does to an overloaded
    /// mix — so a rendered file sounds like the real thing rather than hiding
    /// the overload behind a rescale.
    /// </summary>
    public static void Write(
        string filePath,
        int sampleRate,
        int channels,
        ReadOnlySpan<float> samples)
    {
        var dataBytes = samples.Length * 2;
        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataBytes);

        for (var index = 0; index < samples.Length; index++)
        {
            var clamped = Math.Clamp(samples[index], -1f, 1f);
            writer.Write((short)Math.Round(clamped * 32767f));
        }
    }
}
