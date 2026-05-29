namespace TextureCompressor.Colors;

public static class RgbaColorConversions
{
    public static byte ToUNorm8(byte value) => value;

    public static byte ToUNorm8(sbyte value) => (byte)ScaleSignedToUnsigned(value, sbyte.MaxValue, byte.MaxValue);

    public static byte ToUNorm8(ushort value) => (byte)ScaleUnsigned(value, ushort.MaxValue, byte.MaxValue);

    public static byte ToUNorm8(short value) => (byte)ScaleSignedToUnsigned(value, short.MaxValue, byte.MaxValue);

    public static byte ToUNorm8(uint value) => (byte)ScaleUnsigned(value, uint.MaxValue, byte.MaxValue);

    public static byte ToUNorm8(int value) => (byte)ScaleSignedToUnsigned(value, int.MaxValue, byte.MaxValue);

    public static byte ToUNorm8(ulong value) => (byte)ScaleUnsignedWide(value, ulong.MaxValue, byte.MaxValue);

    public static byte ToUNorm8(long value) => (byte)ScaleSignedToUnsignedWide(value, long.MaxValue, byte.MaxValue);

    public static byte ToUNorm8(Half value) => FloatToUNorm8((float)value);

    public static byte ToUNorm8(float value) => FloatToUNorm8(value);

    public static byte ToUNorm8(double value) => FloatToUNorm8((float)value);

    public static sbyte ToSNorm8(byte value) => (sbyte)ScaleUnsigned(value, byte.MaxValue, (ulong)sbyte.MaxValue);

    public static sbyte ToSNorm8(sbyte value) => value;

    public static sbyte ToSNorm8(ushort value) => (sbyte)ScaleUnsigned(value, ushort.MaxValue, (ulong)sbyte.MaxValue);

    public static sbyte ToSNorm8(short value) => (sbyte)ScaleSigned(value, short.MaxValue, sbyte.MaxValue);

    public static sbyte ToSNorm8(uint value) => (sbyte)ScaleUnsigned(value, uint.MaxValue, (ulong)sbyte.MaxValue);

    public static sbyte ToSNorm8(int value) => (sbyte)ScaleSigned(value, int.MaxValue, sbyte.MaxValue);

    public static sbyte ToSNorm8(ulong value) => (sbyte)ScaleUnsignedWide(value, ulong.MaxValue, (ulong)sbyte.MaxValue);

    public static sbyte ToSNorm8(long value) => (sbyte)ScaleSignedWide(value, long.MaxValue, sbyte.MaxValue);

    public static sbyte ToSNorm8(Half value) => FloatToSNorm8((float)value);

    public static sbyte ToSNorm8(float value) => FloatToSNorm8(value);

    public static sbyte ToSNorm8(double value) => FloatToSNorm8((float)value);

    public static ushort ToUNorm16(byte value) => (ushort)ScaleUnsigned(value, byte.MaxValue, ushort.MaxValue);

    public static ushort ToUNorm16(sbyte value) => (ushort)ScaleSignedToUnsigned(value, sbyte.MaxValue, ushort.MaxValue);

    public static ushort ToUNorm16(ushort value) => value;

    public static ushort ToUNorm16(short value) => (ushort)ScaleSignedToUnsigned(value, short.MaxValue, ushort.MaxValue);

    public static ushort ToUNorm16(uint value) => (ushort)ScaleUnsigned(value, uint.MaxValue, ushort.MaxValue);

    public static ushort ToUNorm16(int value) => (ushort)ScaleSignedToUnsigned(value, int.MaxValue, ushort.MaxValue);

    public static ushort ToUNorm16(ulong value) => (ushort)ScaleUnsignedWide(value, ulong.MaxValue, ushort.MaxValue);

    public static ushort ToUNorm16(long value) => (ushort)ScaleSignedToUnsignedWide(value, long.MaxValue, ushort.MaxValue);

    public static ushort ToUNorm16(Half value) => FloatToUNorm16((float)value);

    public static ushort ToUNorm16(float value) => FloatToUNorm16(value);

    public static ushort ToUNorm16(double value) => FloatToUNorm16((float)value);

    public static short ToSNorm16(byte value) => (short)ScaleUnsigned(value, byte.MaxValue, (ulong)short.MaxValue);

    public static short ToSNorm16(sbyte value) => (short)ScaleSigned(value, sbyte.MaxValue, short.MaxValue);

    public static short ToSNorm16(ushort value) => (short)ScaleUnsigned(value, ushort.MaxValue, (ulong)short.MaxValue);

    public static short ToSNorm16(short value) => value;

    public static short ToSNorm16(uint value) => (short)ScaleUnsigned(value, uint.MaxValue, (ulong)short.MaxValue);

    public static short ToSNorm16(int value) => (short)ScaleSigned(value, int.MaxValue, short.MaxValue);

    public static short ToSNorm16(ulong value) => (short)ScaleUnsignedWide(value, ulong.MaxValue, (ulong)short.MaxValue);

    public static short ToSNorm16(long value) => (short)ScaleSignedWide(value, long.MaxValue, short.MaxValue);

    public static short ToSNorm16(Half value) => FloatToSNorm16((float)value);

    public static short ToSNorm16(float value) => FloatToSNorm16(value);

    public static short ToSNorm16(double value) => FloatToSNorm16((float)value);

    public static uint ToUNorm32(byte value) => (uint)ScaleUnsigned(value, byte.MaxValue, uint.MaxValue);

    public static uint ToUNorm32(sbyte value) => (uint)ScaleSignedToUnsigned(value, sbyte.MaxValue, uint.MaxValue);

    public static uint ToUNorm32(ushort value) => (uint)ScaleUnsigned(value, ushort.MaxValue, uint.MaxValue);

    public static uint ToUNorm32(short value) => (uint)ScaleSignedToUnsigned(value, short.MaxValue, uint.MaxValue);

    public static uint ToUNorm32(uint value) => value;

    public static uint ToUNorm32(int value) => (uint)ScaleSignedToUnsigned(value, int.MaxValue, uint.MaxValue);

    public static uint ToUNorm32(ulong value) => (uint)ScaleUnsignedWide(value, ulong.MaxValue, uint.MaxValue);

    public static uint ToUNorm32(long value) => (uint)ScaleSignedToUnsignedWide(value, long.MaxValue, uint.MaxValue);

    public static uint ToUNorm32(Half value) => FloatToUNorm32((float)value);

    public static uint ToUNorm32(float value) => FloatToUNorm32(value);

    public static uint ToUNorm32(double value) => FloatToUNorm32((float)value);

    public static int ToSNorm32(byte value) => (int)ScaleUnsigned(value, byte.MaxValue, int.MaxValue);

    public static int ToSNorm32(sbyte value) => (int)ScaleSigned(value, sbyte.MaxValue, int.MaxValue);

    public static int ToSNorm32(ushort value) => (int)ScaleUnsigned(value, ushort.MaxValue, int.MaxValue);

    public static int ToSNorm32(short value) => (int)ScaleSigned(value, short.MaxValue, int.MaxValue);

    public static int ToSNorm32(uint value) => (int)ScaleUnsigned(value, uint.MaxValue, int.MaxValue);

    public static int ToSNorm32(int value) => value;

    public static int ToSNorm32(ulong value) => (int)ScaleUnsignedWide(value, ulong.MaxValue, int.MaxValue);

    public static int ToSNorm32(long value) => (int)ScaleSignedWide(value, long.MaxValue, int.MaxValue);

    public static int ToSNorm32(Half value) => FloatToSNorm32((float)value);

    public static int ToSNorm32(float value) => FloatToSNorm32(value);

    public static int ToSNorm32(double value) => FloatToSNorm32((float)value);

    public static ulong ToUInt64(byte value) => ScaleUnsignedWide(value, byte.MaxValue, ulong.MaxValue);

    public static ulong ToUInt64(sbyte value) => ScaleSignedToUnsignedWide(value, sbyte.MaxValue, ulong.MaxValue);

    public static ulong ToUInt64(ushort value) => ScaleUnsignedWide(value, ushort.MaxValue, ulong.MaxValue);

    public static ulong ToUInt64(short value) => ScaleSignedToUnsignedWide(value, short.MaxValue, ulong.MaxValue);

    public static ulong ToUInt64(uint value) => ScaleUnsignedWide(value, uint.MaxValue, ulong.MaxValue);

    public static ulong ToUInt64(int value) => ScaleSignedToUnsignedWide(value, int.MaxValue, ulong.MaxValue);

    public static ulong ToUInt64(ulong value) => value;

    public static ulong ToUInt64(long value) => ScaleSignedToUnsignedWide(value, long.MaxValue, ulong.MaxValue);

    public static ulong ToUInt64(Half value) => FloatToUInt64((double)value);

    public static ulong ToUInt64(float value) => FloatToUInt64(value);

    public static ulong ToUInt64(double value) => FloatToUInt64(value);

    public static long ToSInt64(byte value) => (long)ScaleUnsignedWide(value, byte.MaxValue, (ulong)long.MaxValue);

    public static long ToSInt64(sbyte value) => ScaleSignedWide(value, sbyte.MaxValue, long.MaxValue);

    public static long ToSInt64(ushort value) => (long)ScaleUnsignedWide(value, ushort.MaxValue, (ulong)long.MaxValue);

    public static long ToSInt64(short value) => ScaleSignedWide(value, short.MaxValue, long.MaxValue);

    public static long ToSInt64(uint value) => (long)ScaleUnsignedWide(value, uint.MaxValue, (ulong)long.MaxValue);

    public static long ToSInt64(int value) => ScaleSignedWide(value, int.MaxValue, long.MaxValue);

    public static long ToSInt64(ulong value) => (long)ScaleUnsignedWide(value, ulong.MaxValue, (ulong)long.MaxValue);

    public static long ToSInt64(long value) => value;

    public static long ToSInt64(Half value) => FloatToSInt64((double)value);

    public static long ToSInt64(float value) => FloatToSInt64(value);

    public static long ToSInt64(double value) => FloatToSInt64(value);

    public static Half ToHalf(byte value) => (Half)UNorm8ToFloat(value);

    public static Half ToHalf(sbyte value) => (Half)SNorm8ToFloat(value);

    public static Half ToHalf(ushort value) => (Half)UNorm16ToFloat(value);

    public static Half ToHalf(short value) => (Half)SNorm16ToFloat(value);

    public static Half ToHalf(uint value) => (Half)UNorm32ToFloat(value);

    public static Half ToHalf(int value) => (Half)SNorm32ToFloat(value);

    public static Half ToHalf(ulong value) => (Half)UNorm64ToDouble(value);

    public static Half ToHalf(long value) => (Half)SNorm64ToDouble(value);

    public static Half ToHalf(Half value) => value;

    public static Half ToHalf(float value) => (Half)value;

    public static Half ToHalf(double value) => (Half)value;

    public static float ToFloat(byte value) => UNorm8ToFloat(value);

    public static float ToFloat(sbyte value) => SNorm8ToFloat(value);

    public static float ToFloat(ushort value) => UNorm16ToFloat(value);

    public static float ToFloat(short value) => SNorm16ToFloat(value);

    public static float ToFloat(uint value) => UNorm32ToFloat(value);

    public static float ToFloat(int value) => SNorm32ToFloat(value);

    public static float ToFloat(ulong value) => (float)UNorm64ToDouble(value);

    public static float ToFloat(long value) => (float)SNorm64ToDouble(value);

    public static float ToFloat(Half value) => (float)value;

    public static float ToFloat(float value) => value;

    public static float ToFloat(double value) => (float)value;

    public static double ToDouble(byte value) => UNorm8ToFloat(value);

    public static double ToDouble(sbyte value) => SNorm8ToFloat(value);

    public static double ToDouble(ushort value) => UNorm16ToFloat(value);

    public static double ToDouble(short value) => SNorm16ToFloat(value);

    public static double ToDouble(uint value) => UNorm32ToFloat(value);

    public static double ToDouble(int value) => SNorm32ToFloat(value);

    public static double ToDouble(ulong value) => UNorm64ToDouble(value);

    public static double ToDouble(long value) => SNorm64ToDouble(value);

    public static double ToDouble(Half value) => (double)value;

    public static double ToDouble(float value) => value;

    public static double ToDouble(double value) => value;

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

    public static ulong FloatToUInt64(double value)
    {
        var rounded = Math.Round(SaturateDouble(value) * ulong.MaxValue);
        return rounded >= ulong.MaxValue ? ulong.MaxValue : (ulong)rounded;
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

    public static long FloatToSInt64(double value)
    {
        var rounded = Math.Round(ClampSignedDouble(value) * long.MaxValue);

        if (rounded <= -long.MaxValue)
        {
            return -long.MaxValue;
        }

        if (rounded >= long.MaxValue)
        {
            return long.MaxValue;
        }

        return (long)rounded;
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

    public static double UNorm64ToDouble(ulong value)
    {
        return value / (double)ulong.MaxValue;
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

    public static double SNorm64ToDouble(long value)
    {
        return Math.Max(value / (double)long.MaxValue, -1d);
    }

    public static float Srgb8ToLinearFloat(byte value)
    {
        var srgb = UNorm8ToFloat(value);
        return srgb <= 0.04045f
            ? srgb / 12.92f
            : MathF.Pow((srgb + 0.055f) / 1.055f, 2.4f);
    }

    public static byte LinearFloatToSrgb8(float value)
    {
        value = Saturate(value);
        var srgb = value <= 0.0031308f
            ? value * 12.92f
            : (1.055f * MathF.Pow(value, 1f / 2.4f)) - 0.055f;
        return FloatToUNorm8(srgb);
    }

    public static byte Srgb8ToLinearUNorm8(byte value) =>
        Srgb8ToLinearUNorm8Lookup.Table[value];

    public static byte LinearUNorm8ToSrgb8(byte value) =>
        LinearUNorm8ToSrgb8Lookup.Table[value];

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

    private static double SaturateDouble(double value)
    {
        if (double.IsNaN(value))
        {
            return 0d;
        }

        return Math.Clamp(value, 0d, 1d);
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

    private static double ClampSignedDouble(double value)
    {
        if (double.IsNaN(value))
        {
            return 0d;
        }

        return Math.Clamp(value, -1d, 1d);
    }

    private static double SrgbToLinear(double value)
    {
        value = SaturateDouble(value);
        return value <= 0.04045d
            ? value / 12.92d
            : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
    }

    private static uint CeilingToUNorm32(double value)
    {
        var ceiling = Math.Ceiling(SaturateDouble(value) * uint.MaxValue);
        return ceiling >= uint.MaxValue ? uint.MaxValue : (uint)ceiling;
    }

    private static ulong CeilingToUNorm64(double value)
    {
        var ceiling = Math.Ceiling(SaturateDouble(value) * ulong.MaxValue);
        return ceiling >= ulong.MaxValue ? ulong.MaxValue : (ulong)ceiling;
    }

    private static byte EncodeLinearUNormToSrgb8(uint value, ReadOnlySpan<uint> thresholds)
    {
        var low = 0;
        var high = thresholds.Length;
        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (value < thresholds[mid])
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        return (byte)low;
    }

    private static byte EncodeLinearUNormToSrgb8(ulong value, ReadOnlySpan<ulong> thresholds)
    {
        var low = 0;
        var high = thresholds.Length;
        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (value < thresholds[mid])
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        return (byte)low;
    }

    private static byte[] CreateSrgb8ToLinearUNorm8Table()
    {
        var table = new byte[byte.MaxValue + 1];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = FloatToUNorm8(Srgb8ToLinearFloat((byte)i));
        }

        return table;
    }

    private static ushort[] CreateSrgb8ToLinearUNorm16Table()
    {
        var table = new ushort[byte.MaxValue + 1];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = FloatToUNorm16(Srgb8ToLinearFloat((byte)i));
        }

        return table;
    }

    private static uint[] CreateSrgb8ToLinearUNorm32Table()
    {
        var table = new uint[byte.MaxValue + 1];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = FloatToUNorm32(Srgb8ToLinearFloat((byte)i));
        }

        return table;
    }

    private static ulong[] CreateSrgb8ToLinearUNorm64Table()
    {
        var table = new ulong[byte.MaxValue + 1];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = FloatToUInt64(Srgb8ToLinearFloat((byte)i));
        }

        return table;
    }

    private static byte[] CreateLinearUNorm8ToSrgb8Table()
    {
        var table = new byte[byte.MaxValue + 1];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = LinearFloatToSrgb8(UNorm8ToFloat((byte)i));
        }

        return table;
    }

    private static byte[] CreateLinearUNorm16ToSrgb8Table()
    {
        var table = new byte[ushort.MaxValue + 1];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = LinearFloatToSrgb8(UNorm16ToFloat((ushort)i));
        }

        return table;
    }

    private static uint[] CreateLinearUNorm32ToSrgb8Thresholds()
    {
        var thresholds = new uint[byte.MaxValue];
        for (var i = 1; i <= byte.MaxValue; i++)
        {
            thresholds[i - 1] = CeilingToUNorm32(SrgbToLinear((i - 0.5d) / byte.MaxValue));
        }

        return thresholds;
    }

    private static ulong[] CreateLinearUNorm64ToSrgb8Thresholds()
    {
        var thresholds = new ulong[byte.MaxValue];
        for (var i = 1; i <= byte.MaxValue; i++)
        {
            thresholds[i - 1] = CeilingToUNorm64(SrgbToLinear((i - 0.5d) / byte.MaxValue));
        }

        return thresholds;
    }

    private static class Srgb8ToLinearUNorm8Lookup
    {
        public static readonly byte[] Table = CreateSrgb8ToLinearUNorm8Table();
    }

    private static class LinearUNorm8ToSrgb8Lookup
    {
        public static readonly byte[] Table = CreateLinearUNorm8ToSrgb8Table();
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

    private static ulong ScaleUnsignedWide(ulong value, ulong sourceMax, ulong targetMax)
    {
        if (sourceMax == targetMax)
        {
            return value;
        }

        if (value == 0)
        {
            return 0;
        }

        if (value >= sourceMax)
        {
            return targetMax;
        }

        return (ulong)((((UInt128)value * targetMax) + (sourceMax / 2)) / sourceMax);
    }

    private static ulong ScaleSignedToUnsignedWide(long value, long sourceMax, ulong targetMax)
    {
        if (value <= 0)
        {
            return 0;
        }

        if (value >= sourceMax)
        {
            return targetMax;
        }

        return ScaleUnsignedWide((ulong)value, (ulong)sourceMax, targetMax);
    }

    private static long ScaleSignedWide(long value, long sourceMax, long targetMax)
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
        var scaled = (long)ScaleUnsignedWide(magnitude, (ulong)sourceMax, (ulong)targetMax);
        return value < 0 ? -scaled : scaled;
    }
}
