using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace Argentini.Duid.Tests;

public class DuidTests(ITestOutputHelper testOutputHelper)
{
	#region Basic
	
	[Fact]
	public void Duid_Produces_22Char_UrlSafe_String()
	{
		var validChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();

		for (var i = 0; i < 100; i++)
		{
			var k = Duid.NewDuid().ToString();

			testOutputHelper.WriteLine(k);
			
			Assert.Equal(22, k.Length);
			Assert.True(k.All(c => validChars.Contains(c)));
		}
	}
	
	[Fact]
	public void Duid_ReturnsNull_OnNullEmptyOrWrongLength()
	{
		string? s1 = null;
		const string s3 = "tooShort";
		
		Assert.False(Duid.IsValidString(s1));
		Assert.False(Duid.IsValidString(s3));
	}

	[Fact]
	public void Duid_IsValidFormat()
	{
		const string invalid = "1BnLDYfeYdiYHN1bU6WgOe";
		const string valid = "REa0SkiInk5x2y9hy7PuYs";

		Assert.False(Duid.IsValidString(invalid));
		Assert.True(Duid.IsValidString(valid));
	}

	[Fact]
	public void Duid_Empty()
	{
		Assert.Equal("AAAAAAAAAAAAAAAAAAAAAA", Duid.Empty.ToString());
		Assert.True(Duid.IsValidString("AAAAAAAAAAAAAAAAAAAAAA"));
	}

	#endregion
	
	#region Round-trips & Formatting

    [Fact]
    public void Duid_RoundTrip_String_Parse_TryParse()
    {
        for (var i = 0; i < 100; i++)
        {
            var id = Duid.NewDuid();
            var s  = id.ToString();

            Assert.True(Duid.IsValidString(s));
            Assert.True(Duid.TryParse(s, out var parsed));
            Assert.Equal(id, parsed);

            // Parse(ReadOnlySpan<char>)
            var parsed2 = Duid.Parse(s.AsSpan());
            Assert.Equal(id, parsed2);

            // TryParse with provider overloads (ignored but should route correctly)
            Assert.True(Duid.TryParse(s, provider: null, out var parsed3));
            Assert.True(Duid.TryParse(s.AsSpan(), provider: null, out var parsed4));
            Assert.Equal(id, parsed3);
            Assert.Equal(id, parsed4);
        }
    }

    [Fact]
    public void Duid_FirstCharacter_IsAlwaysALetter()
    {
        for (var i = 0; i < 100; i++)
        {
            var s = Duid.NewDuid().ToString();
            Assert.True(char.IsLetter(s[0]), $"First char should be a letter, got '{s[0]}' in {s}");
        }
    }

    [Fact]
    public void Duid_TryFormat_To_CharSpan_And_Utf8_Bytes()
    {
        var id = Duid.NewDuid();

        Span<char> chars = stackalloc char[Duid.StringLength];
        Assert.True(id.TryWriteChars(chars, out var writtenChars));
        Assert.Equal(Duid.StringLength, writtenChars);

        var s = new string(chars);
        Assert.Equal(id.ToString(), s);

        Span<byte> bytes = stackalloc byte[Duid.StringLength];
        Assert.True(id.TryWriteUtf8(bytes, out var writtenBytes));
        Assert.Equal(Duid.StringLength, writtenBytes);

        var sFromUtf8 = Encoding.ASCII.GetString(bytes);
        Assert.Equal(id.ToString(), sFromUtf8);
    }

    [Fact]
    public void Duid_TryFormat_DestinationTooSmall_ReturnsFalse()
    {
        var id = Duid.NewDuid();

        Span<char> smallChars = stackalloc char[Duid.StringLength - 1];
        Assert.False(id.TryWriteChars(smallChars, out var cw));
        Assert.Equal(0, cw);

        Span<byte> smallBytes = stackalloc byte[Duid.StringLength - 1];
        Assert.False(id.TryWriteUtf8(smallBytes, out var bw));
        Assert.Equal(0, bw);
    }

    #endregion
    
    #region UTF-8 Parsing

    [Fact]
    public void Duid_TryParseUtf8_Valid_And_Invalid()
    {
        var id = Duid.NewDuid();
        var s  = id.ToString();

        var utf8 = Encoding.ASCII.GetBytes(s);
        Assert.True(Duid.TryParseUtf8(utf8, out var parsed));
        Assert.Equal(id, parsed);

        // Non-ASCII byte should fail
        var bad = (byte[])utf8.Clone();
        bad[0] = 0xC3; // non-ASCII lead byte
        Assert.False(Duid.TryParseUtf8(bad, out _));

        // Wrong length should fail
        var wrongLen = Encoding.ASCII.GetBytes(s + "X");
        Assert.False(Duid.TryParseUtf8(wrongLen, out _));
    }

    #endregion
    
    #region Binary API

    [Fact]
    public void Duid_ToBytes_And_FromBytes_BigEndian()
    {
        var id = Duid.NewDuid();
        var bytes = id.ToByteArray();

        Assert.Equal(16, bytes.Length);

        Assert.True(Duid.TryFromBytes(bytes, out var parsed));
        Assert.Equal(id, parsed);

        // Copy into destination
        Span<byte> dst = stackalloc byte[16];
        Assert.True(id.TryWriteBytes(dst));
        Assert.True(dst.SequenceEqual(bytes));
    }

    [Fact]
    public void Duid_TryFromBytes_WrongLength_Fails_And_TryWriteBytes_DestinationTooSmall()
    {
        var id = Duid.NewDuid();

        Assert.False(Duid.TryFromBytes(new byte[15], out _));

        Span<byte> small = stackalloc byte[15];
        Assert.False(id.TryWriteBytes(small));
    }

    [Fact]
    public void Duid_FromUInt128_And_Empty_AreConsistent()
    {
        var zero = Duid.FromUInt128(0, 0);
        Assert.Equal(Duid.Empty, zero);
        Assert.Equal("AAAAAAAAAAAAAAAAAAAAAA", zero.ToString());

        // Also ensure IsEmpty works
        Assert.True(zero.IsEmpty);
        Assert.False(Duid.NewDuid().IsEmpty);
    }

    #endregion

    #region Ordering & Equality

    [Fact]
    public void Duid_CompareTo_And_Operators()
    {
        var a = Duid.FromUInt128(0x0000000000000001UL, 0x0000000000000000UL);
        var b = Duid.FromUInt128(0x0000000000000001UL, 0x0000000000000001UL);
        var c = Duid.FromUInt128(0x0000000000000002UL, 0x0000000000000000UL);

        Assert.True(a < b);
        Assert.True(b < c);
        Assert.True(a <= b);
        Assert.True(c > b);
        Assert.True(c >= b);

        Assert.True(a != b);
        Assert.Equal(a, a);
        Assert.Equal(a.GetHashCode(), a.GetHashCode());
    }

    #endregion
    
    #region Validation edges

    [Fact]
    public void Duid_IsValidString_Rejects_NonAscii_And_DigitFirst()
    {
        // Starts with a digit – invalid
        const string bad1 = "1Ea0SkiInk5x2y9hy7PuYs";
        Assert.False(Duid.IsValidString(bad1));

        // Non-ASCII in the middle – invalid
        var good = Duid.NewDuid().ToString();
        var chars = good.ToCharArray();
        chars[10] = 'Ñ'; // non-ASCII
        var bad2 = new string(chars);
        Assert.False(Duid.IsValidString(bad2));

        // Wrong length
        Assert.False(Duid.IsValidString(good + "X"));
    }

    #endregion
    
    #region JSON Converter

    [Fact]
    public void Duid_JsonConverter_RoundTrip()
    {
        var id = Duid.NewDuid();
        var options = new JsonSerializerOptions
        {
            Converters = { new DuidJsonConverter() }
        };
        var json = JsonSerializer.Serialize(id, options);

        // JSON should be a single string token of length 22 (+ quotes)
        Assert.StartsWith("\"", json);
        Assert.EndsWith("\"", json);
        Assert.Equal(24, json.Length); // quotes + 22 chars

        var back = JsonSerializer.Deserialize<Duid>(json, options);
        Assert.Equal(id, back);
    }

    #endregion
    
    #region TypeConverter

    [Fact]
    public void Duid_TypeConverter_FromAndToString()
    {
        var id = Duid.NewDuid();
        var s  = id.ToString();

        var conv = TypeDescriptor.GetConverter(typeof(Duid));
        Assert.True(conv.CanConvertFrom(typeof(string)));
        Assert.True(conv.CanConvertTo(typeof(string)));

        var from = (Duid)conv.ConvertFrom(s)!;
        Assert.Equal(id, from);

        var to = (string)conv.ConvertTo(id, typeof(string))!;
        Assert.Equal(s, to);
    }

    #endregion
    
    #region TryParse (null / wrong types)

    [Fact]
    public void Duid_TryParse_Various_Failures()
    {
        Assert.False(Duid.TryParse(null, out _));
        Assert.False(Duid.TryParse(ReadOnlySpan<char>.Empty, out _));
        Assert.False(Duid.TryParse("not22chars", out _));

        // Contains a character not in the alphabet (e.g., '-')
        var str = Duid.NewDuid().ToString();
        var bad = str[..5] + "-" + str[6..];
     
        Assert.False(Duid.TryParse(bad, out _));
    }
    
    #endregion
}