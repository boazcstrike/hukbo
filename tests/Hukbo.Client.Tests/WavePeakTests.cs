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

    [Fact]
    public void TryReadPcm_ValidMonoFile_ReturnsFormatDataAndPeak()
    {
        var wav = BuildPcm16Wav(channelCount: 1, samples: [16384, -1000, 500]);

        var succeeded = WavePeak.TryReadPcm(
            wav,
            out var sampleRate,
            out var channelCount,
            out var pcmData,
            out var peak);

        Assert.True(succeeded);
        Assert.Equal(44100, sampleRate);
        Assert.Equal(1, channelCount);
        Assert.Equal(0.5f, peak);
        Assert.Equal(6, pcmData.Length);
        Assert.Equal(
            (short)16384,
            System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(pcmData[..2]));
    }

    [Fact]
    public void TryReadPcm_TruncatedBuffer_ReturnsFalse()
    {
        var wav = BuildPcm16Wav(channelCount: 1, samples: [short.MinValue, 1000]);
        var truncated = wav[..^4];

        var succeeded = WavePeak.TryReadPcm(
            truncated,
            out var sampleRate,
            out var channelCount,
            out var pcmData,
            out var peak);

        Assert.False(succeeded);
        Assert.Equal(0, sampleRate);
        Assert.Equal(0, channelCount);
        Assert.Equal(0, pcmData.Length);
        Assert.Equal(0f, peak);
    }

    [Fact]
    public void ComputeScaleFactor_QuietTake_ScalesUpToTheReferencePeak()
    {
        // Take 04 of clash-shield-wasay from the design doc's measured table:
        // peak 0.096.
        var scale = WavePeak.ComputeScaleFactor(
            peak: 0.096f,
            referencePeak: 0.85f,
            minimumScale: 0.5f,
            maximumScale: 6.0f);

        // 0.85 / 0.096 = 8.854..., clamped down to the ceiling of 6.0.
        Assert.Equal(6.0f, scale);
    }

    [Fact]
    public void ComputeScaleFactor_FullScaleTake_IsAttenuatedNotAmplified()
    {
        var scale = WavePeak.ComputeScaleFactor(
            peak: 1.0f,
            referencePeak: 0.85f,
            minimumScale: 0.5f,
            maximumScale: 6.0f);

        Assert.Equal(0.85f, scale);
        Assert.True(scale < 1.0f);
    }

    [Fact]
    public void ComputeScaleFactor_ClampsAtTheMinimumBound()
    {
        var scale = WavePeak.ComputeScaleFactor(
            peak: 10.0f,
            referencePeak: 0.85f,
            minimumScale: 0.5f,
            maximumScale: 6.0f);

        Assert.Equal(0.5f, scale);
    }

    [Fact]
    public void ComputeScaleFactor_ClampsAtTheMaximumBound()
    {
        var scale = WavePeak.ComputeScaleFactor(
            peak: 0.001f,
            referencePeak: 0.85f,
            minimumScale: 0.5f,
            maximumScale: 6.0f);

        Assert.Equal(6.0f, scale);
    }

    [Fact]
    public void ApplyGain_QuietBuffer_RaisesItsPeakToTheReferencePeak()
    {
        // Peak sample 3146 / 32768 = 0.096, matching take 04 of
        // clash-shield-wasay in the design doc's measured table. Scaling by
        // 6.0 (the clamped factor for that take) lifts the peak to 18876,
        // which is 0.576 of full scale — audibly present rather than the
        // 0.096 a listener could not hear at all.
        var pcm = BuildPcmBytes([3146, -1000, 500]);

        var scaled = WavePeak.ApplyGain(pcm, scale: 6.0f);

        Assert.Equal(
            (short)18876,
            System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(scaled.AsSpan(0, 2)));
        Assert.Equal(
            (short)-6000,
            System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(scaled.AsSpan(2, 2)));
        Assert.Equal(
            (short)3000,
            System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(scaled.AsSpan(4, 2)));
    }

    [Fact]
    public void ApplyGain_FullScaleBuffer_IsAttenuated()
    {
        var pcm = BuildPcmBytes([short.MaxValue]);

        var scaled = WavePeak.ApplyGain(pcm, scale: 0.5f);

        Assert.Equal(
            (short)16384,
            System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(scaled.AsSpan(0, 2)));
    }

    [Fact]
    public void ApplyGain_PositiveSampleScaledPastFullScale_SaturatesAtShortMaxValueWithoutWrapping()
    {
        var pcm = BuildPcmBytes([short.MaxValue]);

        var scaled = WavePeak.ApplyGain(pcm, scale: 6.0f);

        var result = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(scaled.AsSpan(0, 2));
        Assert.Equal(short.MaxValue, result);
        Assert.True(result > 0);
    }

    [Fact]
    public void ApplyGain_NegativeSampleScaledPastFullScale_SaturatesAtShortMinValueWithoutWrapping()
    {
        var pcm = BuildPcmBytes([short.MinValue]);

        var scaled = WavePeak.ApplyGain(pcm, scale: 6.0f);

        var result = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(scaled.AsSpan(0, 2));
        Assert.Equal(short.MinValue, result);
        Assert.True(result < 0);
    }

    [Fact]
    public void ApplyGain_IdentityScale_LeavesEverySampleUnchanged()
    {
        var pcm = BuildPcmBytes([1234, -5678, 0, short.MaxValue, short.MinValue]);

        var scaled = WavePeak.ApplyGain(pcm, scale: 1.0f);

        Assert.Equal(pcm, scaled);
    }

    private static byte[] BuildPcmBytes(short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var index = 0; index < samples.Length; index++)
        {
            BitConverter.GetBytes(samples[index]).CopyTo(bytes, index * 2);
        }

        return bytes;
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
