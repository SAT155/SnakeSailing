#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SnakeSmartHammer;

internal static class BlueprintPlacer
{
	private const int ValidatePerFrame = 50;

	public static Blueprint Active { get; private set; }

	public static void Arm(Blueprint blueprint)
	{
		Active = blueprint;
	}

	public static void Disarm()
	{
		Active = null;
	}

	public static bool TryMeasure(Player player, Blueprint blueprint, out Dictionary<string, Piece> pieces, out Dictionary<string, PieceBounds> bounds, out string missing)
	{
		pieces = new Dictionary<string, Piece>();
		bounds = new Dictionary<string, PieceBounds>();
		missing = null;
		foreach (BlueprintItem item in blueprint.Items)
		{
			if (!pieces.ContainsKey(item.Prefab))
			{
				GameObject gameObject = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(item.Prefab) : null);
				Piece piece = ((gameObject != null) ? gameObject.GetComponent<Piece>() : null);
				if (piece == null)
				{
					missing = item.Prefab;
					return false;
				}
				pieces[item.Prefab] = piece;
				GameObject gameObject2 = GameApi.CloneGhost(gameObject, player);
				try
				{
					Footprint footprint = Footprint.Measure(gameObject2);
					bounds[item.Prefab] = new PieceBounds(new Vector3(footprint.MinX, footprint.MinY, footprint.MinZ), new Vector3(footprint.MaxX, footprint.MaxY, footprint.MaxZ), footprint.Snaps);
				}
				finally
				{
					UnityEngine.Object.Destroy(gameObject2);
				}
			}
		}
		return true;
	}

	public static GridLayout RepeatsFor(Blueprint blueprint, Dictionary<string, PieceBounds> bounds)
	{
		PieceBounds value;
		Func<string, PieceBounds> localBounds = (string prefab) => (!bounds.TryGetValue(prefab, out value)) ? default(PieceBounds) : value;
		Steps steps = blueprint.FootprintSteps(localBounds);
		(Vector3 Min, Vector3 Max) tuple = blueprint.LocalBounds(localBounds);
		Vector3 item = tuple.Min;
		Vector3 item2 = tuple.Max;
		Footprint fp = default(Footprint);
		fp.SpanX = steps.X;
		fp.SpanY = steps.Y;
		fp.SpanZ = steps.Z;
		fp.MinX = item.x;
		fp.MaxX = item2.x;
		fp.MinY = item.y;
		fp.MaxY = item2.y;
		fp.MinZ = item.z;
		fp.MaxZ = item2.z;
		return GridState.LayoutFor(fp);
	}

	public static Quaternion AnchorRotation(GameObject ghost)
	{
		return GridMath.Yaw((ghost.transform.rotation * GridMath.Inverse(GridState.TiltRotation)).eulerAngles.y) * GridState.TiltRotation;
	}

	public static bool Begin(Player player, GameObject ghost)
	{
		Blueprint active = Active;
		if (active == null)
		{
			return false;
		}
		if (!PlacementHistory.TryStart(player, "Still placing the previous blueprint"))
		{
			return true;
		}
		Vector3 position = ghost.transform.position;
		Quaternion rot = AnchorRotation(ghost);
		Plugin.Instance.StartCoroutine(Guard(Run(player, active, position, rot, GridState.FollowTerrain), player));
		return true;
	}

	private static IEnumerator Guard(IEnumerator inner, Player player)
	{
		try
		{
			while (true)
			{
				bool flag;
				try
				{
					flag = inner.MoveNext();
				}
				catch (Exception ex)
				{
					Plugin.LogOnce("blueprint-place", "Blueprint placement failed: " + ex);
					GameApi.Message(player, "Blueprint placement failed, see the BepInEx log");
					flag = false;
				}
				if (flag)
				{
					yield return inner.Current;
					continue;
				}
				break;
			}
		}
		finally
		{
			PlacementHistory.Finish();
		}
	}

	private static IEnumerator Run(Player player, Blueprint blueprint, Vector3 anchor, Quaternion rot, bool followTerrain)
	{
		if (!TryMeasure(player, blueprint, out var pieces, out var bounds, out var missing))
		{
			GameApi.Message(player, "Missing piece: " + missing + " (this blueprint needs a mod that isn't loaded)");
			yield break;
		}
		Dictionary<string, GameObject> probes = new Dictionary<string, GameObject>();
		try
		{
			foreach (string key in pieces.Keys)
			{
				GameObject gameObject = GameApi.CloneGhost(ZNetScene.instance.GetPrefab(key), player);
				gameObject.name = "SmartBuildBlueprintProbe";
				gameObject.SetActive(value: true);
				probes[key] = gameObject;
			}
			GridLayout repeats = RepeatsFor(blueprint, bounds);
			int copies = repeats.Count;
			int itemsPerCopy = blueprint.Items.Count;
			int total = copies * itemsPerCopy;
			Dictionary<string, int> perCopyCounts = blueprint.PieceCounts(1);
			int[] order = blueprint.PlacementOrder();
			float? anchorGround = (followTerrain ? GameApi.GroundHeight(anchor) : null);
			int blocked = 0;
			int checkedCount = 0;
			for (int r2 = 0; r2 < copies; r2++)
			{
				repeats.Pose(anchor, rot, r2, out var copyPos, out var copyRot);
				float shift = (anchorGround.HasValue ? GameApi.TerrainShift(anchorGround.Value, copyPos) : 0f);
				for (int oi = 0; oi < itemsPerCopy; oi++)
				{
					int index = order[oi];
					string prefab = blueprint.Items[index].Prefab;
					blueprint.PoseOf(index, copyPos, copyRot, out var position, out var rotation);
					position.y += shift;
					GameObject gameObject2 = probes[prefab];
					gameObject2.transform.SetPositionAndRotation(position, rotation);
					Physics.SyncTransforms();
					if (!CellValidator.IsValid(player, pieces[prefab], gameObject2, position))
					{
						blocked++;
					}
					checkedCount++;
					if (checkedCount % 50 == 49)
					{
						yield return null;
					}
					if (player == null)
					{
						yield break;
					}
				}
				copyPos = default(Vector3);
				copyRot = default(Quaternion);
			}
			if (blocked > 0)
			{
				GameApi.Message(player, "Blocked: " + blocked + " of " + total + " pieces can't be placed here");
				yield break;
			}
			Dictionary<string, int> dictionary = blueprint.PieceCounts(copies);
			foreach (KeyValuePair<string, int> item in dictionary)
			{
				if (!GridCost.CanAfford(player, pieces[item.Key], item.Value))
				{
					GameApi.Message(player, GridCost.Shortage(player, pieces[item.Key], item.Value));
					yield break;
				}
			}
			string text = GridCost.CombinedShortage(player, pieces[blueprint.Items[0].Prefab], GridCost.Combined(player, blueprint, pieces, dictionary));
			if (text != null)
			{
				GameApi.Message(player, text);
				yield break;
			}
			GridState.Reset();
			PlacementHistory.BeginCapture(DateTime.UtcNow.Ticks);
			bool cheated = player.NoCostCheat();
			bool anyCharged = false;
			string stopped = null;
			int placedSinceYield = 0;
			int perFrame = ModConfig.PlacePerFrame.Value;
			for (int r2 = 0; r2 < copies; r2++)
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
				repeats.Pose(anchor, rot, r2, out var position2, out var cellRotation);
				foreach (KeyValuePair<string, int> item2 in perCopyCounts)
				{
					if (!GridCost.CanAfford(player, pieces[item2.Key], item2.Value))
					{
						stopped = "Stopped after " + PlacementHistory.Captured.Count + " pieces: " + GridCost.Shortage(player, pieces[item2.Key], item2.Value);
						break;
					}
				}
				if (stopped != null)
				{
					break;
				}
				foreach (KeyValuePair<string, int> item3 in perCopyCounts)
				{
					if (!GameApi.IsFree(player, pieces[item3.Key]))
					{
						GameApi.ConsumeResources(player, pieces[item3.Key], item3.Value);
						anyCharged = true;
					}
				}
				float num = (anchorGround.HasValue ? GameApi.TerrainShift(anchorGround.Value, position2) : 0f);
				PlacementHistory.EnterCaptureWindow();
				try
				{
					for (int i = 0; i < itemsPerCopy; i++)
					{
						int index2 = order[i];
						string prefab2 = blueprint.Items[index2].Prefab;
						blueprint.PoseOf(index2, position2, cellRotation, out var position3, out var rotation2);
						position3.y += num;
						GameApi.PlacePiece(player, pieces[prefab2], position3, rotation2, r2 == 0 && i == 0, cheated);
						placedSinceYield++;
					}
				}
				finally
				{
					PlacementHistory.ExitCaptureWindow();
				}
				if (placedSinceYield >= perFrame)
				{
					placedSinceYield = 0;
					yield return null;
				}
			}
			int count = PlacementHistory.Captured.Count;
			if (count > 0 && PlacementHistory.Captured[0] != null)
			{
				blueprint.PoseOf(order[0], anchor, rot, out var asked, out var _);
				PlacementAnchor.Log("Blueprint '" + blueprint.Name + "': first piece asked for " + PlacementAnchor.Fmt(asked) + ", landed at " + PlacementAnchor.Fmt(PlacementHistory.Captured[0].transform.position) + " (anchor " + PlacementAnchor.Fmt(anchor) + ", " + count + " pieces)");
			}
			PlacementHistory.EndCapture(anyCharged);
			GameApi.Message(player, stopped ?? ("Placed " + copies + "x '" + blueprint.Name + "' (" + count + " pieces) - still armed, click to place another"));
		}
		finally
		{
			foreach (GameObject value in probes.Values)
			{
				if (value != null)
				{
					UnityEngine.Object.Destroy(value);
				}
			}
		}
	}
}
