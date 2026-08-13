using System.Buffers.Binary;

namespace Hukbo.Client.Audio;

/// <summary>
/// Reads an uncompressed PCM WAV file's raw bytes and, where the sample-domain
/// normalisation in <see cref="MonoGameSoundPlayer"/> needs it, hands back
/// what is needed to rebuild the clip: the sample rate, the channel count, and
/// the PCM sample data itself, alongside the peak amplitude this type already
/// measured. Pure and allocation-light: no file handle, no MonoGame type, and
/// no exception on malformed input — every unsupported or truncated case
/// simply returns <see langword="false"/>, because a bad take must never stop
/// the game from starting.
/// </summary>
internal static class WavePeak
{
    private const ushort PcmFormatTag = 1;
    private const ushort SupportedBitsPerSample = 16;

    /// <summary>
    /// Walks the RIFF chunk list looking for a <c>fmt </c> chunk describing
    /// 16-bit PCM, mono or stereo, and a <c>data</c> chunk holding the samples.
    /// <paramref name="peak"/> is the largest absolute sample value across every
    /// channel and every frame, scaled to <c>[0, 1]</c> against the full 16-bit
    /// range. Silence reads as a peak of <c>0</c> and is a valid, successful
    /// result.
    /// </summary>
    public static bool TryReadPeak(ReadOnlySpan<byte> wavBytes, out float peak)
    {
        peak = 0f;

        if (!TryParseHeader(wavBytes, out _, out _, out var dataChunk))
        {
            return false;
        }

        peak = ComputePeak(dataChunk);
        return true;
    }

    /// <summary>
    /// The same chunk walk as <see cref="TryReadPeak"/>, but also hands back
    /// the format fields and the raw sample bytes needed to rebuild a scaled
    /// clip via <see cref="ApplyGain"/> and MonoGame's
    /// <c>SoundEffect(byte[], int, AudioChannels)</c> constructor. See
    /// <c>docs/plans/2026-08-13-shield-clash-legibility-design.md</c>
    /// section 3.
    /// </summary>
    public static bool TryReadPcm(
        ReadOnlySpan<byte> wavBytes,
        out int sampleRate,
        out int channelCount,
        out ReadOnlySpan<byte> pcmData,
        out float peak)
    {
        sampleRate = 0;
        channelCount = 0;
        pcmData = default;
        peak = 0f;

        if (!TryParseHeader(wavBytes, out var parsedChannelCount, out var parsedSampleRate, out var dataChunk))
        {
            return false;
        }

        sampleRate = (int)parsedSampleRate;
        channelCount = parsedChannelCount;
        pcmData = dataChunk;
        peak = ComputePeak(dataChunk);
        return true;
    }

    /// <summary>
    /// The gain multiplier that brings a take measured at <paramref name="peak"/>
    /// to <paramref name="referencePeak"/>, clamped to
    /// <c>[<paramref name="minimumScale"/>, <paramref name="maximumScale"/>]</c>
    /// so a near-silent take is not amplified into noise and a take already at
    /// or above the reference peak is only ever attenuated a little. The
    /// caller is responsible for never calling this with a non-positive
    /// <paramref name="peak"/>; <see cref="MonoGameSoundPlayer"/> falls back to
    /// the unmodified clip in that case instead.
    /// </summary>
    public static float ComputeScaleFactor(
        float peak,
        float referencePeak,
        float minimumScale,
        float maximumScale) =>
        Math.Clamp(referencePeak / peak, minimumScale, maximumScale);

    /// <summary>
    /// Scales every 16-bit little-endian sample in <paramref name="pcmData"/>
    /// by <paramref name="scale"/>, saturating each written sample to
    /// <c>[short.MinValue, short.MaxValue]</c> so a loud take scaled up can
    /// never wrap around. A well-formed 16-bit PCM buffer is always an even
    /// number of bytes; a trailing odd byte, which should never occur, is
    /// copied through unscaled rather than dropped.
    /// </summary>
    public static byte[] ApplyGain(ReadOnlySpan<byte> pcmData, float scale)
    {
        var scaled = new byte[pcmData.Length];
        var sampleCount = pcmData.Length / 2;

        for (var index = 0; index < sampleCount; index++)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(pcmData.Slice(index * 2, 2));
            var widened = sample * scale;
            var saturated = Math.Clamp(widened, (float)short.MinValue, (float)short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(
                scaled.AsSpan(index * 2, 2),
                (short)MathF.Round(saturated));
        }

        if (sampleCount * 2 < pcmData.Length)
        {
            scaled[^1] = pcmData[^1];
        }

        return scaled;
    }

    /// <summary>
    /// Parses the RIFF/WAVE header down to the <c>fmt </c> and <c>data</c>
    /// chunks, refusing anything but 16-bit mono or stereo PCM. Shared by
    /// <see cref="TryReadPeak"/> and <see cref="TryReadPcm"/> so the chunk walk
    /// exists exactly once.
    /// </summary>
    private static bool TryParseHeader(
        ReadOnlySpan<byte> wavBytes,
        out ushort channelCount,
        out uint sampleRate,
        out ReadOnlySpan<byte> dataChunk)
    {
        channelCount = 0;
        sampleRate = 0;
        dataChunk = default;

        if (wavBytes.Length < 12)
        {
            return false;
        }

        if (!wavBytes[..4].SequenceEqual("RIFF"u8) ||
            !wavBytes[8..12].SequenceEqual("WAVE"u8))
        {
            return false;
        }

        var offset = 12;
        var haveFormat = false;
        ushort formatTag = 0;
        ushort bitsPerSample = 0;
        var haveData = false;

        while (offset + 8 <= wavBytes.Length)
        {
            var chunkId = wavBytes.Slice(offset, 4);
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(
                wavBytes.Slice(offset + 4, 4));
            var chunkBodyOffset = offset + 8;

            // A chunk whose declared size runs past the buffer we were given is
            // a truncated file. Refuse rather than reading past what exists.
            if (chunkSize > int.MaxValue ||
                chunkBodyOffset + (int)chunkSize > wavBytes.Length)
            {
                return false;
            }

            var chunkBody = wavBytes.Slice(chunkBodyOffset, (int)chunkSize);

            if (chunkId.SequenceEqual("fmt "u8))
            {
                if (chunkBody.Length < 16)
                {
                    return false;
                }

                formatTag = BinaryPrimitives.ReadUInt16LittleEndian(chunkBody[0..2]);
                channelCount = BinaryPrimitives.ReadUInt16LittleEndian(chunkBody[2..4]);
                sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(chunkBody[4..8]);
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(chunkBody[14..16]);
                haveFormat = true;
            }
            else if (chunkId.SequenceEqual("data"u8))
            {
                dataChunk = chunkBody;
                haveData = true;
            }

            // Chunks are word-aligned: an odd-sized chunk body is followed by
            // one pad byte that is not part of any chunk's declared size.
            var advance = 8 + (int)chunkSize + ((chunkSize % 2 == 1) ? 1 : 0);
            offset += advance;
        }

        if (!haveFormat || !haveData)
        {
            return false;
        }

        if (formatTag != PcmFormatTag ||
            bitsPerSample != SupportedBitsPerSample ||
            (channelCount != 1 && channelCount != 2))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// The largest absolute sample value across every channel and every
    /// frame in <paramref name="dataChunk"/>, scaled to <c>[0, 1]</c> against
    /// the full 16-bit range.
    /// </summary>
    private static float ComputePeak(ReadOnlySpan<byte> dataChunk)
    {
        var sampleCount = dataChunk.Length / 2;
        var maxAbs = 0;
        for (var index = 0; index < sampleCount; index++)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(
                dataChunk.Slice(index * 2, 2));

            // Math.Abs(short.MinValue) overflows a short, so widen to int
            // first; short.MinValue's true magnitude, 32768, is also the exact
            // full-scale peak of 1.0f below.
            var magnitude = Math.Abs((int)sample);
            if (magnitude > maxAbs)
            {
                maxAbs = magnitude;
            }
        }

        return maxAbs / 32768f;
    }
}
