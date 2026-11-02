#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using UnityEngine;

namespace SnakeSkills.SmartBuild;

internal static class CellValidator
{
	public static bool IsValid(Player player, Piece piece, GameObject cellGhost, Vector3 pos)
	{
		if (piece.m_noClipping && GameApi.IsClipping(player, cellGhost))
		{
			return false;
		}
		if (!GameApi.HasWardAccess(pos))
		{
			return false;
		}
		if (GameApi.InNoBuildZone(pos))
		{
			return false;
		}
		if (GameApi.WrongBiome(piece, pos))
		{
			return false;
		}
		return true;
	}
}
