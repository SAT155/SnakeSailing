#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using HarmonyLib;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(Player), "UpdateWearNTearHover")]
internal static class HoverReachPatch
{
	private static void Prefix(Player __instance, out float __state)
	{
		__state = SelectReachScope.Raise(__instance);
	}

	private static void Finalizer(Player __instance, float __state)
	{
		SelectReachScope.Restore(__instance, __state);
	}
}
