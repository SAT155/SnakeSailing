#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using HarmonyLib;

namespace SnakeSmartHammer;

[HarmonyPatch(typeof(Player), "Repair")]
internal static class DesignerRepairClickPatch
{
	private static bool Prefix(Player __instance)
	{
		try
		{
			if (__instance != Player.m_localPlayer || !SmartHammer.Equipped(__instance) || !DesignerSession.Following)
			{
				return true;
			}
			DesignerSession.OnPlaced(__instance);
			return false;
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("designer-repair", "Setting the designer box down failed: " + ex);
			return true;
		}
	}
}
