using System.Buffers.Binary;

namespace Hukbo.Client.Audio;

/// <summary>
/// Reads the peak sample amplitude of an uncompressed PCM WAV file from its raw
/// bytes. Pure and allocation-light: no file handle, no MonoGame type, and no
/// exception on malformed input — every unsupported or truncated case simply
/// returns <see langword="false"/>, because a bad take must never stop the game
/// from starting.
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
        ushort channelCount = 0;
        ushort bitsPerSample = 0;
        ReadOnlySpan<byte> dataChunk = default;
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

        peak = maxAbs / 32768f;
        return true;
    }
}
