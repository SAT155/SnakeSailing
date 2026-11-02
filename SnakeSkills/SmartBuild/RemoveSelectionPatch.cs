#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using HarmonyLib;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(Player), "RemovePiece")]
internal static class RemoveSelectionPatch
{
	private static bool Prefix(Player __instance, ref bool __result, out float __state)
	{
		__state = -1f;
		if (__instance != Player.m_localPlayer || !SmartHammer.Equipped(__instance))
		{
			return true;
		}
		bool flag;
		try
		{
			flag = SelectionDelete.TryIntercept(__instance);
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("remove-selection", "Selection delete check failed: " + ex);
			return true;
		}
		if (flag)
		{
			__result = false;
			return false;
		}
		__state = SelectReachScope.Raise(__instance);
		return true;
	}

	private static void Finalizer(Player __instance, float __state)
	{
		SelectReachScope.Restore(__instance, __state);
	}
}
