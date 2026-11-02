#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using HarmonyLib;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(Player), "StartGuardianPower")]
internal static class GuardianPowerBlockPatch
{
	private static bool Prefix(Player __instance, ref bool __result)
	{
		if (__instance != Player.m_localPlayer || !SmartHammer.Equipped(__instance))
		{
			return true;
		}
		try
		{
			if (!__instance.InPlaceMode() || !Keys.Held(ModConfig.KeyBlueprintSelect.Value))
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
