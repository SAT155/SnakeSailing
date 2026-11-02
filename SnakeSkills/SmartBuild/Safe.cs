#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;

namespace SnakeSkills.SmartBuild;

internal static class Safe
{
	public static T Call<T>(Func<T> call, T fallback, string what)
	{
		try
		{
			return call();
		}
		catch (Exception ex)
		{
			Plugin.LogOnce(what, what + " unavailable in this game version: " + ex.Message);
			return fallback;
		}
	}

	public static void Run(Action call, string what)
	{
		try
		{
			call();
		}
		catch (Exception ex)
		{
			Plugin.LogOnce(what, what + " unavailable in this game version: " + ex.Message);
		}
	}
}
