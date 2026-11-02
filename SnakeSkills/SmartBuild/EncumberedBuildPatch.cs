#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using HarmonyLib;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(Player), "UpdatePlacement")]
internal static class EncumberedBuildPatch
{
	internal static bool Active;

	private static void Prefix(Player __instance)
	{
		try
		{
			Active = __instance == Player.m_localPlayer && SmartHammer.Equipped(__instance) && __instance.IsEncumbered();
		}
		catch (Exception)
		{
			Active = false;
		}
	}

	private static void Finalizer()
	{
		Active = false;
	}
}
