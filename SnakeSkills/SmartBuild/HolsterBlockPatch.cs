#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using HarmonyLib;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(Humanoid), "HideHandItems")]
internal static class HolsterBlockPatch
{
	private static bool Prefix(Humanoid __instance, ref bool __result)
	{
		Player player = __instance as Player;
		if (player == null || player != Player.m_localPlayer || !SmartHammer.Equipped(player))
		{
			return true;
		}
		try
		{
			if (!player.InPlaceMode() || (!Keys.Held(ModConfig.KeyRotation.Value) && !ControlPanel.CapturingInput))
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
