#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using HarmonyLib;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(Player), "HaveStamina")]
internal static class EncumberedStaminaPatch
{
	private static void Postfix(Player __instance, ref bool __result)
	{
		if (EncumberedBuildPatch.Active && __instance == Player.m_localPlayer)
		{
			__result = true;
		}
	}
}
