#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using HarmonyLib;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(Player), "StartEmote")]
internal static class SitBlockPatch
{
	private static bool Prefix(Player __instance, string emote, ref bool __result)
	{
		if (emote != "sit" || __instance != Player.m_localPlayer || !SmartHammer.Equipped(__instance))
		{
			return true;
		}
		try
		{
			if (!__instance.InPlaceMode() || (!Plugin.AxisKeyHeld() && !ControlPanel.CapturingInput))
			{
				return true;
			}
		}
		catch (Exception)
		{
			return true;
		}
		__result = false;
		return false;
	}
}
