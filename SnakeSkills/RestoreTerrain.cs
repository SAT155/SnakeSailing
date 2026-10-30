using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using SkillManager;
using UnityEngine;
using static SnakeSkills.SnakeSkillsPlugin;
using Object = UnityEngine.Object;

namespace SnakeSkills;

// Restore terrain: a hoe piece, unlocked by Landscaping, that moves the ground in the brush back
// toward how the world generated it (fills holes, takes down raised ground), free of stone and
// without a workbench. How much ground one strike moves grows with the skill, and each point moves
// less toward the edge of the brush, so a partial restore leaves a soft mound or dip, not a step.
//
// The piece is a copy of the level ground piece with its TerrainOp removed, so placing it does
// nothing by itself; Player.PlacePiece is intercepted for it (the way SnakeSmarterHoe's tools work)
// and the restore is written straight into the TerrainComp deltas, which are the difference from
// the generated height (0 = untouched).
internal static class RestoreTerrain
{
	private const string Section = "7 - Landscaping";
	private const string PrefabName = "SnakeSkills_RestoreTerrain";
	private const string PieceName = "Restore terrain";

	private static ConfigEntry<Toggle> enabled = null!;
	private static ConfigEntry<int> unlockLevel = null!;
	private static ConfigEntry<float> volumeAtUnlock = null!;
	private static ConfigEntry<float> volumeAt100 = null!;
	private static ConfigEntry<float> depthAtUnlock = null!;
	private static ConfigEntry<float> depthAt100 = null!;
	private static ConfigEntry<Toggle> restorePaint = null!;

	private static GameObject? prefab;
	private static float baseRadius = 4f;

	public static void Init(SnakeSkillsPlugin mod)
	{
		enabled = mod.config(Section, "Restore Terrain Tool", Toggle.On, "If on, the hoe gets a Restore terrain piece once Landscaping is high enough. It moves the ground in the brush back toward how the world generated it (fills holes, lowers raised ground), with no stone or workbench needed.");
		unlockLevel = mod.config(Section, "Restore Unlock Level", 25, new ConfigDescription("Landscaping level at which Restore terrain shows up in the hoe menu (re-equip the hoe after reaching it).", new AcceptableValueRange<int>(0, 100)));
		volumeAtUnlock = mod.config(Section, "Restore Volume At Unlock", 2f, new ConfigDescription("Cubic metres of ground one Restore terrain strike can fill or remove at the unlock level. A vanilla Raise ground strike adds roughly 1-2 m³ for its stone.", new AcceptableValueRange<float>(0.1f, 100f)));
		volumeAt100 = mod.config(Section, "Restore Volume At 100", 20f, new ConfigDescription("Cubic metres one strike can fill or remove at Landscaping 100. In between it grows with the skill.", new AcceptableValueRange<float>(0.1f, 500f)));
		depthAtUnlock = mod.config(Section, "Restore Depth At Unlock", 0.5f, new ConfigDescription("Most one point in the middle of the brush can move per strike (m) at the unlock level. Points move less toward the edge, so the result stays smooth.", new AcceptableValueRange<float>(0.05f, 8f)));
		depthAt100 = mod.config(Section, "Restore Depth At 100", 2f, new ConfigDescription("Most one point in the middle of the brush can move per strike (m) at Landscaping 100.", new AcceptableValueRange<float>(0.05f, 16f)));
		restorePaint = mod.config(Section, "Restore Paint", Toggle.On, "If on, Restore terrain also takes away path, paved and cultivated paint where the ground is fully back to its generated height, so grass grows back.");
	}

	internal static bool IsRestorePiece(GameObject go) => Utils.GetPrefabName(go) == PrefabName;

	private static bool Unlocked(Player player) => enabled.Value == Toggle.On && Landscaping.Active && player.GetSkillFactor("Landscaping") * 100f >= unlockLevel.Value - 0.001f;

	private static PieceTable? HoeTable()
	{
		GameObject? hoe = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab("Hoe") : null;
		return hoe != null && hoe.GetComponent<ItemDrop>() is { } drop ? drop.m_itemData.m_shared.m_buildPieces : null;
	}

	// The piece: a copy of the level ground piece (same ghost, effects and placement rules) without
	// its terrain operation, network view or cost, kept under an inactive root so it never wakes up.
	private static void Setup(PieceTable table)
	{
		if (prefab == null)
		{
			GameObject? source = table.m_pieces.Find(p => p != null && p.name.StartsWith("mud_road")) ?? table.m_pieces.Find(p => p != null && p.GetComponent<TerrainOp>() != null);
			if (source == null)
			{
				return;
			}

			if (source.GetComponent<TerrainOp>() is { } op && op.m_settings.GetRadius() > 0.1f)
			{
				baseRadius = op.m_settings.GetRadius();
			}

			GameObject root = new("SnakeSkills_Prefabs");
			root.SetActive(false);
			Object.DontDestroyOnLoad(root);
			prefab = Object.Instantiate(source, root.transform);
			prefab.name = PrefabName;
			foreach (TerrainOp op2 in prefab.GetComponentsInChildren<TerrainOp>(true))
			{
				Object.DestroyImmediate(op2);
			}
			foreach (ZNetView view in prefab.GetComponentsInChildren<ZNetView>(true))
			{
				Object.DestroyImmediate(view);
			}

			Piece piece = prefab.GetComponent<Piece>();
			piece.m_name = PieceName;
			piece.m_description = "Moves the ground in the brush back toward how it was when the world was made: fills holes and takes down raised ground. No stone needed. Landscaping lets each strike move more.";
			piece.m_resources = new Piece.Requirement[0];
			piece.m_craftingStation = null;
			if (LoadIcon() is { } icon)
			{
				piece.m_icon = icon;
			}
		}

		if (!table.m_pieces.Contains(prefab))
		{
			table.m_pieces.Add(prefab);
		}
	}

	private static Sprite? LoadIcon()
	{
		using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SnakeSkills.icons.restore.png");
		if (stream == null)
		{
			return null;
		}
		byte[] data = new byte[stream.Length];
		_ = stream.Read(data, 0, data.Length);
		Texture2D texture = new(2, 2);
		// ImageConversion.LoadImage by reflection: referencing its module at build time pulls in
		// netstandard 2.1, which a net48 build can't use (CS1705).
		MethodInfo? load = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
		if (load == null || !(bool)load.Invoke(null, new object[] { texture, data }))
		{
			return null;
		}
		return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
	}

	// Shows (or hides) the piece in the hoe menu by skill. Runs whenever the build menu's list is
	// rebuilt; the piece only shows when its m_name is in the player's known recipes.
	[HarmonyPatch(typeof(PieceTable), nameof(PieceTable.UpdateAvailable))]
	private static class ShowWhenUnlocked
	{
		private static void Prefix(PieceTable __instance, HashSet<string> knownRecipies, Player player)
		{
			if (player != Player.m_localPlayer || knownRecipies == null || __instance != HoeTable())
			{
				return;
			}

			Setup(__instance);
			Landscaping.AddHints(__instance);
			if (Unlocked(player))
			{
				knownRecipies.Add(PieceName);
			}
			else
			{
				knownRecipies.Remove(PieceName);
			}
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
	private static class Strike
	{
		private static bool Prefix(Player __instance, Piece piece, Vector3 pos, bool doAttack)
		{
			if (__instance != Player.m_localPlayer || piece == null || piece.gameObject != prefab)
			{
				return true;
			}

			if (!Unlocked(__instance))
			{
				__instance.Message(MessageHud.MessageType.Center, $"Restore terrain needs Landscaping {unlockLevel.Value}");
				return false;
			}

			float radius = baseRadius * Landscaping.BrushScale(__instance);
			if (!PrivateArea.CheckAccess(pos, radius))
			{
				return false;
			}

			if (!__instance.NoCostCheat())
			{
				float stamina = __instance.GetBuildStamina();
				if (!__instance.HaveStamina(stamina))
				{
					if (Hud.instance != null)
					{
						Hud.instance.StaminaBarEmptyFlash();
					}
					return false;
				}
				__instance.UseStamina(stamina);
				if (__instance.GetRightItem() is { m_shared.m_useDurability: true } tool)
				{
					tool.m_durability = Mathf.Max(0f, tool.m_durability - tool.m_shared.m_useDurabilityDrain);
				}
			}

			if (doAttack && __instance.m_zanim != null)
			{
				__instance.FaceLookDirection();
				__instance.m_zanim.SetTrigger(__instance.GetRightItem()?.m_shared.m_attack?.m_attackAnimation ?? "attack");
			}

			float skill = Mathf.InverseLerp(unlockLevel.Value / 100f, 1f, __instance.GetSkillFactor("Landscaping"));
			float volume = Mathf.Lerp(volumeAtUnlock.Value, volumeAt100.Value, skill);
			float depth = Mathf.Lerp(depthAtUnlock.Value, depthAt100.Value, skill);
			float moved = Restore(pos, radius, volume, depth, restorePaint.Value == Toggle.On);

			piece.m_placeEffect?.Create(pos, Quaternion.identity, null, 1f, -1, default(ZDOID));
			__instance.RaiseSkill("Landscaping");
			if (moved < 0.01f)
			{
				__instance.Message(MessageHud.MessageType.TopLeft, "This ground is already as the world made it");
			}
			return false;
		}
	}

	// Moves every vertex in the brush toward a delta of 0 (its generated height). A vertex can move
	// at most maxDepth × falloff, with falloff 1 in the inner half of the brush easing to 0 at the
	// edge, and all moves are scaled down together if they add up to more than maxVolume. Returns
	// the volume moved (m³).
	private static float Restore(Vector3 center, float radius, float maxVolume, float maxDepth, bool paint)
	{
		List<Heightmap> heightmaps = TerrainZones.heightmaps;
		TerrainZones.Claim(center, radius);
		heightmaps.Clear();
		Heightmap.FindHeightmap(center, radius + 2f, heightmaps);
		if (heightmaps.Count == 0)
		{
			return 0f;
		}

		float scale = heightmaps[0].m_scale;
		// World grid point → (current delta, wanted move, every heightmap vertex that is this point).
		Dictionary<(int, int), (float delta, float move, List<(Heightmap hmap, TerrainComp comp, int i, int x, int y)> owners)> grid = new();
		foreach (Heightmap hmap in heightmaps)
		{
			TerrainComp comp = hmap.GetAndCreateTerrainCompiler();
			if (comp == null || !comp.m_initialized || !comp.IsOwner() || comp.m_levelDelta == null)
			{
				continue;
			}

			Vector3 origin = hmap.transform.position;
			int half = hmap.m_width / 2;
			for (int y = 0; y <= hmap.m_width; y++)
			{
				float dz = origin.z + (y - half) * scale - center.z;
				if (Mathf.Abs(dz) > radius)
				{
					continue;
				}
				for (int x = 0; x <= hmap.m_width; x++)
				{
					float dx = origin.x + (x - half) * scale - center.x;
					float dist = Mathf.Sqrt(dx * dx + dz * dz);
					if (dist > radius)
					{
						continue;
					}

					int i = y * (comp.m_width + 1) + x;
					(int, int) key = (Mathf.RoundToInt((origin.x + (x - half) * scale) / scale), Mathf.RoundToInt((origin.z + (y - half) * scale) / scale));
					if (!grid.TryGetValue(key, out var point))
					{
						float delta = comp.m_levelDelta[i] + comp.m_smoothDelta[i];
						float falloff = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius * 0.5f, radius, dist));
						float move = -Mathf.Sign(delta) * Mathf.Min(Mathf.Abs(delta), maxDepth * falloff);
						point = (delta, move, new List<(Heightmap, TerrainComp, int, int, int)>());
					}
					point.owners.Add((hmap, comp, i, x, y));
					grid[key] = point;
				}
			}
		}

		float volume = 0f;
		foreach (var point in grid.Values)
		{
			volume += Mathf.Abs(point.move) * scale * scale;
		}
		float factor = volume > maxVolume ? maxVolume / volume : 1f;

		HashSet<Heightmap> changed = new();
		foreach (var point in grid.Values)
		{
			float move = point.move * factor;
			float after = point.delta + move;
			bool home = Mathf.Abs(after) < 0.01f;
			if (Mathf.Abs(move) < 0.001f && !(home && paint))
			{
				continue;
			}

			foreach ((Heightmap hmap, TerrainComp comp, int i, int x, int y) in point.owners)
			{
				comp.m_levelDelta[i] = home ? 0f : after;
				comp.m_smoothDelta[i] = 0f;
				comp.m_modifiedHeight[i] = !home;
				if (home && paint && comp.m_modifiedPaint != null && comp.m_modifiedPaint[i])
				{
					comp.m_modifiedPaint[i] = false;
					comp.m_paintMask[i] = Heightmap.m_paintMaskNothing;
					if (hmap.m_paintMask != null)
					{
						hmap.m_paintMask.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
					}
				}
				changed.Add(hmap);
			}
		}

		foreach (Heightmap hmap in changed)
		{
			TerrainComp comp = hmap.GetAndCreateTerrainCompiler();
			comp.m_operations++;
			comp.Save(false);
			hmap.Poke(0, false);
		}
		if (ClutterSystem.instance != null)
		{
			ClutterSystem.instance.ResetGrass(center, radius + 2f);
		}
		return Mathf.Min(volume, maxVolume);
	}
}
