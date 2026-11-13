#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using HarmonyLib;
using UnityEngine;

namespace SnakeSmartHammer;

[HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
internal static class UpdatePlacementGhostPatch
{
	private static string _lastPiece;

	// Fork: run after every other mod's postfix (Valheim Plus grid alignment and Advanced Building
	// Mode move the ghost in theirs). Upstream ran in load order, so a later mod could move the
	// ghost after the preview was drawn from it, and the grid landed where the preview wasn't.
	[HarmonyPriority(Priority.Last)]
	[HarmonyAfter(new string[] { "org.bepinex.plugins.valheim_plus" })]
	private static void Postfix(Player __instance)
	{
		if (__instance != Player.m_localPlayer || !SmartHammer.Equipped(__instance))
		{
			return;
		}
		try
		{
			GameObject placementGhost = __instance.m_placementGhost;
			if (placementGhost != null)
			{
				if (_lastPiece != null && _lastPiece != placementGhost.name)
				{
					BlueprintPlacer.Disarm();
					BlueprintSession.Clear();
				}
				_lastPiece = placementGhost.name;
				GridState.NoteSelectedPiece(placementGhost.name);
				if (BlueprintPieces.TryGetBlueprint(placementGhost.name, out var blueprint))
				{
					BlueprintPlacer.Arm(blueprint);
				}
			}
			GameObject placementGhost2 = __instance.m_placementGhost;
			if (!GridHold.Frozen && GridState.HasTilt && placementGhost2 != null)
			{
				placementGhost2.transform.rotation = placementGhost2.transform.rotation * GridState.TiltRotation;
			}
			GridHold.Apply(__instance);
			PlacementAnchor.Record(__instance.m_placementGhost);
			GridPreview.Update(__instance);
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("preview", "Preview failed: " + ex);
		}
	}
}
