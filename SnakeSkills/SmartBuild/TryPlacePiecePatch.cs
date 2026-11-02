#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using HarmonyLib;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(Player), "TryPlacePiece")]
internal static class TryPlacePiecePatch
{
	private static bool Prefix(Player __instance, Piece piece, ref bool __result)
	{
		if (__instance != Player.m_localPlayer || !SmartHammer.Equipped(__instance) || (!GridState.IsGrid && !GridHold.Frozen && BlueprintPlacer.Active == null))
		{
			return true;
		}
		try
		{
			GameObject placementGhost = __instance.m_placementGhost;
			if (placementGhost == null || piece == null)
			{
				return true;
			}
			if (BlueprintPlacer.Active != null)
			{
				PlacementAnchor.Restore(placementGhost, "Blueprint '" + BlueprintPlacer.Active.Name + "'");
				if (!BlueprintPlacer.Begin(__instance, placementGhost))
				{
					return true;
				}
				DesignerSession.OnPlaced(__instance);
				__result = false;
				return false;
			}
			if (GridHold.Frozen)
			{
				string text = GridHold.OutOfReach(__instance);
				if (text != null)
				{
					GameApi.Message(__instance, text);
					__result = false;
					return false;
				}
			}
			else if (__instance.GetPlacementStatus() != 0)
			{
				return true;
			}
			PlacementAnchor.Restore(placementGhost, "Grid of " + GridState.Cells + " " + placementGhost.name);
			if (!GridPlacer.Begin(__instance, piece, placementGhost))
			{
				return true;
			}
			DesignerSession.OnPlaced(__instance);
			__result = false;
			return false;
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("place", "Grid placement failed, falling back to vanilla: " + ex);
			return true;
		}
	}

	private static void Postfix(Player __instance, bool __result)
	{
		if (!__result || __instance != Player.m_localPlayer)
		{
			return;
		}
		try
		{
			if (SmartHammer.Equipped(__instance))
			{
				DesignerSession.OnPlaced(__instance);
			}
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("place-designer", "Setting the designer box down failed: " + ex);
		}
	}
}
