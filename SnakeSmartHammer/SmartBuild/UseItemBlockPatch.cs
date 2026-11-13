#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using HarmonyLib;

namespace SnakeSmartHammer;

[HarmonyPatch(typeof(Humanoid), "UseItem")]
internal static class UseItemBlockPatch
{
	private static bool Prefix(Humanoid __instance)
	{
		Player player = __instance as Player;
		if (player == null || player != Player.m_localPlayer || !SmartHammer.Equipped(player))
		{
			return true;
		}
		try
		{
			return !player.InPlaceMode() || (!ControlPanel.CapturingInput && !DesignerPrompt.IsOpen && !Plugin.AxisKeyHeld() && !Keys.Held(ModConfig.KeyRotation.Value));
		}
		catch (Exception)
		{
			return true;
		}
	}
}
