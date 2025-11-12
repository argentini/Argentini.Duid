using System.Buffers.Binary;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// ReSharper disable RedundantBoolCompare
// ReSharper disable MemberCanBePrivate.Global

namespace Argentini.Duid;

/// <summary>
/// DUID is a compact, URL-friendly, 22-char identifier that always starts with a letter
/// and has more entropy than a v4 GUID (128 bits vs 122 bits).
/// </summary>
[System.Diagnostics.DebuggerDisplay("{ToString(),nq}")]
[TypeConverter(typeof(DuidTypeConverter))]
public readonly struct Duid : IEquatable<Duid>, IComparable<Duid>, IComparable, ISpanFormattable, ISpanParsable<Duid>, IUtf8SpanFormattable
{
    #region Constants

    /// <summary>
    /// Public constant for the canonical byte length of a DUID (16 bytes).
    /// </summary>
    public const int ByteLength   = 16;

    /// <summary>
    /// Public constant for the canonical text length of a DUID (22 chars).
    /// </summary>
    public const int StringLength = 22;

    #endregion

    #region Properties / Fields

    private readonly ulong _payloadHi; // first 8 bytes
    private readonly ulong _payloadLo; // last 8 bytes
    public bool IsEmpty => _payloadHi == 0 && _payloadLo == 0;

    /// <summary>
    /// Represents a DUID with all zero bytes (equivalent to default(Duid)).
    /// </summary>
    public static readonly Duid Empty = default;

    #endregion

    #region Creation

    /// <summary>
    /// Create a new DUID from its raw payload.
    /// </summary>
    /// <param name="hi"></param>
    /// <param name="lo"></param>
    private Duid(ulong hi, ulong lo)
    {
        _payloadHi = hi;
        _payloadLo = lo;
    }

    /// <summary>
    /// Create a new DUID.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Duid NewDuid()
    {
        Span<byte> tmp = stackalloc byte[ByteLength];
        RandomNumberGenerator.Fill(tmp);

        var hi = BinaryPrimitives.ReadUInt64BigEndian(tmp[..8]);
        var lo = BinaryPrimitives.ReadUInt64BigEndian(tmp[8..]);

        return new Duid(hi, lo);
    }

    /// <summary>
    /// Create a DUID from 16 big-endian bytes (throwing version).
    /// </summary>
    public static Duid FromBytesBigEndian(ReadOnlySpan<byte> src) => TryFromBytes(src, out var id) == false ? throw new ArgumentException("Invalid byte array length. Expected 16 bytes.", nameof(src)) : id;

    /// <summary>
    /// Create a DUID from its raw 128-bit payload.
    /// </summary>
    /// <param name="hi"></param>
    /// <param name="lo"></param>
    /// <returns></returns>
    public static Duid FromUInt128(ulong hi, ulong lo) => new Duid(hi, lo);
    
    #endregion

    #region Binary access

    /// <summary>
    /// Copy the 16-byte big-endian payload into destination.
    /// </summary>
    private void CopyTo(Span<byte> destination)
    {
        if (destination.Length < ByteLength)
            throw new ArgumentException("Destination too small.", nameof(destination));

        BinaryPrimitives.WriteUInt64BigEndian(destination[..8], _payloadHi);
        BinaryPrimitives.WriteUInt64BigEndian(destination[8..],  _payloadLo);
    }

    /// <summary>
    /// Return a new 16-byte array (big-endian).
    /// </summary>
    public byte[] ToByteArray()
    {
        var arr = new byte[ByteLength];
        CopyTo(arr);
        return arr;
    }

    /// <summary>
    /// Try creating from 16 bytes (big-endian).
    /// </summary>
    public static bool TryFromBytes(ReadOnlySpan<byte> src, out Duid id)
    {
        if (src.Length != ByteLength)
        {
            id = default;
            return false;
        }

        var hi = BinaryPrimitives.ReadUInt64BigEndian(src[..8]);
        var lo = BinaryPrimitives.ReadUInt64BigEndian(src[8..]);

        id = new Duid(hi, lo);
        return true;
    }

    /// <summary>
    /// Write the 16-byte big-endian payload into destination.
    /// </summary>
    /// <param name="destination"></param>
    /// <returns></returns>
    public bool TryWriteBytes(Span<byte> destination)
    {
        if (destination.Length < ByteLength)
            return false;

        CopyTo(destination);
        
        return true;
    }

    #endregion

    #region Text (Base62) encode/decode

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private static readonly sbyte[] RevMap = CreateRevMap(Alphabet);
    private static readonly byte[] AlphabetBytes = Encoding.ASCII.GetBytes(Alphabet);
    
    private const int Radix3 = 62 * 62 * 62; // 238,328

    // Triplet tables: index -> three chars / bytes for base62^3 chunk
    // ~1.36 MiB for chars + ~0.68 MiB for bytes; built once.
    private static readonly char[] TripletsChars = BuildTripletsChars(Alphabet);
    private static readonly byte[] TripletsUtf8  = BuildTripletsUtf8(AlphabetBytes);

    /// <summary>
    /// Build the triplet character table for base62^3 chunks.
    /// </summary>
    /// <param name="alphabet"></param>
    /// <returns></returns>
    private static char[] BuildTripletsChars(string alphabet)
    {
        var tbl = new char[Radix3 * 3]; // flattened [i*3 + 0..2]
        var k = 0;
        
        for (var a = 0; a < 62; a++)
        for (var b = 0; b < 62; b++)
        for (var c = 0; c < 62; c++)
        {
            tbl[k++] = alphabet[a];
            tbl[k++] = alphabet[b];
            tbl[k++] = alphabet[c];
        }

        return tbl;
    }

    /// <summary>
    /// Build the triplet UTF-8 byte table for base62^3 chunks.
    /// </summary>
    /// <param name="alphabetBytes"></param>
    /// <returns></returns>
    private static byte[] BuildTripletsUtf8(byte[] alphabetBytes)
    {
        var tbl = new byte[Radix3 * 3];
        var k = 0;
        
        for (var a = 0; a < 62; a++)
        for (var b = 0; b < 62; b++)
        for (var c = 0; c < 62; c++)
        {
            tbl[k++] = alphabetBytes[a];
            tbl[k++] = alphabetBytes[b];
            tbl[k++] = alphabetBytes[c];
        }
        
        return tbl;
    }
    
    /// <summary>
    /// Create a reverse mapping from ASCII char to Base62 index.
    /// </summary>
    /// <param name="alphabet"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private static sbyte[] CreateRevMap(string alphabet)
    {
        ArgumentNullException.ThrowIfNull(alphabet);
        
        if (alphabet.Length != 62)
            throw new ArgumentException("Alphabet must have exactly 62 characters.", nameof(alphabet));

        var map = new sbyte[128];

        for (var i = 0; i < map.Length; i++)
            map[i] = -1;

        var seen = new bool[128];

        for (var i = 0; i < alphabet.Length; i++)
        {
            var ch = alphabet[i];
            
            if (ch > 127)
                throw new ArgumentException("Alphabet must be ASCII (<= 127).", nameof(alphabet));
            
            if (seen[ch])
                throw new ArgumentException($"Duplicate character '{ch}' in alphabet.", nameof(alphabet));
            
            seen[ch] = true;
            map[ch] = (sbyte)i;
        }

        return map;
    }    
    
    private static readonly sbyte[] FirstDigitRankByIndex = BuildLetterRankByIndex(Alphabet); // map 0..61 -> 0..51 or -1

    /// <summary>
    /// Build a mapping from Alphabet index to letter rank (0..51) or -1 if not a letter.
    /// </summary>
    /// <param name="alphabet"></param>
    /// <returns></returns>
    private static sbyte[] BuildLetterRankByIndex(string alphabet)
    {
        var map = new sbyte[62];
        
        for (var i = 0; i < map.Length; i++)
            map[i] = -1;

        var rank = 0;

        for (var i = 0; i < alphabet.Length; i++)
        {
            var ch = alphabet[i];
            
            if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                map[i] = (sbyte)(rank++);
        }

        return map;
    }

    #endregion
    
    #region Parsing 
    
    /// <summary>
    /// Parse a DUID from a ReadOnlySpan&lt;char&gt;.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public static bool TryParse(ReadOnlySpan<char> s, out Duid id)
    {
        id = default;

        if (s.Length != StringLength)
            return false;

        // First char must be a letter in the current Alphabet
        var c0 = s[0];
        
        if ((uint)c0 > 127)
            return false;
        
        int firstIdx = RevMap[c0];
        
        if (firstIdx < 0)
            return false;

        int r = FirstDigitRankByIndex[firstIdx]; // 0..51 if letter, -1 otherwise
        
        if (r < 0) return false;

        // Rebuild Q from the remaining 21 digits: Q = Σ d[i] * 62^(21-i)
        Span<byte> acc = stackalloc byte[ByteLength]; // big-endian 128-bit

        acc.Clear();
        
        for (var i = 1; i < StringLength; i++)
        {
            var c = s[i];
            var d = (c < 128) ? RevMap[c] : -1;
            
            if (d < 0)
                return false;
            
            if (MulAddBase(acc, 62, d) == false)
                return false;
        }

        // P = Q * 52 + r
        if (MulAddBase(acc, 52, r) == false)
            return false;

        var hi = BinaryPrimitives.ReadUInt64BigEndian(acc[..8]);
        var lo = BinaryPrimitives.ReadUInt64BigEndian(acc[8..]);

        id = new Duid(hi, lo);
        
        return true;
    }

    /// <summary>
    /// Parse a DUID from its string representation.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public static bool TryParse(string? s, out Duid id)
    {
        if (s is not null)
            return TryParse(s.AsSpan(), out id);
        
        id = default;
        
        return false;
    }

    /// <summary>
    /// Parse a DUID from its string representation with format/provider (ignored).
    /// </summary>
    /// <param name="s"></param>
    /// <param name="provider"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public static bool TryParse([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? s, IFormatProvider? provider, out Duid result) => TryParse(s, out result);

    /// <summary>
    /// Parse a DUID from a ReadOnlySpan&lt;char&gt; with format/provider (ignored).
    /// </summary>
    /// <param name="s"></param>
    /// <param name="provider"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Duid result) => TryParse(s, out result);    

    /// <summary>
    /// Parse a DUID from UTF-8 bytes.
    /// </summary>
    /// <param name="utf8"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public static bool TryParseUtf8(ReadOnlySpan<byte> utf8, out Duid id)
    {
        if (utf8.Length != StringLength)
        {
            id = default;
            return false;
        }
        
        Span<char> tmp = stackalloc char[StringLength];
        
        for (var i = 0; i < utf8.Length; i++)
        {
            var b = utf8[i];

            if (b > 0x7F)
            {
                id = default;
                return false;
            }
            
            tmp[i] = (char)b;
        }

        return TryParse(tmp, out id);
    }

    /// <summary>
    /// Parse a DUID from UTF-8 bytes with format/provider (ignored).
    /// </summary>
    /// <param name="utf8"></param>
    /// <param name="provider"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8, IFormatProvider? provider, out Duid result) => TryParseUtf8(utf8, out result);

    /// <summary>
    /// Parse DUID from a ReadOnlySpan&lt;char&gt;.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    /// <exception cref="FormatException"></exception>
    public static Duid Parse(ReadOnlySpan<char> s)
    {
        return TryParse(s, out var id) == false ? throw new FormatException("Invalid DUID.") : id;
    }

    /// <summary>
    /// Parse a DUID from its string representation.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    public static Duid Parse(string s) => Parse(s.AsSpan());

    /// <summary>
    /// Parse a DUID from its string representation with format/provider (ignored).
    /// </summary>
    /// <param name="s"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public static Duid Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan());

    /// <summary>
    /// Parse a DUID from a ReadOnlySpan&lt;char&gt; with format/provider (ignored).
    /// </summary>
    /// <param name="s"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public static Duid Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    
    #endregion
    
    #region Conversion

    /// <summary>
    /// Convert to string representation.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="FormatException"></exception>
    public override string ToString()
    {
        return string.Create(StringLength, this, static (span, self) =>
        {
            _ = self.TryWriteChars(span, out _);
        });
    }
    
    /// <summary>
    /// Convert to string representation with format/provider (ignored).
    /// </summary>
    /// <param name="format"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public string ToString(string? format, IFormatProvider? provider) => ToString();

    string IFormattable.ToString(string? format, IFormatProvider? provider) => ToString(format, provider);

    /// <summary>
    /// Format the DUID into the given destination span with zero allocations with format/provider (ignored).
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="charsWritten"></param>
    /// <param name="format"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        if (destination.Length < StringLength)
        {
            charsWritten = 0;
            return false;
        }

        // Work with 128-bit in registers
        var hi = _payloadHi;
        var lo = _payloadLo;

        // First digit (letter): r = P % 52; P = P / 52
        var r = DivMod128ByConst(ref hi, ref lo, 52);
        
        destination[0] = Alphabet[(int)r];

        // Tail: 21 digits => 7 groups of 3 (base 62^3)
        // Fill from the end toward the front.
        var pos = StringLength;
        
        for (var g = 0; g < 7; g++)
        {
            var rem = DivMod128ByConst(ref hi, ref lo, Radix3);
            var idx = (int)rem * 3;

            destination[--pos] = TripletsChars[idx + 2];
            destination[--pos] = TripletsChars[idx + 1];
            destination[--pos] = TripletsChars[idx + 0];
        }

        charsWritten = StringLength;
        
        return true;
    }
    
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => TryFormat(destination, out charsWritten, format, provider);

    /// <summary>
    /// Format the DUID into the given destination span with zero allocations with format/provider (ignored).
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="charsWritten"></param>
    /// <returns></returns>
    public bool TryWriteChars(Span<char> destination, out int charsWritten) => TryFormat(destination, out charsWritten);

    /// <summary>
    /// Format the DUID into UTF-8 bytes in the given destination span with zero allocations.
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="charsWritten"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryFormatUtf8(Span<byte> destination, out int charsWritten)
    {
        if (destination.Length < StringLength)
        {
            charsWritten = 0;
            return false;
        }

        var hi = _payloadHi;
        var lo = _payloadLo;

        var r = DivMod128ByConst(ref hi, ref lo, 52);
        
        destination[0] = AlphabetBytes[(int)r];

        var pos = StringLength;
        
        for (var g = 0; g < 7; g++)
        {
            var rem = DivMod128ByConst(ref hi, ref lo, Radix3);
            var idx = (int)rem * 3;

            destination[--pos] = TripletsUtf8[idx + 2];
            destination[--pos] = TripletsUtf8[idx + 1];
            destination[--pos] = TripletsUtf8[idx + 0];
        }

        charsWritten = StringLength;

        return true;
    }
    
    /// <summary>
    /// Format the DUID into UTF-8 bytes in the given destination span with zero allocations.
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="bytesWritten"></param>
    /// <returns></returns>
    public bool TryWriteUtf8(Span<byte> destination, out int bytesWritten) => TryFormatUtf8(destination, out bytesWritten);
    
    /// <summary>
    /// Format the DUID into UTF-8 bytes in the given destination span with zero allocations.
    /// </summary>
    /// <param name="utf8Destination"></param>
    /// <param name="bytesWritten"></param>
    /// <param name="format"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => TryFormatUtf8(utf8Destination, out bytesWritten);
    
    #endregion

    #region Validation

    /// <summary>
    /// Validate if the given string is a valid DUID.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    public static bool IsValidString(ReadOnlySpan<char> s)
    {
        if (s.Length != StringLength)
            return false;

        // First char must be a letter in Alphabet
        var c0 = s[0];
        
        if ((uint)c0 > 127)
            return false;
        
        int firstIdx = RevMap[c0];
        
        if (firstIdx < 0)
            return false;
        
        if (FirstDigitRankByIndex[firstIdx] < 0)
            return false; // not a letter

        // Decode the tail and ensure we fit 128 bits
        Span<byte> acc = stackalloc byte[ByteLength];

        acc.Clear();

        for (var i = 1; i < StringLength; i++)
        {
            var c = s[i];
            var d = (c < 128) ? RevMap[c] : -1;
            
            if (d < 0)
                return false;
            
            if (MulAddBase(acc, 62, d) == false)
                return false;
        }

        // Multiply by 52 and add rank safely; on overflow, MulAddBase returns false.
        int r = FirstDigitRankByIndex[firstIdx];

        if (BeCompare(acc, MaxQ) > 0)
            return false; // Q too large, would overflow
        
        return MulAddBase(acc, 52, r) != false;
    }

    /// <summary>
    /// Validate if the given string is a valid DUID.
    /// </summary>
    public static bool IsValidString(string? s)
    {
        return s is not null && IsValidString(s.AsSpan());
    }
    
    #endregion
    
    #region Comparisons, operators
    
    /// <summary>
    /// Compare two DUIDs.
    /// </summary>
    public int CompareTo(Duid other)
    {
        var c = _payloadHi.CompareTo(other._payloadHi);
        
        return c != 0 ? c : _payloadLo.CompareTo(other._payloadLo);
    }

    int IComparable.CompareTo(object? obj)
        => obj is Duid other ? CompareTo(other) :
            obj is null ? 1 :
            throw new ArgumentException($"Object must be of type {nameof(Duid)}");

    /// <summary>
    /// Determine DUID equality.
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(Duid other) => _payloadHi == other._payloadHi && _payloadLo == other._payloadLo;

    /// <summary>
    /// Determine DUID equality.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object? obj) => obj is Duid d && Equals(d);

    /// <summary>
    /// Get a hash code for the DUID.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() => HashCode.Combine(_payloadHi, _payloadLo);

    public static bool operator ==(Duid a, Duid b) => a.Equals(b);
    public static bool operator !=(Duid a, Duid b) => !a.Equals(b);
    public static bool operator < (Duid a, Duid b) => a.CompareTo(b) < 0;
    public static bool operator > (Duid a, Duid b) => a.CompareTo(b) > 0;
    public static bool operator <=(Duid a, Duid b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Duid a, Duid b) => a.CompareTo(b) >= 0;

    #endregion
    
    #region Helpers

    /// <summary>
    /// Fast 128-bit division by constant 32-bit divisor.
    /// </summary>
    /// <param name="hi"></param>
    /// <param name="lo"></param>
    /// <param name="d"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static uint DivMod128ByConst(ref ulong hi, ref ulong lo, uint d)
    {
        // Divide a 128-bit integer (hi:lo) by 32-bit d.
        // Processes four 32-bit limbs: hiHi, hiLo, loHi, loLo.
        ulong rem = 0;

        var limb = hi >> 32;
        var cur  = (rem << 32) | limb;
        var q0   = cur / d;
        
        rem        = cur - q0 * d;
        limb = (uint)hi;
        cur  = (rem << 32) | limb;

        var q1 = cur / d;
        
        rem      = cur - q1 * d;
        limb = lo >> 32;
        cur  = (rem << 32) | limb;

        var q2 = cur / d;
        
        rem      = cur - q2 * d;

        limb = (uint)lo;
        cur  = (rem << 32) | limb;
        
        var q3 = cur / d;
        
        rem      = cur - q3 * d;
        hi = (q0 << 32) | q1;
        lo = (q2 << 32) | q3;

        return (uint)rem;
    }
    
    /// <summary>
    /// Helper to compute: be = be * mul + add
    /// </summary>
    /// <param name="be"></param>
    /// <param name="mul"></param>
    /// <param name="add"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MulAddBase(Span<byte> be, int mul, int add)
    {
        var m = (uint)mul;
        var carry = (uint)add;

        for (var i = be.Length - 1; i >= 0; i--)
        {
            // ReSharper disable once RedundantCast
            var v = (uint)be[i] * m + carry;
        
            be[i] = (byte)v;
            carry = v >> 8;
        }
        
        return carry == 0;
    }
    
    /// <summary>
    /// Helper to compute: rem = be % divisor; be = be / divisor
    /// </summary>
    /// <param name="be"></param>
    /// <param name="divisor"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint DivModBigEndian(Span<byte> be, int divisor)
    {
        var d = (uint)divisor;
        var rem = (uint)0;
        
        for (var i = 0; i < be.Length; i++)
        {
            var cur = (rem << 8) | be[i];
            var q = (byte)(cur / d);
            
            be[i] = q;
            rem = cur - q * d;
        }
        
        return rem;
    }

    /// <summary>
    /// Helper to compute the maximum Q = floor((2^128 - 1) / 52)
    /// </summary>
    private static readonly byte[] MaxQ = ComputeMaxQ(); // 16-byte BE
    private static byte[] ComputeMaxQ()
    {
        Span<byte> x = stackalloc byte[16];

        x.Fill(0xFF); // 2^128 - 1
        _ = DivModBigEndian(x, 52); // x = floor((2^128 - 1)/52)
        
        var arr = new byte[16];
        
        x.CopyTo(arr);
        
        return arr;
    }

    /// <summary>
    /// Helper to compare two big-endian byte arrays.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BeCompare(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        for (var i = 0; i < a.Length; i++)
            if (a[i] != b[i])
                return a[i] < b[i] ? -1 : 1;

        return 0;
    }
    
    #endregion
}

/// <summary>
/// Type converter for Duid support.
/// </summary>
public sealed class DuidTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? ctx, Type src) => src == typeof(string) || base.CanConvertFrom(ctx, src);
    public override bool CanConvertTo(ITypeDescriptorContext? ctx, Type? destination) => destination == typeof(string) || base.CanConvertTo(ctx, destination);
    public override object? ConvertFrom(ITypeDescriptorContext? ctx, CultureInfo? culture, object value) => value is string s ? Duid.Parse(s) : base.ConvertFrom(ctx, culture, value);
    public override object? ConvertTo(ITypeDescriptorContext? ctx, CultureInfo? culture, object? value, Type destType)
    {
        if (destType == typeof(string) && value is Duid d)
            return d.ToString();

        return base.ConvertTo(ctx, culture, value, destType);
    }
}

/// <summary>
/// JSON converter for Duid support.
/// <example>
/// Register the JSON converter like this:
/// <code>
/// options.Converters.Add(new DuidJsonConverter());
/// </code>
/// </example>
/// </summary>
public sealed class DuidJsonConverter : System.Text.Json.Serialization.JsonConverter<Duid>
{
    public override Duid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.String
            ? Duid.Parse(reader.GetString()!)
            : throw new JsonException("Expected string for Duid.");

    public override void Write(Utf8JsonWriter writer, Duid value, JsonSerializerOptions options)
    {
        Span<byte> buf = stackalloc byte[Duid.StringLength];
 
        if (value.TryFormatUtf8(buf, out var written) == false || written != Duid.StringLength)
            throw new JsonException("Failed to format Duid.");

        writer.WriteStringValue(buf);
    }
}