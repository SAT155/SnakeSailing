#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using HarmonyLib;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(ZInput), "GetMouseScrollWheel")]
internal static class WheelPatch
{
	private static void Postfix(ref float __result)
	{
		try
		{
			Wheel.Intercept(ref __result);
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("wheel", "Mouse wheel handling failed: " + ex);
		}
	}
}
