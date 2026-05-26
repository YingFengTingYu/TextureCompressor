namespace TextureCompressor.Colors;

internal static class RgbaColorConversions
{
    public static byte ToUNorm8(byte value) => value;

    public static byte ToUNorm8(sbyte value) => (byte)ScaleSignedToUnsigned(value, sbyte.MaxValue, byte.MaxValue);

    public static byte ToUNorm8(ushort value) => (byte)ScaleUnsigned(value, ushort.MaxValue, byte.MaxValue);

    public static byte ToUNorm8(short value) => (byte)ScaleSignedToUnsigned(value, short.MaxValue, byte.MaxValue);

    public static byte ToUNorm8(uint value) => (byte)ScaleUnsigned(value, uint.MaxValue, byte.MaxValue);

    public static byte ToUNorm8(int value) => (byte)ScaleSignedToUnsigned(value, int.MaxValue, byte.MaxValue);

    public static byte ToUNorm8(Half value) => FloatToUNorm8((float)value);

    public static byte ToUNorm8(float value) => FloatToUNorm8(value);

    public static sbyte ToSNorm8(byte value) => (sbyte)ScaleUnsigned(value, byte.MaxValue, (ulong)sbyte.MaxValue);

    public static sbyte ToSNorm8(sbyte value) => value;

    public static sbyte ToSNorm8(ushort value) => (sbyte)ScaleUnsigned(value, ushort.MaxValue, (ulong)sbyte.MaxValue);

    public static sbyte ToSNorm8(short value) => (sbyte)ScaleSigned(value, short.MaxValue, sbyte.MaxValue);

    public static sbyte ToSNorm8(uint value) => (sbyte)ScaleUnsigned(value, uint.MaxValue, (ulong)sbyte.MaxValue);

    public static sbyte ToSNorm8(int value) => (sbyte)ScaleSigned(value, int.MaxValue, sbyte.MaxValue);

    public static sbyte ToSNorm8(Half value) => FloatToSNorm8((float)value);

    public static sbyte ToSNorm8(float value) => FloatToSNorm8(value);

    public static ushort ToUNorm16(byte value) => (ushort)ScaleUnsigned(value, byte.MaxValue, ushort.MaxValue);

    public static ushort ToUNorm16(sbyte value) => (ushort)ScaleSignedToUnsigned(value, sbyte.MaxValue, ushort.MaxValue);

    public static ushort ToUNorm16(ushort value) => value;

    public static ushort ToUNorm16(short value) => (ushort)ScaleSignedToUnsigned(value, short.MaxValue, ushort.MaxValue);

    public static ushort ToUNorm16(uint value) => (ushort)ScaleUnsigned(value, uint.MaxValue, ushort.MaxValue);

    public static ushort ToUNorm16(int value) => (ushort)ScaleSignedToUnsigned(value, int.MaxValue, ushort.MaxValue);

    public static ushort ToUNorm16(Half value) => FloatToUNorm16((float)value);

    public static ushort ToUNorm16(float value) => FloatToUNorm16(value);

    public static short ToSNorm16(byte value) => (short)ScaleUnsigned(value, byte.MaxValue, (ulong)short.MaxValue);

    public static short ToSNorm16(sbyte value) => (short)ScaleSigned(value, sbyte.MaxValue, short.MaxValue);

    public static short ToSNorm16(ushort value) => (short)ScaleUnsigned(value, ushort.MaxValue, (ulong)short.MaxValue);

    public static short ToSNorm16(short value) => value;

    public static short ToSNorm16(uint value) => (short)ScaleUnsigned(value, uint.MaxValue, (ulong)short.MaxValue);

    public static short ToSNorm16(int value) => (short)ScaleSigned(value, int.MaxValue, short.MaxValue);

    public static short ToSNorm16(Half value) => FloatToSNorm16((float)value);

    public static short ToSNorm16(float value) => FloatToSNorm16(value);

    public static uint ToUNorm32(byte value) => (uint)ScaleUnsigned(value, byte.MaxValue, uint.MaxValue);

    public static uint ToUNorm32(sbyte value) => (uint)ScaleSignedToUnsigned(value, sbyte.MaxValue, uint.MaxValue);

    public static uint ToUNorm32(ushort value) => (uint)ScaleUnsigned(value, ushort.MaxValue, uint.MaxValue);

    public static uint ToUNorm32(short value) => (uint)ScaleSignedToUnsigned(value, short.MaxValue, uint.MaxValue);

    public static uint ToUNorm32(uint value) => value;

    public static uint ToUNorm32(int value) => (uint)ScaleSignedToUnsigned(value, int.MaxValue, uint.MaxValue);

    public static uint ToUNorm32(Half value) => FloatToUNorm32((float)value);

    public static uint ToUNorm32(float value) => FloatToUNorm32(value);

    public static int ToSNorm32(byte value) => (int)ScaleUnsigned(value, byte.MaxValue, int.MaxValue);

    public static int ToSNorm32(sbyte value) => (int)ScaleSigned(value, sbyte.MaxValue, int.MaxValue);

    public static int ToSNorm32(ushort value) => (int)ScaleUnsigned(value, ushort.MaxValue, int.MaxValue);

    public static int ToSNorm32(short value) => (int)ScaleSigned(value, short.MaxValue, int.MaxValue);

    public static int ToSNorm32(uint value) => (int)ScaleUnsigned(value, uint.MaxValue, int.MaxValue);

    public static int ToSNorm32(int value) => value;

    public static int ToSNorm32(Half value) => FloatToSNorm32((float)value);

    public static int ToSNorm32(float value) => FloatToSNorm32(value);

    public static Half ToHalf(byte value) => (Half)UNorm8ToFloat(value);

    public static Half ToHalf(sbyte value) => (Half)SNorm8ToFloat(value);

    public static Half ToHalf(ushort value) => (Half)UNorm16ToFloat(value);

    public static Half ToHalf(short value) => (Half)SNorm16ToFloat(value);

    public static Half ToHalf(uint value) => (Half)UNorm32ToFloat(value);

    public static Half ToHalf(int value) => (Half)SNorm32ToFloat(value);

    public static Half ToHalf(Half value) => value;

    public static Half ToHalf(float value) => (Half)value;

    public static float ToFloat(byte value) => UNorm8ToFloat(value);

    public static float ToFloat(sbyte value) => SNorm8ToFloat(value);

    public static float ToFloat(ushort value) => UNorm16ToFloat(value);

    public static float ToFloat(short value) => SNorm16ToFloat(value);

    public static float ToFloat(uint value) => UNorm32ToFloat(value);

    public static float ToFloat(int value) => SNorm32ToFloat(value);

    public static float ToFloat(Half value) => (float)value;

    public static float ToFloat(float value) => value;

    public static byte FloatToUNorm8(float value)
    {
        return (byte)MathF.Round(Saturate(value) * byte.MaxValue);
    }

    public static ushort FloatToUNorm16(float value)
    {
        return (ushort)MathF.Round(Saturate(value) * ushort.MaxValue);
    }

    public static uint FloatToUNorm32(float value)
    {
        var rounded = Math.Round(SaturateDouble(value) * uint.MaxValue);
        return rounded >= uint.MaxValue ? uint.MaxValue : (uint)rounded;
    }

    public static sbyte FloatToSNorm8(float value)
    {
        return (sbyte)MathF.Round(ClampSigned(value) * sbyte.MaxValue);
    }

    public static short FloatToSNorm16(float value)
    {
        return (short)MathF.Round(ClampSigned(value) * short.MaxValue);
    }

    public static int FloatToSNorm32(float value)
    {
        var rounded = Math.Round(ClampSignedDouble(value) * int.MaxValue);

        if (rounded <= -int.MaxValue)
        {
            return -int.MaxValue;
        }

        if (rounded >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)rounded;
    }

    public static float UNorm8ToFloat(byte value)
    {
        return value / (float)byte.MaxValue;
    }

    public static float UNorm16ToFloat(ushort value)
    {
        return value / (float)ushort.MaxValue;
    }

    public static float UNorm32ToFloat(uint value)
    {
        return (float)(value / (double)uint.MaxValue);
    }

    public static float SNorm8ToFloat(sbyte value)
    {
        return MathF.Max(value / (float)sbyte.MaxValue, -1f);
    }

    public static float SNorm16ToFloat(short value)
    {
        return MathF.Max(value / (float)short.MaxValue, -1f);
    }

    public static float SNorm32ToFloat(int value)
    {
        return MathF.Max((float)(value / (double)int.MaxValue), -1f);
    }

    private static float Saturate(float value)
    {
        if (float.IsNaN(value))
        {
            return 0f;
        }

        return Math.Clamp(value, 0f, 1f);
    }

    private static double SaturateDouble(float value)
    {
        if (float.IsNaN(value))
        {
            return 0d;
        }

        return Math.Clamp((double)value, 0d, 1d);
    }

    private static float ClampSigned(float value)
    {
        if (float.IsNaN(value))
        {
            return 0f;
        }

        return Math.Clamp(value, -1f, 1f);
    }

    private static double ClampSignedDouble(float value)
    {
        if (float.IsNaN(value))
        {
            return 0d;
        }

        return Math.Clamp((double)value, -1d, 1d);
    }

    private static ulong ScaleUnsigned(ulong value, ulong sourceMax, ulong targetMax)
    {
        if (sourceMax == targetMax)
        {
            return value;
        }

        return (value * targetMax + sourceMax / 2) / sourceMax;
    }

    private static ulong ScaleSignedToUnsigned(long value, long sourceMax, ulong targetMax)
    {
        if (value <= 0)
        {
            return 0;
        }

        if (value >= sourceMax)
        {
            return targetMax;
        }

        return ScaleUnsigned((ulong)value, (ulong)sourceMax, targetMax);
    }

    private static long ScaleSigned(long value, long sourceMax, long targetMax)
    {
        if (value <= -sourceMax)
        {
            return -targetMax;
        }

        if (value >= sourceMax)
        {
            return targetMax;
        }

        var magnitude = value < 0 ? (ulong)-value : (ulong)value;
        var scaled = (long)ScaleUnsigned(magnitude, (ulong)sourceMax, (ulong)targetMax);
        return value < 0 ? -scaled : scaled;
    }
}
