#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using HarmonyLib;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(Piece), "SetCreator")]
internal static class SetCreatorPatch
{
	private static void Postfix(Piece __instance)
	{
		PlacementHistory.OnPiecePlaced(__instance.gameObject);
	}
}
