#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using HarmonyLib;
using UnityEngine;

namespace SnakeSmartHammer;

[HarmonyPatch(typeof(PlayerController), "LateUpdate")]
internal static class PanelLookPatch
{
	private static bool Prefix(PlayerController __instance)
	{
		if (!ControlPanel.IsOpen && !DesignerPrompt.IsOpen && !BlueprintList.IsOpen)
		{
			return true;
		}
		try
		{
			if (__instance.m_character == null || __instance.m_character != Player.m_localPlayer)
			{
				return true;
			}
			__instance.m_character.SetMouseLook(Vector2.zero);
			return false;
		}
		catch (Exception)
		{
			return true;
		}
	}
}
