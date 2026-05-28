namespace RaceEngineer.Core.Voice;

public static class MicrophonePcmConverter
{
    public const int TargetSampleRate = 16_000;
    private const short ClippingThreshold = 30_000;
    private const float QuietConvertedPeakRms = 50f;

    public static CapturedAudioEncoding ParseEncoding(string? encodingName)
    {
        if (string.IsNullOrWhiteSpace(encodingName))
        {
            return CapturedAudioEncoding.Unknown;
        }

        if (encodingName.Contains("IeeeFloat", StringComparison.OrdinalIgnoreCase)
            || encodingName.Contains("Float", StringComparison.OrdinalIgnoreCase))
        {
            return CapturedAudioEncoding.IeeeFloat;
        }

        if (encodingName.Contains("Pcm", StringComparison.OrdinalIgnoreCase))
        {
            return CapturedAudioEncoding.Pcm;
        }

        return CapturedAudioEncoding.Unknown;
    }

    public static CapturedAudioEncoding InferEncoding(int bitsPerSample, int channels, int blockAlign = 0)
    {
        if (blockAlign > 0 && channels > 0)
        {
            var bytesPerSample = blockAlign / Math.Max(1, channels);
            if (bytesPerSample == 4)
            {
                return CapturedAudioEncoding.IeeeFloat;
            }

            if (bytesPerSample == 2)
            {
                return CapturedAudioEncoding.Pcm;
            }
        }

        return bitsPerSample switch
        {
            32 => CapturedAudioEncoding.IeeeFloat,
            16 => CapturedAudioEncoding.Pcm,
            _ => CapturedAudioEncoding.Unknown
        };
    }

    public static ConvertedPcmChunk ConvertToWhisperPcm16(
        byte[] buffer,
        int offset,
        int bytesRecorded,
        CapturedAudioFormat format)
    {
        if (bytesRecorded <= 0)
        {
            return EmptyChunk();
        }

        var resolvedEncoding = ResolveEncoding(format);
        var chunk = ConvertUsingEncoding(buffer, offset, bytesRecorded, format, resolvedEncoding);
        if (chunk.ConvertedPeakRms < QuietConvertedPeakRms
            && format.BitsPerSample == 32
            && resolvedEncoding != CapturedAudioEncoding.IeeeFloat)
        {
            var floatChunk = ConvertUsingEncoding(buffer, offset, bytesRecorded, format, CapturedAudioEncoding.IeeeFloat);
            if (floatChunk.ConvertedPeakRms > chunk.ConvertedPeakRms * 4f)
            {
                chunk = floatChunk;
            }
        }

        return chunk;
    }

    public static float ComputeRms16(byte[] pcm16)
    {
        if (pcm16.Length < 2)
        {
            return 0f;
        }

        double sumSquares = 0d;
        var sampleCount = pcm16.Length / 2;
        for (var index = 0; index < sampleCount; index++)
        {
            var sample = BitConverter.ToInt16(pcm16, index * 2);
            sumSquares += sample * sample;
        }

        return (float)Math.Sqrt(sumSquares / sampleCount);
    }

    public static bool HasClipping(byte[] pcm16)
    {
        for (var index = 0; index + 1 < pcm16.Length; index += 2)
        {
            var sample = BitConverter.ToInt16(pcm16, index);
            if (Math.Abs(sample) >= ClippingThreshold)
            {
                return true;
            }
        }

        return false;
    }

    public static float[] Pcm16ToFloat(byte[] pcm16)
    {
        var samples = new float[pcm16.Length / 2];
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = BitConverter.ToInt16(pcm16, index * 2) / 32768f;
        }

        return samples;
    }

    public static ConvertedPcmChunk AppendPcm16(IReadOnlyList<byte> left, IReadOnlyList<byte> right)
    {
        if (left.Count == 0)
        {
            var rightArray = right.ToArray();
            var peak = ComputeRms16(rightArray);
            return new ConvertedPcmChunk(rightArray, peak, peak, peak, HasClipping(rightArray));
        }

        var merged = new byte[left.Count + right.Count];
        for (var index = 0; index < left.Count; index++)
        {
            merged[index] = left[index];
        }

        for (var index = 0; index < right.Count; index++)
        {
            merged[left.Count + index] = right[index];
        }

        var mergedPeak = ComputeRms16(merged);
        return new ConvertedPcmChunk(merged, mergedPeak, mergedPeak, mergedPeak, HasClipping(merged));
    }

    private static CapturedAudioEncoding ResolveEncoding(CapturedAudioFormat format)
    {
        if (format.Encoding != CapturedAudioEncoding.Unknown)
        {
            return format.Encoding;
        }

        return InferEncoding(format.BitsPerSample, format.Channels, format.BlockAlign);
    }

    private static ConvertedPcmChunk ConvertUsingEncoding(
        byte[] buffer,
        int offset,
        int bytesRecorded,
        CapturedAudioFormat format,
        CapturedAudioEncoding encoding)
    {
        var monoFloats = DecodeToMonoFloats(buffer, offset, bytesRecorded, format, encoding);
        if (monoFloats.Length == 0)
        {
            return EmptyChunk();
        }

        var rawPeakRms = ComputeRawPeakRms(monoFloats);
        var resampled = Resample(monoFloats, format.SampleRate, TargetSampleRate);
        var pcm16 = FloatsToPcm16(resampled);
        var convertedPeakRms = ComputeRms16(pcm16);
        var clipping = HasClipping(pcm16);
        return new ConvertedPcmChunk(pcm16, rawPeakRms, convertedPeakRms, convertedPeakRms, clipping);
    }

    private static float ComputeRawPeakRms(IReadOnlyList<float> monoFloats)
    {
        if (monoFloats.Count == 0)
        {
            return 0f;
        }

        double sumSquares = 0d;
        for (var index = 0; index < monoFloats.Count; index++)
        {
            var scaled = monoFloats[index] * short.MaxValue;
            sumSquares += scaled * scaled;
        }

        return (float)Math.Sqrt(sumSquares / monoFloats.Count);
    }

    private static float[] DecodeToMonoFloats(
        byte[] buffer,
        int offset,
        int bytesRecorded,
        CapturedAudioFormat format,
        CapturedAudioEncoding encoding)
    {
        return encoding switch
        {
            CapturedAudioEncoding.IeeeFloat => DecodeIeeeFloatMono(buffer, offset, bytesRecorded, format.Channels),
            CapturedAudioEncoding.Pcm when format.BitsPerSample == 16 => DecodePcm16Mono(buffer, offset, bytesRecorded, format.Channels),
            CapturedAudioEncoding.Pcm when format.BitsPerSample == 32 => DecodePcm32Mono(buffer, offset, bytesRecorded, format.Channels),
            _ => format.BitsPerSample switch
            {
                32 => DecodeIeeeFloatMono(buffer, offset, bytesRecorded, format.Channels),
                _ => DecodePcm16Mono(buffer, offset, bytesRecorded, format.Channels)
            }
        };
    }

    private static float[] DecodeIeeeFloatMono(byte[] buffer, int offset, int count, int channels)
    {
        channels = Math.Max(1, channels);
        var frameCount = count / (4 * channels);
        if (frameCount <= 0)
        {
            return [];
        }

        var output = new float[frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            double sum = 0d;
            for (var channel = 0; channel < channels; channel++)
            {
                var sampleOffset = offset + (frame * channels + channel) * 4;
                sum += BitConverter.ToSingle(buffer, sampleOffset);
            }

            output[frame] = (float)(sum / channels);
        }

        return output;
    }

    private static float[] DecodePcm16Mono(byte[] buffer, int offset, int count, int channels)
    {
        channels = Math.Max(1, channels);
        var frameCount = count / (2 * channels);
        if (frameCount <= 0)
        {
            return [];
        }

        var output = new float[frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            double sum = 0d;
            for (var channel = 0; channel < channels; channel++)
            {
                var sampleOffset = offset + (frame * channels + channel) * 2;
                sum += BitConverter.ToInt16(buffer, sampleOffset) / 32768d;
            }

            output[frame] = (float)(sum / channels);
        }

        return output;
    }

    private static float[] DecodePcm32Mono(byte[] buffer, int offset, int count, int channels)
    {
        channels = Math.Max(1, channels);
        var frameCount = count / (4 * channels);
        if (frameCount <= 0)
        {
            return [];
        }

        var output = new float[frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            double sum = 0d;
            for (var channel = 0; channel < channels; channel++)
            {
                var sampleOffset = offset + (frame * channels + channel) * 4;
                sum += BitConverter.ToInt32(buffer, sampleOffset) / 2147483648d;
            }

            output[frame] = (float)(sum / channels);
        }

        return output;
    }

    private static float[] Resample(IReadOnlyList<float> samples, int sourceRate, int targetRate)
    {
        if (samples.Count == 0 || sourceRate <= 0 || targetRate <= 0 || sourceRate == targetRate)
        {
            return samples.Count == 0 ? [] : samples.ToArray();
        }

        var outputLength = Math.Max(1, (int)Math.Round(samples.Count * (double)targetRate / sourceRate));
        var output = new float[outputLength];
        var ratio = (double)(samples.Count - 1) / Math.Max(1, outputLength - 1);
        for (var index = 0; index < outputLength; index++)
        {
            var position = index * ratio;
            var left = (int)Math.Floor(position);
            var right = Math.Min(left + 1, samples.Count - 1);
            var fraction = position - left;
            output[index] = (float)((1d - fraction) * samples[left] + fraction * samples[right]);
        }

        return output;
    }

    private static byte[] FloatsToPcm16(IReadOnlyList<float> samples)
    {
        var pcm = new byte[samples.Count * 2];
        for (var index = 0; index < samples.Count; index++)
        {
            var clamped = Math.Clamp(samples[index], -1f, 1f);
            var value = (short)Math.Round(clamped * short.MaxValue);
            BitConverter.TryWriteBytes(pcm.AsSpan(index * 2, 2), value);
        }

        return pcm;
    }

    private static ConvertedPcmChunk EmptyChunk() =>
        new([], 0f, 0f, 0f, false);
}
