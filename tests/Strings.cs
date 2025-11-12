namespace Argentini.Duid.Tests;

/// <summary>
/// Various tools for working with strings. 
/// </summary>
public static partial class Strings
{
	#region Time

	/// <summary>
	/// Formats the elapsed time in the most appropriate unit:
	/// seconds (s), milliseconds (ms), or microseconds (μs).
	/// </summary>
	public static string FormatTimer(this TimeSpan timeSpan)
	{
		if (timeSpan.TotalNanoseconds < 1000)
			return $"{timeSpan.TotalNanoseconds:0} ns";
		
		if (timeSpan.TotalMicroseconds < 1000)
			return $"{timeSpan.TotalMicroseconds:0} μs";
		
		if (timeSpan.TotalMilliseconds < 1000)
			return $"{timeSpan.TotalMilliseconds:0} ms";

		if (timeSpan.TotalSeconds < 60)
			return $"{timeSpan.TotalSeconds:N3} s";

		return $"{timeSpan.Hours:0}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}.{timeSpan.Milliseconds:000} s";
	}

	/// <summary>
	/// Formats the elapsed time in the most appropriate unit:
	/// seconds (s), milliseconds (ms), microseconds (μs), or nanoseconds (ns).
	/// </summary>
	public static string FormatTimerFromNanoseconds(this double nanoseconds)
	{
		if (nanoseconds < 1_000d)
			return $"{nanoseconds:0} ns";
		
		if (nanoseconds < 1_000_000d)
			return $"{nanoseconds / 1_000:0} μs";
		
		return nanoseconds < 1_000_000_000d ? $"{nanoseconds / 1_000_000:0} ms" : $"{nanoseconds / 1_000_000_000:N3} s";
	}

	#endregion
}
