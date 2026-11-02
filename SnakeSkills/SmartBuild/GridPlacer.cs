#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

internal static class GridPlacer
{
	private const int ValidatePerFrame = 200;

	public static bool Begin(Player player, Piece piece, GameObject ghost)
	{
		if (!PlacementHistory.TryStart(player, "Still placing the previous grid"))
		{
			return true;
		}
		GridLayout layout = GridState.LayoutFor(Footprint.Measure(ghost));
		GameObject gameObject = GameApi.CloneGhost(player);
		gameObject.name = "SmartBuildProbe";
		gameObject.SetActive(value: true);
		Plugin.Instance.StartCoroutine(Guard(Run(player, piece, gameObject, ghost.transform.position, ghost.transform.rotation, layout, GridState.FollowTerrain), gameObject, player));
		return true;
	}

	private static IEnumerator Guard(IEnumerator inner, GameObject probe, Player player)
	{
		try
		{
			while (true)
			{
				object obj = null;
				bool flag;
				try
				{
					flag = inner.MoveNext();
					if (flag)
					{
						obj = inner.Current;
					}
				}
				catch (Exception ex)
				{
					Plugin.LogOnce("placer", "Grid placement failed: " + ex);
					GameApi.Message(player, "Grid placement failed, see the BepInEx log");
					flag = false;
				}
				if (flag)
				{
					yield return obj;
					continue;
				}
				break;
			}
		}
		finally
		{
			if (probe != null)
			{
				UnityEngine.Object.Destroy(probe);
			}
			PlacementHistory.Finish();
		}
	}

	private static IEnumerator Run(Player player, Piece piece, GameObject probe, Vector3 anchor, Quaternion rot, GridLayout layout, bool followTerrain)
	{
		int total = layout.Count;
		float? anchorGround = (followTerrain ? GameApi.GroundHeight(anchor) : null);
		int blocked = 0;
		for (int i = 0; i < total; i++)
		{
			layout.Pose(anchor, rot, i, out var position, out var cellRotation);
			if (anchorGround.HasValue)
			{
				position = GameApi.FollowTerrain(anchorGround.Value, position);
			}
			probe.transform.SetPositionAndRotation(position, cellRotation);
			Physics.SyncTransforms();
			if (!CellValidator.IsValid(player, piece, probe, position))
			{
				blocked++;
			}
			if (i % 200 == 199)
			{
				yield return null;
			}
			if (player == null)
			{
				yield break;
			}
		}
		if (blocked > 0)
		{
			GameApi.Message(player, "Grid blocked: " + blocked + " of " + total + " cells can't be placed");
			yield break;
		}
		bool free = GameApi.IsFree(player, piece);
		if (!GridCost.CanAfford(player, piece, total))
		{
			GameApi.Message(player, GridCost.Shortage(player, piece, total));
			yield break;
		}
		GridState.Reset();
		GridHold.Release();
		PlacementHistory.BeginCapture(DateTime.UtcNow.Ticks);
		int perFrame = ModConfig.PlacePerFrame.Value;
		string stopped = null;
		bool cheated = player.NoCostCheat();
		for (int i = 0; i < total; i += perFrame)
		{
			if (stopped != null)
			{
				break;
			}
			if (player == null || GameApi.IsDead(player))
			{
				stopped = "Stopped after " + PlacementHistory.Captured.Count + " pieces";
				break;
			}
			int num = Mathf.Min(perFrame, total - i);
			if (!free)
			{
				if (!GridCost.CanAfford(player, piece, num))
				{
					stopped = "Stopped after " + PlacementHistory.Captured.Count + " pieces: " + GridCost.Shortage(player, piece, num);
					break;
				}
				GameApi.ConsumeResources(player, piece, num);
			}
			PlacementHistory.EnterCaptureWindow();
			try
			{
				for (int k = i; k < i + num; k++)
				{
					layout.Pose(anchor, rot, k, out var position2, out var cellRotation2);
					if (anchorGround.HasValue)
					{
						position2 = GameApi.FollowTerrain(anchorGround.Value, position2);
					}
					GameApi.PlacePiece(player, piece, position2, cellRotation2, k == 0, cheated);
				}
			}
			finally
			{
				PlacementHistory.ExitCaptureWindow();
			}
			yield return null;
		}
		int count = PlacementHistory.Captured.Count;
		if (count > 0 && PlacementHistory.Captured[0] != null)
		{
			layout.Pose(anchor, rot, 0, out var asked, out var _);
			PlacementAnchor.Log("Grid: first piece asked for " + PlacementAnchor.Fmt(asked) + ", landed at " + PlacementAnchor.Fmt(PlacementHistory.Captured[0].transform.position) + " (" + count + " pieces)");
		}
		PlacementHistory.EndCapture(!free);
		GameApi.Message(player, stopped ?? ("Placed " + count + " pieces"));
	}
}
