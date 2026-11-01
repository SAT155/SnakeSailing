using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SnakeSkills;

// The two hoe ramp tools on modifier keys (SnakeSkills 1.14):
// - Ctrl (Incline): a preview of an even incline from the player's feet up the way they look; the
//   mouse wheel sets its angle. A Ctrl strike shapes the brush onto that incline. The incline
//   follows the player until the first Ctrl strike, then stays put while Ctrl is held, so strikes
//   made while walking up it join into one straight ramp.
// - Alt (RampBlueprints): Alt+click lays ramp points, Alt+click on the last point saves them as a
//   shared blueprint stored on the zones' TerrainComp ZDOs (like SnakeSmarterHoe's, but its own key
//   and format). Alt+click inside a blueprint is a strike that shapes the brush onto it. Built parts
//   turn green instead of disappearing; the blueprint clears itself once every part is built.
internal static class Incline
{
	private static bool held;
	private static bool locked;
	private static Vector3 anchor;
	private static Vector2 dir = Vector2.up;

	internal static bool Held => held;

	internal static bool CtrlDown => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

	internal static void Reset()
	{
		held = false;
		locked = false;
	}

	internal static void Update(Player player, bool enabled, ref float angle, float radius)
	{
		if (!enabled || !CtrlDown || !Landscaping.TerrainPieceInHand(player, out _))
		{
			Reset();
			return;
		}

		if (!held)
		{
			held = true;
			locked = false;
			player.Message(MessageHud.MessageType.TopLeft, $"Incline {angle:0}° (mouse wheel changes it)");
		}
		if (!locked)
		{
			anchor = player.transform.position;
			Vector3 look = GameCamera.instance != null ? GameCamera.instance.transform.forward : player.transform.forward;
			Vector2 flat = new(look.x, look.z);
			if (flat.sqrMagnitude > 0.0001f)
			{
				dir = flat.normalized;
			}
		}

		float wheel = Input.mouseScrollDelta.y;
		if (Mathf.Abs(wheel) > 0.01f)
		{
			angle = Mathf.Clamp(Mathf.Round(angle + Mathf.Sign(wheel)), -45f, 45f);
			player.Message(MessageHud.MessageType.TopLeft, $"Incline {angle:0}°");
		}

		// Preview: centre line and both edges of the incline, with cross bars every 2 m.
		float slope = Mathf.Tan(angle * Mathf.Deg2Rad);
		float reach = 6f;
		if (player.m_placementGhost != null)
		{
			Vector3 g = player.m_placementGhost.transform.position;
			reach = Mathf.Clamp(Vector2.Dot(new Vector2(g.x - anchor.x, g.z - anchor.z), dir) + radius + 2f, 4f, 40f);
		}
		Vector3 forward = new(dir.x, 0f, dir.y);
		Vector3 side = new(-dir.y, 0f, dir.x);
		Color color = new(0.35f, 0.85f, 1f, 0.9f);
		foreach (float offset in new[] { -radius, 0f, radius })
		{
			List<Vector3> line = new();
			for (float t = 0f; t <= reach + 0.01f; t += 1f)
			{
				line.Add(anchor + forward * t + side * offset + Vector3.up * (slope * t + 0.08f));
			}
			Lines.Draw(line, color, offset == 0f ? 0.08f : 0.05f);
		}
		for (float t = 2f; t <= reach + 0.01f; t += 2f)
		{
			Vector3 mid = anchor + forward * t + Vector3.up * (slope * t + 0.08f);
			Lines.Draw(new List<Vector3> { mid - side * radius, mid + side * radius }, color with { a = 0.5f }, 0.03f);
		}
	}

	// The incline as a ramp through the brush centre, for the strike's terrain pass.
	internal static TerrainZones.Ramp RampAt(Vector3 center, float angle, RampStyle style)
	{
		locked = true;
		float slope = Mathf.Tan(angle * Mathf.Deg2Rad);
		float along = Vector2.Dot(new Vector2(center.x - anchor.x, center.z - anchor.z), dir);
		return new TerrainZones.Ramp { baseHeight = anchor.y + slope * along, dir = dir, slope = slope, style = style };
	}
}

internal static class RampBlueprints
{
	private const int FormatVersion = 1;
	private const float BuiltTolerance = 0.3f;
	private const int MaxZones = 6;
	private static readonly int zdoKey = "SnakeSkills_RampBlueprints".GetStableHashCode();

	internal class Plan
	{
		public string Id = "";
		public long AuthorId;
		public string AuthorName = "";
		public List<Vector3> Points = new();
		public float HalfWidth = 2f;

		// Filled in by a scan: points every metre along the ramp at their planned height, and which are built.
		public List<Vector3> Samples = new();
		public bool[] Built = Array.Empty<bool>();
		public bool FullyMeasured;

		public float Margin => Mathf.Clamp(HalfWidth * 0.5f, 1f, 3f);

		public float Length
		{
			get
			{
				float total = 0f;
				for (int i = 1; i < Points.Count; i++)
				{
					total += Flat(Points[i] - Points[i - 1]).magnitude;
				}
				return total;
			}
		}

		// Nearest point of the centre line: horizontal distance and planned height there.
		public (float dist, float height) Nearest(float x, float z)
		{
			Vector2 p = new(x, z);
			float best = float.MaxValue, height = Points.Count > 0 ? Points[0].y : 0f;
			for (int i = 1; i < Points.Count; i++)
			{
				Vector2 a = Flat(Points[i - 1]), b = Flat(Points[i]), ab = b - a;
				float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
				float d = Vector2.Distance(p, a + ab * t);
				if (d < best)
				{
					best = d;
					height = Mathf.Lerp(Points[i - 1].y, Points[i].y, t);
				}
			}
			return (best, height);
		}

		public bool Contains(Vector3 point) => Nearest(point.x, point.z).dist <= HalfWidth + Margin;

		// Target height and weight (1 on the ramp, easing to 0 across the margin) at a world point.
		public Vector2 TargetAt(float x, float z)
		{
			(float dist, float height) = Nearest(x, z);
			if (dist >= HalfWidth + Margin)
			{
				return Vector2.zero;
			}
			float weight = dist <= HalfWidth ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (dist - HalfWidth) / Margin);
			return new Vector2(height, weight);
		}

		public void Write(ZPackage pkg)
		{
			pkg.Write(Id);
			pkg.Write(AuthorId);
			pkg.Write(AuthorName);
			pkg.Write(HalfWidth);
			pkg.Write(Points.Count);
			foreach (Vector3 p in Points)
			{
				pkg.Write(p);
			}
		}

		public static Plan Read(ZPackage pkg)
		{
			Plan plan = new() { Id = pkg.ReadString(), AuthorId = pkg.ReadLong(), AuthorName = pkg.ReadString(), HalfWidth = pkg.ReadSingle() };
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				plan.Points.Add(pkg.ReadVector3());
			}
			return plan;
		}
	}

	private static Vector2 Flat(Vector3 v) => new(v.x, v.z);

	private static readonly Dictionary<string, Plan> known = new();
	private static readonly List<Vector3> pending = new();
	private static readonly HashSet<string> checkAfterStrike = new();
	private static float nextScan;

	// The blueprint the current Alt strike builds (set between TryPlacePiece's prefix and finalizer).
	internal static Plan? Building;

	internal static bool AltDown => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

	internal static void Update(Player player, bool enabled, float maxLength)
	{
		if (!enabled)
		{
			return;
		}

		Refresh(false);
		Vector3? ghost = player.m_placementGhost != null ? player.m_placementGhost.transform.position : null;

		if (Input.GetKeyDown(KeyCode.Backspace) && !TypingSomewhere())
		{
			if (pending.Count > 0)
			{
				pending.RemoveAt(pending.Count - 1);
				player.Message(MessageHud.MessageType.TopLeft, pending.Count > 0 ? $"Ramp point removed ({pending.Count} left)" : "Ramp points cleared");
			}
			else if (AltDown && ghost is { } g && FindAt(g) is { } plan)
			{
				RemoveByPlayer(player, plan);
			}
		}

		// Blueprints near the player, built parts green and the rest amber.
		Vector3 me = player.transform.position;
		foreach (Plan plan in known.Values)
		{
			if (plan.Samples.Count >= 2 && Vector3.Distance(plan.Samples[0], me) < 150f + plan.Length)
			{
				DrawPlan(plan);
			}
		}

		// Points laid so far, plus where the next one would go while Alt is held.
		if (pending.Count > 0)
		{
			List<Vector3> line = new();
			foreach (Vector3 p in pending)
			{
				line.Add(p + Vector3.up * 0.15f);
			}
			if (AltDown && ghost is { } g)
			{
				line.Add(g + Vector3.up * 0.15f);
			}
			Color cyan = new(0.35f, 0.85f, 1f, 0.9f);
			if (line.Count >= 2)
			{
				Lines.Draw(line, cyan, 0.1f);
			}
			foreach (Vector3 p in pending)
			{
				Lines.Draw(new List<Vector3> { p, p + Vector3.up * 1.5f }, cyan, 0.08f);
			}
		}
	}

	private static bool TypingSomewhere()
	{
		return (Chat.instance != null && Chat.instance.HasFocus()) || global::Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible();
	}

	private static void DrawPlan(Plan plan)
	{
		Color done = new(0.3f, 0.95f, 0.35f, 0.9f), todo = new(1f, 0.7f, 0.2f, 0.9f);
		int i = 0;
		while (i < plan.Samples.Count - 1)
		{
			bool built = plan.Built.Length == plan.Samples.Count && plan.Built[i] && plan.Built[i + 1];
			int end = i + 1;
			while (end < plan.Samples.Count - 1 && (plan.Built.Length == plan.Samples.Count && plan.Built[end] && plan.Built[end + 1]) == built)
			{
				end++;
			}
			foreach (float offset in new[] { -plan.HalfWidth, 0f, plan.HalfWidth })
			{
				List<Vector3> line = new();
				for (int k = i; k <= end; k++)
				{
					Vector3 a = plan.Samples[Mathf.Max(0, k - 1)], b = plan.Samples[Mathf.Min(plan.Samples.Count - 1, k + 1)];
					Vector2 along = Flat(b - a).normalized;
					line.Add(plan.Samples[k] + new Vector3(-along.y, 0f, along.x) * offset + Vector3.up * 0.15f);
				}
				Lines.Draw(line, built ? done : todo, offset == 0f ? 0.1f : 0.06f);
			}
			i = end;
		}
	}

	// Alt+click that isn't on a blueprint (or while points are being laid): add a point, or save.
	internal static void Click(Player player, Vector3 pos, float halfWidth, float maxLength)
	{
		if (pending.Count > 0 && Flat(pos - pending[pending.Count - 1]).magnitude < 1f)
		{
			if (pending.Count >= 2)
			{
				Save(player, halfWidth);
			}
			else
			{
				player.Message(MessageHud.MessageType.TopLeft, "Alt+click a second point further along the ramp");
			}
			return;
		}

		float length = 0f;
		for (int i = 1; i < pending.Count; i++)
		{
			length += Flat(pending[i] - pending[i - 1]).magnitude;
		}
		if (pending.Count > 0)
		{
			length += Flat(pos - pending[pending.Count - 1]).magnitude;
		}
		if (length > maxLength)
		{
			player.Message(MessageHud.MessageType.TopLeft, $"A ramp blueprint can be at most {maxLength:0} m long. Alt+click the last point again to save it.");
			return;
		}

		pending.Add(pos);
		if (pending.Count == 1)
		{
			player.Message(MessageHud.MessageType.TopLeft, "Ramp start set. Alt+click the next point.");
			return;
		}
		Vector3 a = pending[pending.Count - 2];
		float run = Flat(pos - a).magnitude;
		float degrees = Mathf.Atan2(Mathf.Abs(pos.y - a.y), Mathf.Max(run, 0.01f)) * Mathf.Rad2Deg;
		player.Message(MessageHud.MessageType.TopLeft, $"Ramp point {pending.Count}: {degrees:0}° slope, {length:0} m. Alt+click it again to save, Backspace undoes.");
	}

	private static void Save(Player player, float halfWidth)
	{
		foreach (Vector3 p in pending)
		{
			if (!PrivateArea.CheckAccess(p, halfWidth))
			{
				return;
			}
		}
		Plan plan = new()
		{
			Id = Guid.NewGuid().ToString("N"),
			AuthorId = player.GetPlayerID(),
			AuthorName = player.GetPlayerName(),
			Points = new List<Vector3>(pending),
			HalfWidth = halfWidth,
		};
		if (!EditZones(plan, list => { list.RemoveAll(o => o.Id == plan.Id); list.Add(plan); }, out string error))
		{
			player.Message(MessageHud.MessageType.Center, "Ramp blueprint not saved: " + error);
			return;
		}
		pending.Clear();
		player.Message(MessageHud.MessageType.Center, $"Ramp blueprint saved ({plan.Length:0} m). Alt+click on it to build it.");
	}

	private static void RemoveByPlayer(Player player, Plan plan)
	{
		bool admin = ZNet.instance != null && ZNet.instance.LocalPlayerIsAdminOrHost();
		if (plan.AuthorId != player.GetPlayerID() && !admin)
		{
			player.Message(MessageHud.MessageType.TopLeft, $"Only {plan.AuthorName} or an admin can remove this ramp blueprint");
			return;
		}
		player.Message(MessageHud.MessageType.TopLeft, Remove(plan, out string error) ? "Ramp blueprint removed (the ground stays as it is)" : "Ramp blueprint not removed: " + error);
	}

	private static bool Remove(Plan plan, out string error) => EditZones(plan, list => list.RemoveAll(o => o.Id == plan.Id), out error);

	// The blueprint whose centre line is nearest to point, if point is on one.
	internal static Plan? FindAt(Vector3 point)
	{
		Refresh(false);
		Plan? best = null;
		float bestDist = float.MaxValue;
		foreach (Plan plan in known.Values)
		{
			float d = plan.Nearest(point.x, point.z).dist;
			if (d <= plan.HalfWidth + plan.Margin && d < bestDist)
			{
				best = plan;
				bestDist = d;
			}
		}
		return best;
	}

	internal static bool HasPending => pending.Count > 0;

	internal static void NoteStrike(Plan plan)
	{
		checkAfterStrike.Add(plan.Id);
		nextScan = 0f;
	}

	// Re-reads blueprints from loaded zones (once a second) and measures what is built.
	private static void Refresh(bool force)
	{
		if (!force && Time.time < nextScan)
		{
			return;
		}
		nextScan = Time.time + 1f;
		known.Clear();
		foreach (TerrainComp comp in TerrainComp.s_instances)
		{
			foreach (Plan plan in ReadZone(comp))
			{
				known[plan.Id] = plan;
			}
		}
		foreach (Plan plan in known.Values)
		{
			Measure(plan);
		}

		if (checkAfterStrike.Count == 0 || Player.m_localPlayer is not { } player)
		{
			return;
		}
		foreach (string id in checkAfterStrike)
		{
			if (!known.TryGetValue(id, out Plan plan) || plan.Built.Length == 0)
			{
				continue;
			}
			int built = 0;
			foreach (bool b in plan.Built)
			{
				built += b ? 1 : 0;
			}
			if (plan.FullyMeasured && built == plan.Built.Length && Remove(plan, out _))
			{
				player.Message(MessageHud.MessageType.Center, "Ramp finished! Blueprint cleared.");
			}
			else
			{
				player.Message(MessageHud.MessageType.TopLeft, $"Ramp {built * 100 / plan.Built.Length}% built");
			}
		}
		checkAfterStrike.Clear();
	}

	private static void Measure(Plan plan)
	{
		plan.Samples.Clear();
		for (int i = 1; i < plan.Points.Count; i++)
		{
			Vector3 a = plan.Points[i - 1], b = plan.Points[i];
			int steps = Mathf.Max(1, Mathf.CeilToInt(Flat(b - a).magnitude));
			for (int s = i == 1 ? 0 : 1; s <= steps; s++)
			{
				plan.Samples.Add(Vector3.Lerp(a, b, s / (float)steps));
			}
		}
		plan.Built = new bool[plan.Samples.Count];
		plan.FullyMeasured = true;
		for (int i = 0; i < plan.Samples.Count; i++)
		{
			if (!TryIsBuilt(plan.Samples[i], out bool built))
			{
				plan.FullyMeasured = false;
			}
			plan.Built[i] = built;
		}
	}

	// Whether the ground is at the planned height (or pushed as far as the 8 m limit allows toward
	// it); false with built = false when that terrain isn't loaded.
	private static bool TryIsBuilt(Vector3 target, out bool built)
	{
		built = false;
		Heightmap hmap = Heightmap.FindHeightmap(target);
		if (hmap == null)
		{
			return false;
		}
		hmap.WorldToVertex(target, out int x, out int y);
		float current = hmap.transform.position.y + hmap.GetHeight(x, y);
		if (Mathf.Abs(current - target.y) <= BuiltTolerance)
		{
			built = true;
			return true;
		}
		TerrainComp comp = TerrainComp.FindTerrainCompiler(target);
		if (comp != null && comp.m_initialized && comp.m_levelDelta != null)
		{
			int index = y * (comp.m_width + 1) + x;
			if (index >= 0 && index < comp.m_levelDelta.Length)
			{
				float delta = comp.m_levelDelta[index] + comp.m_smoothDelta[index];
				float needed = target.y - (current - delta);
				built = Mathf.Abs(delta) >= 7.9f && Mathf.Sign(delta) == Mathf.Sign(needed);
			}
		}
		return true;
	}

	// Applies an edit to the blueprint list of every zone the blueprint touches; all of them must be
	// loaded, and this client takes ownership of each so the change reaches everyone.
	private static bool EditZones(Plan plan, Action<List<Plan>> edit, out string error)
	{
		error = "";
		HashSet<Heightmap> zones = new();
		List<Heightmap> found = new();
		float reach = plan.HalfWidth + plan.Margin + 1f;
		for (int i = 0; i < plan.Points.Count; i++)
		{
			Vector3 a = plan.Points[i], b = plan.Points[Mathf.Min(i + 1, plan.Points.Count - 1)];
			int steps = Mathf.Max(1, Mathf.CeilToInt(Flat(b - a).magnitude / 4f));
			for (int s = 0; s <= steps; s++)
			{
				Vector3 p = Vector3.Lerp(a, b, s / (float)steps);
				if (Heightmap.FindHeightmap(p) == null)
				{
					error = "part of it is in terrain that isn't loaded. Stand closer to its middle.";
					return false;
				}
				found.Clear();
				Heightmap.FindHeightmap(p, reach, found);
				zones.UnionWith(found);
			}
		}
		if (zones.Count > MaxZones)
		{
			error = $"it crosses {zones.Count} terrain zones (at most {MaxZones}). Make it shorter.";
			return false;
		}

		List<TerrainComp> comps = new();
		foreach (Heightmap zone in zones)
		{
			TerrainComp comp = zone.GetAndCreateTerrainCompiler();
			if (comp == null || comp.m_nview == null || !comp.m_nview.IsValid())
			{
				error = "a terrain zone it touches isn't ready. Try again in a moment.";
				return false;
			}
			if (!comp.m_nview.IsOwner())
			{
				if (comp.m_initialized)
				{
					comp.CheckLoad();
				}
				comp.m_nview.ClaimOwnership();
			}
			comps.Add(comp);
		}
		foreach (TerrainComp comp in comps)
		{
			List<Plan> list = ReadZone(comp);
			edit(list);
			WriteZone(comp, list);
		}
		nextScan = 0f;
		return true;
	}

	private static List<Plan> ReadZone(TerrainComp comp)
	{
		List<Plan> result = new();
		if (comp == null || comp.m_nview == null || !comp.m_nview.IsValid())
		{
			return result;
		}
		byte[] bytes = comp.m_nview.GetZDO().GetByteArray(zdoKey, null);
		if (bytes == null || bytes.Length == 0)
		{
			return result;
		}
		try
		{
			ZPackage pkg = new(bytes);
			if (pkg.ReadInt() != FormatVersion)
			{
				return result;
			}
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				result.Add(Plan.Read(pkg));
			}
		}
		catch (Exception e)
		{
			SnakeSkillsPlugin.Log("Unreadable ramp blueprint data in a terrain zone: " + e.Message);
		}
		return result;
	}

	private static void WriteZone(TerrainComp comp, List<Plan> list)
	{
		ZDO zdo = comp.m_nview.GetZDO();
		if (list.Count == 0)
		{
			zdo.Set(zdoKey, Array.Empty<byte>());
			return;
		}
		ZPackage pkg = new();
		pkg.Write(FormatVersion);
		pkg.Write(list.Count);
		foreach (Plan plan in list)
		{
			plan.Write(pkg);
		}
		zdo.Set(zdoKey, pkg.GetArray());
	}

	// Alt+click with a hoe terrain piece: lays ramp points (no strike, no stamina) unless the brush is
	// on a blueprint and no points are being laid, in which case the strike goes ahead and builds it.
	[HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
	private static class AltClicks
	{
		private static bool Prefix(Player __instance, Piece piece, ref bool __result)
		{
			Building = null;
			if (!Landscaping.BlueprintsOn || __instance != Player.m_localPlayer || !AltDown || piece == null || !Landscaping.TerrainPieceInHand(__instance, out _) || __instance.m_placementGhost == null)
			{
				return true;
			}

			Vector3 pos = __instance.m_placementGhost.transform.position;
			if (pending.Count == 0 && FindAt(pos) is { } plan)
			{
				Building = plan;
				return true;
			}

			__result = false;
			// Holding the button repeats placement; only a fresh press lays a point.
			if (Input.GetMouseButtonDown(0))
			{
				Click(__instance, pos, Landscaping.BrushRadius(__instance, piece), Landscaping.BlueprintMaxLength);
			}
			return false;
		}

		private static void Finalizer()
		{
			if (Building != null)
			{
				NoteStrike(Building);
			}
			Building = null;
		}
	}
}

// A small pool of world-space lines, redrawn every frame: Begin, Draw..., End hides the rest.
internal static class Lines
{
	private static readonly List<LineRenderer> pool = new();
	private static Material? material;
	private static int used;

	internal static void Begin() => used = 0;

	internal static void Draw(List<Vector3> points, Color color, float width)
	{
		if (points.Count < 2)
		{
			return;
		}
		if (used == pool.Count)
		{
			GameObject go = new("SnakeSkills_Line");
			Object.DontDestroyOnLoad(go);
			LineRenderer line = go.AddComponent<LineRenderer>();
			material ??= new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
			line.sharedMaterial = material;
			line.useWorldSpace = true;
			line.numCapVertices = 2;
			line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			line.receiveShadows = false;
			pool.Add(line);
		}
		LineRenderer lr = pool[used++];
		if (lr == null)
		{
			return;
		}
		lr.enabled = true;
		lr.startColor = lr.endColor = color;
		lr.startWidth = lr.endWidth = width;
		lr.positionCount = points.Count;
		lr.SetPositions(points.ToArray());
	}

	internal static void End()
	{
		for (int i = used; i < pool.Count; i++)
		{
			if (pool[i] != null && pool[i].enabled)
			{
				pool[i].enabled = false;
			}
		}
	}
}
