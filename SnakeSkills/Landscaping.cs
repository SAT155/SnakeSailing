using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using SkillManager;
using UnityEngine;
using static SnakeSkills.SnakeSkillsPlugin;
using Object = UnityEngine.Object;

namespace SnakeSkills;

// Landscaping: a skill for the vanilla hoe. Each hoe strike trains it; with skill the strike costs
// less stamina and the brush can be made bigger (Left Alt + mouse wheel). Every level-ground,
// path or paved-road strike also knocks down lone terrain bumps in its area, and with Shift held
// eases slopes too steep for a cart. Shift doesn't sprint while the hoe's build mode is open.
//
// Terrain edits sync through each zone's TerrainComp ZDO, and only its owner's Save() reaches
// other players. A non-owner's vanilla strike is sent to the owner as just the piece's prefab
// hash, so a bigger brush would be lost: the strike claims every zone it touches first (any
// client may), the same fix SnakeSmarterHoe uses. With the zones owned, the vanilla operation
// runs here at once, and the bump/slope pass right after it works on the result.
internal enum RampMode
{
	Slope,
	Relax,
}

internal enum RampStyle
{
	Fill,
	Cut,
	Spread,
}

internal static class Landscaping
{
	private const string Section = "7 - Landscaping";
	private const string SmarterHoeGUID = "com.jubaroo.smarterhoe";

	private static ConfigEntry<Toggle> enabled = null!;
	private static ConfigEntry<float> staminaReduction = null!;
	private static ConfigEntry<float> maxBrushScale = null!;
	private static ConfigEntry<float> experienceGainedFactor = null!;
	private static ConfigEntry<int> experienceLoss = null!;
	private static ConfigEntry<Toggle> noSprint = null!;
	private static ConfigEntry<Toggle> flattenBumps = null!;
	private static ConfigEntry<float> bumpHeight = null!;
	private static ConfigEntry<Toggle> easePaths = null!;
	private static ConfigEntry<Toggle> easeLevel = null!;
	private static ConfigEntry<float> maxSlope = null!;
	private static ConfigEntry<RampStyle> rampStyle = null!;
	private static ConfigEntry<Toggle> sizeWithWheel = null!;
	private static ConfigEntry<float> brushSize = null!;
	private static ConfigEntry<Toggle> rampNeedsShift = null!;
	private static ConfigEntry<RampMode> rampMode = null!;

	private static Skill landscaping = null!;
	private static bool smarterHoeLoaded;

	public static void Init(SnakeSkillsPlugin mod)
	{
		landscaping = new Skill("Landscaping", "landscaping.png");
		landscaping.Description.English("Lowers the stamina each hoe strike costs and lets you use a bigger hoe brush.");
		landscaping.Configurable = false;

		enabled = mod.config(Section, "Hoe Changes", Toggle.On, "If on, the hoe uses the Landscaping skill (less stamina, bigger brush) and smooths bumps and steep spots. Off is the vanilla hoe; the skill still shows.");
		staminaReduction = mod.config(Section, "Stamina Reduction", 0.6f, new ConfigDescription("Share of a hoe strike's stamina saved at Landscaping 100 (0.6 = 60% less). Scales with the skill.", new AcceptableValueRange<float>(0f, 0.95f)));
		maxBrushScale = mod.config(Section, "Max Brush Scale", 2.5f, new ConfigDescription("How much bigger than vanilla the hoe brush can get at Landscaping 100 (2.5 = two and a half times the radius). At level 0 it is vanilla size; in between it grows with the skill.", new AcceptableValueRange<float>(1f, 5f)));
		sizeWithWheel = mod.config(Section, "Brush Size With Alt + Wheel", Toggle.On, "If on, hold Left Alt and turn the mouse wheel while the hoe's build mode is open to make the brush smaller or bigger, up to what your skill allows. It starts at the biggest.", synchronizedSetting: false);
		brushSize = mod.config(Section, "Brush Size", 0f, new ConfigDescription("The brush size you picked with Left Alt + wheel (1 = vanilla). It stays the same as Landscaping rises; only the upper limit grows. 0 = always the biggest your skill allows.", new AcceptableValueRange<float>(0f, 5f)), synchronizedSetting: false);
		experienceGainedFactor = mod.config(Section, "Skill Experience Gain Factor", 1f, new ConfigDescription("Factor for experience gained for the landscaping skill (one point per hoe strike).", new AcceptableValueRange<float>(0.01f, 5f)));
		experienceGainedFactor.SettingChanged += (_, _) => landscaping.SkillGainFactor = experienceGainedFactor.Value;
		landscaping.SkillGainFactor = experienceGainedFactor.Value;
		experienceLoss = mod.config(Section, "Skill Experience Loss", 0, new ConfigDescription("How much experience to lose in the landscaping skill on death.", new AcceptableValueRange<int>(0, 100)));
		experienceLoss.SettingChanged += (_, _) => landscaping.SkillLoss = experienceLoss.Value;
		landscaping.SkillLoss = experienceLoss.Value;
		noSprint = mod.config(Section, "No Sprint With Hoe", Toggle.On, "If on, Shift doesn't sprint while the hoe's build mode is open, so stamina keeps recovering between strikes.");
		flattenBumps = mod.config(Section, "Flatten Bumps", Toggle.On, "If on, level ground, path and paved road strikes lower any single terrain point in the brush that sticks up above all of its neighbours (the little bumps you'd otherwise hunt with a pickaxe).");
		bumpHeight = mod.config(Section, "Bump Height", 0.1f, new ConfigDescription("How far (m) a point has to stick up above its highest neighbour to count as a bump.", new AcceptableValueRange<float>(0.02f, 1f)));
		easePaths = mod.config(Section, "Ease Slopes On Paths", Toggle.On, "If on, path and paved road strikes made while holding Shift even out terrain inside the brush that is steeper than Max Cart Slope, turning steps into a ramp a cart can take. Without Shift the strike is a normal one.");
		easeLevel = mod.config(Section, "Ease Slopes On Level Ground", Toggle.On, "If on, level ground strikes made while holding Shift also ramp the edge of the flattened area down or up to the ground around it when it is steeper than Max Cart Slope. Without Shift level ground stays flat to the edge.");
		maxSlope = mod.config(Section, "Max Cart Slope", 25f, new ConfigDescription("Steepest slope (degrees) the slope easing leaves between neighbouring terrain points.", new AcceptableValueRange<float>(5f, 45f)));
		rampNeedsShift = mod.config(Section, "Ramp Only With Shift", Toggle.On, "If on, slope ramping only happens on strikes made while holding Shift. Off ramps on every level ground/path/paved strike (as in SnakeSkills 1.2 and 1.3).");
		rampMode = mod.config(Section, "Ramp Mode", RampMode.Slope, "Slope: the strike shapes the brush into one even ramp. If the ground is gentle enough it follows the ground's own slope; if it is too steep, the ramp runs across the hill at Max Cart Slope (like a road winding up a hillside) in the direction closest to where you are looking, instead of straight up the steepest part. Relax: the older behaviour (1.2-1.4), which only evens out steps steeper than Max Cart Slope between neighbouring points.");
		rampStyle = mod.config(Section, "Shift Ramp Style", RampStyle.Spread, "Which ground a ramp may move. Spread: cut the high side and fill the low side (best for paths along a hillside). Fill: only adds ground, so a ramp is built up against a cliff or step. Cut: only removes ground. Terrain can be raised or dug at most 8 m from how the world generated it. (1.4 used Fill, 1.2-1.3 Spread.)");

		smarterHoeLoaded = Chainloader.PluginInfos.ContainsKey(SmarterHoeGUID);
		if (smarterHoeLoaded)
		{
			Log("SmarterHoe (or SnakeSmarterHoe) is installed, so SnakeSkills leaves the hoe alone. The Landscaping skill still shows but doesn't change the hoe. Remove SmarterHoe to use it.");
		}
	}

	internal static bool Active => enabled.Value == Toggle.On && !smarterHoeLoaded;

	internal static bool HoldingHoe(Player player)
	{
		ItemDrop.ItemData? item = player.GetRightItem();
		return item != null && ((item.m_dropPrefab != null && item.m_dropPrefab.name == "Hoe") || item.m_shared.m_name == "$item_hoe");
	}

	private static bool HoeBuildMode(Player player) => player.InPlaceMode() && HoldingHoe(player);

	private static float MaxScale(Player player) => Mathf.Lerp(1f, maxBrushScale.Value, player.GetSkillFactor("Landscaping"));

	internal static float BrushScale(Player player) => brushSize.Value <= 0f ? MaxScale(player) : Mathf.Clamp(brushSize.Value, 1f, MaxScale(player));

	// Left Alt + wheel sizes the brush; the same scroll must not also zoom the camera or turn the piece.
	private static bool AdjustingBrush => Active && sizeWithWheel.Value == Toggle.On && Player.m_localPlayer is { } player && HoeBuildMode(player) && Input.GetKey(KeyCode.LeftAlt);

	[HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateCamera))]
	private static class NoZoomWhileSizing
	{
		private static void Prefix(GameCamera __instance, out float __state) => __state = __instance.m_distance;

		private static void Postfix(GameCamera __instance, float __state)
		{
			if (AdjustingBrush)
			{
				__instance.m_distance = __state;
			}
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacement))]
	private static class NoRotateWhileSizing
	{
		private static void Prefix(Player __instance, out int __state) => __state = __instance.m_placeRotation;

		private static void Postfix(Player __instance, int __state)
		{
			if (__instance == Player.m_localPlayer && AdjustingBrush)
			{
				__instance.m_placeRotation = __state;
			}
		}
	}

	internal static void Update()
	{
		if (!Active || sizeWithWheel.Value == Toggle.Off || Player.m_localPlayer is not { } player || !HoeBuildMode(player))
		{
			return;
		}

		// Middle click: back to the default size (the biggest the skill allows, growing with it).
		if (Input.GetMouseButtonDown(2))
		{
			brushSize.Value = 0f;
			player.Message(MessageHud.MessageType.TopLeft, $"Hoe brush: reset to the biggest your skill allows ({MaxScale(player):0.0}x)");
			return;
		}

		if (!Input.GetKey(KeyCode.LeftAlt))
		{
			return;
		}

		float wheel = Input.mouseScrollDelta.y;
		if (Mathf.Abs(wheel) < 0.01f)
		{
			return;
		}

		float max = MaxScale(player);
		if (max <= 1.001f)
		{
			player.Message(MessageHud.MessageType.TopLeft, "Hoe brush: vanilla size (Landscaping makes it bigger)");
			return;
		}

		// Steps of a tenth of vanilla's radius.
		float scale = Mathf.Clamp(BrushScale(player) + Mathf.Sign(wheel) * 0.1f, 1f, max);
		brushSize.Value = Mathf.Round(scale * 10f) / 10f;
		player.Message(MessageHud.MessageType.TopLeft, $"Hoe brush: {scale:0.0}x (max {max:0.0}x)");
	}

	// Middle click is vanilla's "remove piece"; with the hoe out it resets the brush instead.
	[HarmonyPatch(typeof(Player), nameof(Player.RemovePiece))]
	private static class NoRemoveWithHoe
	{
		private static bool Prefix(Player __instance) => !(Active && sizeWithWheel.Value == Toggle.On && __instance == Player.m_localPlayer && HoldingHoe(__instance));
	}

	private const string HintMarker = "<color=#c0c0c0>";

	// Control hints under each hoe tool's description in the build menu (the description is
	// localized as a whole, so the vanilla token in front still works).
	internal static void AddHints(PieceTable table)
	{
		if (!Active)
		{
			return;
		}
		foreach (GameObject go in table.m_pieces)
		{
			if (go == null || go.GetComponent<Piece>() is not { } piece || (piece.m_description ?? "").Contains(HintMarker))
			{
				continue;
			}
			TerrainOp.Settings? s = go.GetComponent<TerrainOp>()?.m_settings;
			bool restore = RestoreTerrain.IsRestorePiece(go);
			if (s == null && !restore)
			{
				continue;
			}

			List<string> lines = new();
			if (sizeWithWheel.Value == Toggle.On)
			{
				lines.Add("Left Alt + mouse wheel: brush size (bigger with Landscaping)");
				lines.Add("Middle click: reset brush size");
			}
			if (s != null && !s.m_raise && (s.m_level ? easeLevel.Value : easePaths.Value) == Toggle.On && (s.m_level || s.m_smooth || s.m_paintCleared))
			{
				lines.Add(rampNeedsShift.Value == Toggle.On ? "Hold Shift: shape into a cart ramp (faces where you look)" : "Shapes into a cart ramp (faces where you look)");
			}
			if (s != null && !s.m_raise && flattenBumps.Value == Toggle.On && (s.m_level || s.m_smooth || s.m_paintCleared))
			{
				lines.Add("Knocks down lone bumps");
			}
			if (lines.Count > 0)
			{
				piece.m_description = (piece.m_description ?? "") + "\n\n" + HintMarker + string.Join("\n", lines) + "</color>";
			}
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.CheckRun))]
	private static class NoSprintWithHoe
	{
		private static bool Prefix(Player __instance, ref bool __result)
		{
			if (Active && noSprint.Value == Toggle.On && __instance == Player.m_localPlayer && HoeBuildMode(__instance))
			{
				__result = false;
				return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.GetBuildStamina))]
	private static class LessStamina
	{
		private static void Postfix(Player __instance, ref float __result)
		{
			if (Active && HoldingHoe(__instance))
			{
				__result *= 1f - staminaReduction.Value * __instance.GetSkillFactor("Landscaping");
			}
		}
	}

	// The strike being applied, between TerrainOp.Awake's prefix and postfix.
	private class Strike
	{
		public Vector3 center;
		public float radius;
		public bool level;
		public bool path;
		public TerrainZones.Ramp? ramp;
	}

	[HarmonyPatch(typeof(TerrainOp), nameof(TerrainOp.Awake))]
	private static class HoeStrike
	{
		private static void Prefix(TerrainOp __instance, out Strike? __state)
		{
			__state = null;
			if (!Active || TerrainOp.m_forceDisableTerrainOps || Player.m_localPlayer is not { } player || !HoldingHoe(player) || player.m_buildPieces == null)
			{
				return;
			}

			// Only the pieces of the hoe's own build table, placed near us (not some other mod's op).
			string prefab = Utils.GetPrefabName(__instance.gameObject);
			if (!player.m_buildPieces.m_pieces.Exists(p => p != null && p.name == prefab) || Vector3.Distance(__instance.transform.position, player.transform.position) > 20f)
			{
				return;
			}

			TerrainOp.Settings s = __instance.m_settings;
			float scale = BrushScale(player);
			if (scale > 1.001f)
			{
				// A copy: the instance's settings object may be shared with the prefab's.
				__instance.m_settings = new TerrainOp.Settings
				{
					m_levelOffset = s.m_levelOffset,
					m_level = s.m_level,
					m_levelRadius = s.m_levelRadius * scale,
					m_square = s.m_square,
					m_raise = s.m_raise,
					m_raiseRadius = s.m_raiseRadius * scale,
					m_raiseDelta = s.m_raiseDelta,
					m_raisePower = s.m_raisePower,
					m_smooth = s.m_smooth,
					m_smoothRadius = s.m_smoothRadius * scale,
					m_smoothPower = s.m_smoothPower,
					m_paintCleared = s.m_paintCleared,
					m_paintHeightCheck = s.m_paintHeightCheck,
					m_paintType = s.m_paintType,
					m_paintRadius = s.m_paintRadius * scale,
					m_paintStrength = s.m_paintStrength,
					m_paintExp = s.m_paintExp,
					m_paintCurve = s.m_paintCurve,
					m_rotation = s.m_rotation,
					m_sides = s.m_sides,
					m_addMedianMax = s.m_addMedianMax,
					m_centerMultiplicationFactor = s.m_centerMultiplicationFactor,
					m_pointMultiplicationFactor = s.m_pointMultiplicationFactor,
					m_halfOffset = s.m_halfOffset,
				};
				s = __instance.m_settings;
			}

			float radius = s.GetRadius();
			TerrainZones.Claim(__instance.transform.position, radius);
			player.RaiseSkill("Landscaping");

			// Raise is left alone: it builds up on purpose. Level ground levels; path and paved road
			// smooth or paint without raising.
			__state = new Strike
			{
				center = __instance.transform.position,
				radius = s.m_level ? s.m_levelRadius : Mathf.Max(s.m_smooth ? s.m_smoothRadius : 0f, s.m_paintRadius),
				level = s.m_level && !s.m_raise,
				path = !s.m_level && !s.m_raise && (s.m_smooth || s.m_paintCleared),
			};

			// The ramp's slope is read from the ground before the strike changes it.
			if (WantsRamp(__state) && rampMode.Value == RampMode.Slope)
			{
				Vector3 facing = GameCamera.instance != null ? GameCamera.instance.transform.forward : player.transform.forward;
				__state.ramp = TerrainZones.PlanRamp(__state.center, __state.radius, maxSlope.Value, facing, rampStyle.Value);
			}
		}

		private static bool WantsRamp(Strike strike)
		{
			bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
			return (shift || rampNeedsShift.Value == Toggle.Off) && (strike.path ? easePaths.Value == Toggle.On : strike.level && easeLevel.Value == Toggle.On);
		}

		private static void Postfix(Strike? __state)
		{
			if (__state == null || (!__state.level && !__state.path) || __state.radius < 0.5f)
			{
				return;
			}

			bool bumps = flattenBumps.Value == Toggle.On;
			// Ramping only on request by default: Shift (which no longer sprints with the hoe out).
			bool ease = WantsRamp(__state);
			bool relax = ease && rampMode.Value == RampMode.Relax;
			if (bumps || relax || __state.ramp != null)
			{
				TerrainZones.Tidy(__state.center, __state.radius, bumps ? bumpHeight.Value : -1f, relax ? maxSlope.Value : -1f, relax && __state.level, rampStyle.Value, __state.ramp);
			}
		}
	}

	// Ghost: show the brush at the size it will strike.
	[HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
	private static class GhostSize
	{
		private static readonly Dictionary<int, float> baseSizes = new();

		private static void Postfix(Player __instance)
		{
			if (!Active || __instance != Player.m_localPlayer || __instance.m_placementGhost is not { } ghost || !HoldingHoe(__instance) || (ghost.GetComponentInChildren<TerrainOp>(true) == null && !RestoreTerrain.IsRestorePiece(ghost)))
			{
				return;
			}

			float scale = BrushScale(__instance);
			foreach (CircleProjector circle in ghost.GetComponentsInChildren<CircleProjector>(true))
			{
				circle.m_radius = BaseSize(circle, circle.m_radius) * scale;
			}
			foreach (Projector projector in ghost.GetComponentsInChildren<Projector>(true))
			{
				projector.orthographicSize = BaseSize(projector, projector.orthographicSize) * scale;
			}
			if (FindChild(ghost.transform, "_GhostOnly") is { } ghostOnly)
			{
				ghostOnly.localScale = Vector3.one * (BaseSize(ghostOnly, ghostOnly.localScale.x) * scale);
			}
		}

		private static float BaseSize(Object o, float current)
		{
			int id = o.GetInstanceID();
			if (!baseSizes.TryGetValue(id, out float size))
			{
				baseSizes[id] = size = current;
				if (baseSizes.Count > 512)
				{
					baseSizes.Clear();
					baseSizes[id] = size;
				}
			}
			return size;
		}

		private static Transform? FindChild(Transform parent, string name)
		{
			foreach (Transform child in parent)
			{
				if (child.name == name)
				{
					return child;
				}
				if (FindChild(child, name) is { } found)
				{
					return found;
				}
			}
			return null;
		}
	}
}

// Terrain helpers for Landscaping: zone ownership, and the bump/slope pass on the result of a strike.
internal static class TerrainZones
{
	internal static readonly List<Heightmap> heightmaps = new();

	public static void Claim(Vector3 center, float radius)
	{
		heightmaps.Clear();
		Heightmap.FindHeightmap(center, radius + 2f, heightmaps);
		foreach (Heightmap hmap in heightmaps)
		{
			TerrainComp comp = hmap.GetAndCreateTerrainCompiler();
			if (comp != null && comp.m_nview != null && comp.m_nview.IsValid() && !comp.m_nview.IsOwner())
			{
				// Take in the previous owner's last edits before building on them.
				if (comp.m_initialized)
				{
					comp.CheckLoad();
				}
				comp.m_nview.ClaimOwnership();
			}
		}
	}

	// One terrain vertex in world grid coordinates, and every heightmap that holds it (zone edges are
	// shared by two or four heightmaps, which must all get the same height).
	private class Point
	{
		public float height;
		public bool inside;
		public readonly List<(Heightmap hmap, TerrainComp comp, int x, int y)> owners = new();
	}

	public static void Tidy(Vector3 center, float radius, float bumpHeight, float maxSlopeDegrees, bool levelEdgesOnly, RampStyle style, Ramp? ramp)
	{
		heightmaps.Clear();
		Heightmap.FindHeightmap(center, radius + 2f, heightmaps);
		if (heightmaps.Count == 0)
		{
			return;
		}

		float scale = heightmaps[0].m_scale;
		Dictionary<(int, int), Point> grid = new();
		foreach (Heightmap hmap in heightmaps)
		{
			TerrainComp comp = hmap.GetAndCreateTerrainCompiler();
			if (comp == null || !comp.m_initialized || !comp.IsOwner() || comp.m_levelDelta == null)
			{
				// Without ownership our change wouldn't reach anyone; leave this zone as it is.
				return;
			}

			// The strike's changes are in the deltas but not yet in the heights: rebuild now.
			hmap.Poke(0, false);
			Vector3 origin = hmap.transform.position;
			int half = hmap.m_width / 2;
			for (int y = 0; y <= hmap.m_width; y++)
			{
				float wz = origin.z + (y - half) * scale;
				if (Mathf.Abs(wz - center.z) > radius + scale * 1.5f)
				{
					continue;
				}
				for (int x = 0; x <= hmap.m_width; x++)
				{
					float wx = origin.x + (x - half) * scale;
					float dx = wx - center.x;
					float dz = wz - center.z;
					float dist = Mathf.Sqrt(dx * dx + dz * dz);
					if (dist > radius + scale * 1.5f)
					{
						continue;
					}

					(int, int) key = (Mathf.RoundToInt(wx / scale), Mathf.RoundToInt(wz / scale));
					if (!grid.TryGetValue(key, out Point point))
					{
						grid[key] = point = new Point { height = origin.y + hmap.GetHeight(x, y), inside = dist <= radius };
					}
					point.owners.Add((hmap, comp, x, y));
				}
			}
		}

		Dictionary<(int, int), float> start = new();
		foreach (KeyValuePair<(int, int), Point> p in grid)
		{
			start[p.Key] = p.Value.height;
		}

		if (bumpHeight > 0f)
		{
			FlattenBumps(grid, bumpHeight);
		}
		if (ramp != null)
		{
			ApplyRamp(grid, scale, center, radius, ramp);
		}
		if (maxSlopeDegrees > 0f)
		{
			EaseSlopes(grid, scale * Mathf.Tan(maxSlopeDegrees * Mathf.Deg2Rad), levelEdgesOnly, style);
		}

		HashSet<Heightmap> changed = new();
		foreach (KeyValuePair<(int, int), Point> p in grid)
		{
			float diff = p.Value.height - start[p.Key];
			if (!p.Value.inside || Mathf.Abs(diff) < 0.001f)
			{
				continue;
			}
			foreach ((Heightmap hmap, TerrainComp comp, int x, int y) in p.Value.owners)
			{
				// Same bookkeeping as vanilla's LevelTerrain: fold the smoothing into the level delta.
				int i = y * (comp.m_width + 1) + x;
				comp.m_levelDelta[i] = Mathf.Clamp(comp.m_levelDelta[i] + comp.m_smoothDelta[i] + diff, -8f, 8f);
				comp.m_smoothDelta[i] = 0f;
				comp.m_modifiedHeight[i] = true;
				changed.Add(hmap);
			}
		}

		foreach (Heightmap hmap in changed)
		{
			hmap.GetAndCreateTerrainCompiler().Save(false);
			hmap.Poke(0, false);
		}
	}

	// A planned ramp: an even slope through the brush centre, rising `slope` metres per metre along
	// `dir` and level across it.
	internal class Ramp
	{
		public float baseHeight;
		public Vector2 dir;
		public float slope;
		public RampStyle style;
	}

	// Fits a plane to the ground in the brush (least squares). If it is no steeper than the cart
	// slope, the ramp follows it. If it is steeper, the ramp runs across the hill: of the two
	// directions where the ground rises at exactly the cart slope, the one closer to where the
	// player looks (either way along it). The ground rises along d at |G|·cos(angle between d and G),
	// so that angle is acos(maxSlope / |G|).
	public static Ramp? PlanRamp(Vector3 center, float radius, float maxSlopeDegrees, Vector3 facing, RampStyle style)
	{
		heightmaps.Clear();
		Heightmap.FindHeightmap(center, radius + 2f, heightmaps);
		double n = 0, sx = 0, sz = 0, sh = 0, sxx = 0, szz = 0, sxz = 0, sxh = 0, szh = 0;
		foreach (Heightmap hmap in heightmaps)
		{
			Vector3 origin = hmap.transform.position;
			int half = hmap.m_width / 2;
			for (int y = 0; y <= hmap.m_width; y++)
			{
				float dz = origin.z + (y - half) * hmap.m_scale - center.z;
				if (Mathf.Abs(dz) > radius)
				{
					continue;
				}
				for (int x = 0; x <= hmap.m_width; x++)
				{
					float dx = origin.x + (x - half) * hmap.m_scale - center.x;
					if (dx * dx + dz * dz > radius * radius)
					{
						continue;
					}
					float h = origin.y + hmap.GetHeight(x, y);
					n++;
					sx += dx;
					sz += dz;
					sh += h;
					sxx += dx * dx;
					szz += dz * dz;
					sxz += dx * dz;
					sxh += dx * h;
					szh += dz * h;
				}
			}
		}
		if (n < 6)
		{
			return null;
		}

		// Centred sums, then the 2x2 normal equations for the gradient.
		double mx = sx / n, mz = sz / n, mh = sh / n;
		double cxx = sxx / n - mx * mx, czz = szz / n - mz * mz, cxz = sxz / n - mx * mz;
		double cxh = sxh / n - mx * mh, czh = szh / n - mz * mh;
		double det = cxx * czz - cxz * cxz;
		if (System.Math.Abs(det) < 1e-9)
		{
			return null;
		}
		Vector2 g = new((float)((cxh * czz - czh * cxz) / det), (float)((czh * cxx - cxh * cxz) / det));
		float baseHeight = (float)(mh - g.x * mx - g.y * mz);

		float steep = g.magnitude;
		float max = Mathf.Tan(maxSlopeDegrees * Mathf.Deg2Rad);
		Ramp ramp = new() { baseHeight = baseHeight, style = style };
		if (steep < 0.001f)
		{
			ramp.dir = Vector2.right;
			ramp.slope = 0f;
		}
		else if (steep <= max)
		{
			ramp.dir = g / steep;
			ramp.slope = steep;
		}
		else
		{
			float angle = Mathf.Acos(max / steep) * Mathf.Rad2Deg;
			Vector2 up = g / steep;
			Vector2 a = Rotate(up, angle), b = Rotate(up, -angle);
			Vector2 look = new Vector2(facing.x, facing.z).normalized;
			ramp.dir = Mathf.Abs(Vector2.Dot(a, look)) >= Mathf.Abs(Vector2.Dot(b, look)) ? a : b;
			ramp.slope = max;
		}
		return ramp;
	}

	private static Vector2 Rotate(Vector2 v, float degrees)
	{
		float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
		return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
	}

	// Moves the points inside the brush onto the ramp, fully in the inner 60% of the radius and
	// fading out to the edge so the ramp blends into the ground around it.
	private static void ApplyRamp(Dictionary<(int, int), Point> grid, float scale, Vector3 center, float radius, Ramp ramp)
	{
		foreach (KeyValuePair<(int, int), Point> p in grid)
		{
			if (!p.Value.inside)
			{
				continue;
			}
			float dx = p.Key.Item1 * scale - center.x, dz = p.Key.Item2 * scale - center.z;
			float dist = Mathf.Sqrt(dx * dx + dz * dz);
			float weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius * 0.6f, radius, dist));
			float target = ramp.baseHeight + ramp.slope * (ramp.dir.x * dx + ramp.dir.y * dz);
			float moved = Mathf.Lerp(p.Value.height, target, weight);
			p.Value.height = ramp.style switch
			{
				RampStyle.Fill => Mathf.Max(p.Value.height, moved),
				RampStyle.Cut => Mathf.Min(p.Value.height, moved),
				_ => moved,
			};
		}
	}

	private static readonly (int, int)[] around8 = { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) };
	private static readonly (int, int)[] around4 = { (0, -1), (-1, 0), (1, 0), (0, 1) };

	// A point higher than all eight neighbours by more than bumpHeight drops to their average.
	private static void FlattenBumps(Dictionary<(int, int), Point> grid, float bumpHeight)
	{
		for (int pass = 0; pass < 2; pass++)
		{
			foreach (KeyValuePair<(int, int), Point> p in grid)
			{
				if (!p.Value.inside)
				{
					continue;
				}

				float max = float.MinValue;
				float sum = 0f;
				int count = 0;
				foreach ((int dx, int dz) in around8)
				{
					if (grid.TryGetValue((p.Key.Item1 + dx, p.Key.Item2 + dz), out Point n))
					{
						max = Mathf.Max(max, n.height);
						sum += n.height;
						count++;
					}
				}

				if (count == 8 && p.Value.height - max > bumpHeight)
				{
					p.Value.height = sum / count;
				}
			}
		}
	}

	// Spreads any step steeper than maxStep (height per grid point) over the points inside the brush,
	// so a steep bit becomes an even ramp. Points outside the brush don't move, so the ramp meets the
	// ground around it. levelEdgesOnly: only the rim next to the outside is eased (level ground keeps
	// its flat middle; the easing then works inward from the edge). Fill only ever raises a point
	// (to the higher neighbour minus maxStep), so a cliff in the brush gets a ramp built up against
	// it; Cut only lowers; Spread moves both sides halfway.
	private static void EaseSlopes(Dictionary<(int, int), Point> grid, float maxStep, bool levelEdgesOnly, RampStyle style)
	{
		List<KeyValuePair<(int, int), Point>> points = new(grid);
		for (int iteration = 0; iteration < 80; iteration++)
		{
			bool moved = false;
			foreach (KeyValuePair<(int, int), Point> p in points)
			{
				if (!p.Value.inside)
				{
					continue;
				}
				foreach ((int dx, int dz) in around4)
				{
					if (!grid.TryGetValue((p.Key.Item1 + dx, p.Key.Item2 + dz), out Point n) || (levelEdgesOnly && n.inside && iteration == 0))
					{
						continue;
					}

					float step = p.Value.height - n.height;
					float excess = Mathf.Abs(step) - maxStep;
					if (excess <= 0.005f)
					{
						continue;
					}

					float sign = Mathf.Sign(step);
					if (style != RampStyle.Spread)
					{
						// Fill raises the lower of the two, Cut lowers the higher, and only points inside move.
						Point low = step < 0 ? p.Value : n, high = step < 0 ? n : p.Value;
						if (style == RampStyle.Fill && low.inside)
						{
							low.height = high.height - maxStep;
						}
						else if (style == RampStyle.Cut && high.inside)
						{
							high.height = low.height + maxStep;
						}
						else
						{
							continue;
						}
					}
					else if (n.inside)
					{
						p.Value.height -= sign * excess * 0.5f;
						n.height += sign * excess * 0.5f;
					}
					else
					{
						p.Value.height -= sign * excess;
					}
					moved = true;
				}
			}
			if (!moved)
			{
				break;
			}
		}
	}
}
