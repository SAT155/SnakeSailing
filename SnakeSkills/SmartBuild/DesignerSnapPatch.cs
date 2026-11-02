#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

[HarmonyPatch(typeof(Piece), "GetSnapPoints", new Type[]
{
	typeof(Vector3),
	typeof(float),
	typeof(List<Transform>),
	typeof(List<Piece>)
})]
internal static class DesignerSnapPatch
{
	private static void Postfix(Vector3 point, float radius, List<Transform> points)
	{
		try
		{
			Player localPlayer = Player.m_localPlayer;
			if (!(localPlayer == null) && SmartHammer.Equipped(localPlayer))
			{
				DesignerSession.AppendSnapPoints(point, radius, points);
			}
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("designer-snap", "Adding the designer box's snap points failed: " + ex);
		}
	}
}
