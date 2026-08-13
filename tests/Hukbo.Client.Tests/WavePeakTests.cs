using Hukbo.Client.Audio;

namespace Hukbo.Client.Tests;

public sealed class WavePeakTests
{
    [Fact]
    public void TryReadPeak_FullScaleMonoSample_ReturnsPeakOfOne()
    {
        var wav = BuildPcm16Wav(channelCount: 1, samples: [short.MinValue, 0, 100]);

        var succeeded = WavePeak.TryReadPeak(wav, out var peak);

        Assert.True(succeeded);
        Assert.Equal(1.0f, peak);
    }

    [Fact]
    public void TryReadPeak_HalfScaleSample_ReturnsHalfPeak()
    {
        var wav = BuildPcm16Wav(channelCount: 1, samples: [16384, -1000, 500]);

        var succeeded = WavePeak.TryReadPeak(wav, out var peak);

        Assert.True(succeeded);
        Assert.Equal(0.5f, peak);
    }

    [Fact]
    public void TryReadPeak_Silence_ReturnsZeroPeakAndTrue()
    {
        var wav = BuildPcm16Wav(channelCount: 1, samples: [0, 0, 0, 0]);

        var succeeded = WavePeak.TryReadPeak(wav, out var peak);

        Assert.True(succeeded);
        Assert.Equal(0f, peak);
    }

    [Fact]
    public void TryReadPeak_StereoFile_ReturnsPeakAcrossBothChannels()
    {
        // Left channel stays quiet; the right channel carries the loud sample.
        // A reader that only looked at one channel would miss this peak.
        var wav = BuildPcm16Wav(
            channelCount: 2,
            samples: [100, short.MaxValue, -200, 300]);

        var succeeded = WavePeak.TryReadPeak(wav, out var peak);

        Assert.True(succeeded);
        Assert.Equal(short.MaxValue / 32768f, peak);
    }

    [Fact]
    public void TryReadPeak_UnsupportedBitDepth_ReturnsFalse()
    {
        var wav = BuildWav(
            formatTag: 1,
            channelCount: 1,
            bitsPerSample: 24,
            dataBytes: new byte[6]);

        var succeeded = WavePeak.TryReadPeak(wav, out var peak);

        Assert.False(succeeded);
        Assert.Equal(0f, peak);
    }

    [Fact]
    public void TryReadPeak_NonPcmFormatTag_ReturnsFalse()
    {
        // 3 is IEEE float, the format ElevenLabs never writes and this helper
        // deliberately does not support.
        var wav = BuildWav(
            formatTag: 3,
            channelCount: 1,
            bitsPerSample: 16,
            dataBytes: new byte[4]);

        var succeeded = WavePeak.TryReadPeak(wav, out var peak);

        Assert.False(succeeded);
        Assert.Equal(0f, peak);
    }

    [Fact]
    public void TryReadPeak_MissingDataChunk_ReturnsFalse()
    {
        var wav = BuildWav(
            formatTag: 1,
            channelCount: 1,
            bitsPerSample: 16,
            dataBytes: null);

        var succeeded = WavePeak.TryReadPeak(wav, out var peak);

        Assert.False(succeeded);
        Assert.Equal(0f, peak);
    }

    [Fact]
    public void TryReadPeak_TruncatedBuffer_ReturnsFalse()
    {
        var wav = BuildPcm16Wav(channelCount: 1, samples: [short.MinValue, 1000]);
        var truncated = wav[..^4];

        var succeeded = WavePeak.TryReadPeak(truncated, out var peak);

        Assert.False(succeeded);
        Assert.Equal(0f, peak);
    }

    [Fact]
    public void TryReadPeak_NotRiff_ReturnsFalse()
    {
        var notRiff = new byte[16];

        var succeeded = WavePeak.TryReadPeak(notRiff, out var peak);

        Assert.False(succeeded);
        Assert.Equal(0f, peak);
    }

    private static byte[] BuildPcm16Wav(ushort channelCount, short[] samples)
    {
        var dataBytes = new byte[samples.Length * 2];
        for (var index = 0; index < samples.Length; index++)
        {
            BitConverter.GetBytes(samples[index]).CopyTo(dataBytes, index * 2);
        }

        return BuildWav(
            formatTag: 1,
            channelCount: channelCount,
            bitsPerSample: 16,
            dataBytes: dataBytes);
    }

    /// <summary>
    /// Assembles a minimal RIFF/WAVE byte array: the twelve-byte RIFF header,
    /// a sixteen-byte <c>fmt </c> chunk, and, unless <paramref name="dataBytes"/>
    /// is <see langword="null"/>, a <c>data</c> chunk holding it verbatim.
    /// </summary>
    private static byte[] BuildWav(
        ushort formatTag,
        ushort channelCount,
        ushort bitsPerSample,
        byte[]? dataBytes)
    {
        const uint sampleRate = 44100;
        var blockAlign = (ushort)(channelCount * (bitsPerSample / 8));
        var byteRate = sampleRate * blockAlign;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        var fmtBody = new byte[16];
        using (var fmtStream = new MemoryStream(fmtBody))
        using (var fmtWriter = new BinaryWriter(fmtStream))
        {
            fmtWriter.Write(formatTag);
            fmtWriter.Write(channelCount);
            fmtWriter.Write(sampleRate);
            fmtWriter.Write(byteRate);
            fmtWriter.Write(blockAlign);
            fmtWriter.Write(bitsPerSample);
        }

        var chunksLength = 8 + fmtBody.Length + (dataBytes is null ? 0 : 8 + dataBytes.Length);

        writer.Write("RIFF"u8);
        writer.Write(4 + chunksLength);
        writer.Write("WAVE"u8);

        writer.Write("fmt "u8);
        writer.Write(fmtBody.Length);
        writer.Write(fmtBody);

        if (dataBytes is not null)
        {
            writer.Write("data"u8);
            writer.Write(dataBytes.Length);
            writer.Write(dataBytes);
        }

        writer.Flush();
        return stream.ToArray();
    }
}
