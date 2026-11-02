using System.Collections.Generic;
using UnityEngine;

namespace SnakeSkills;

// Ctrl+Z / Ctrl+Y for the hoe (SnakeSkills 1.16). Before each hoe strike (vanilla tools, Ctrl incline,
// Alt blueprint strikes, Restore terrain) the TerrainComp data of every vertex in reach is saved:
// level and smooth delta, the modified flag and paint. Undo writes those back and saves the zone
// (after claiming it, so the change reaches everyone). Strikes made while the mouse button stays
// held are one step. Only the local player's own strikes are recorded; undoing puts the ground back
// as it was before the strike, which also undoes anything someone else did to the same spot since.
internal static class TerrainUndo
{
	private struct Vertex
	{
		public float level;
		public float smooth;
		public bool modifiedHeight;
		public Color paint;
		public bool modifiedPaint;
	}

	private class Step
	{
		public readonly Dictionary<(TerrainComp comp, int i), Vertex> vertices = new();
		public Vector3 center;
		public float radius;
		public float time;
	}

	private static readonly List<Step> undo = new();
	private static readonly List<Step> redo = new();
	private static readonly List<Heightmap> heightmaps = new();

	internal static int MaxSteps = 30;

	// Call before a strike changes the terrain around center (zones already claimed).
	internal static void Capture(Vector3 center, float radius)
	{
		Step? step = undo.Count > 0 ? undo[undo.Count - 1] : null;
		bool merge = step != null && !Input.GetMouseButtonDown(0) && Input.GetMouseButton(0) && Time.time - step.time < 1.5f;
		if (!merge)
		{
			step = new Step { center = center, radius = radius };
			undo.Add(step);
			while (undo.Count > Mathf.Max(1, MaxSteps))
			{
				undo.RemoveAt(0);
			}
		}
		else
		{
			// Grow the step's area to cover this strike too.
			float reach = Vector3.Distance(step!.center, center) + radius;
			step.radius = Mathf.Max(step.radius, reach);
		}
		step!.time = Time.time;
		redo.Clear();

		heightmaps.Clear();
		Heightmap.FindHeightmap(center, radius + 2f, heightmaps);
		float r = radius + 2f;
		foreach (Heightmap hmap in heightmaps)
		{
			TerrainComp comp = hmap.GetAndCreateTerrainCompiler();
			if (comp == null)
			{
				continue;
			}
			if (!comp.m_initialized)
			{
				comp.Initialize();
			}
			if (comp.m_levelDelta == null)
			{
				continue;
			}
			Vector3 origin = hmap.transform.position;
			int half = hmap.m_width / 2;
			for (int y = 0; y <= hmap.m_width; y++)
			{
				float dz = origin.z + (y - half) * hmap.m_scale - center.z;
				if (Mathf.Abs(dz) > r)
				{
					continue;
				}
				for (int x = 0; x <= hmap.m_width; x++)
				{
					float dx = origin.x + (x - half) * hmap.m_scale - center.x;
					if (dx * dx + dz * dz > r * r)
					{
						continue;
					}
					int i = y * (comp.m_width + 1) + x;
					if (!step.vertices.ContainsKey((comp, i)))
					{
						step.vertices[(comp, i)] = Read(comp, i);
					}
				}
			}
		}
	}

	private static Vertex Read(TerrainComp comp, int i) => new()
	{
		level = comp.m_levelDelta[i],
		smooth = comp.m_smoothDelta[i],
		modifiedHeight = comp.m_modifiedHeight[i],
		paint = comp.m_paintMask != null ? comp.m_paintMask[i] : default,
		modifiedPaint = comp.m_modifiedPaint != null && comp.m_modifiedPaint[i],
	};

	internal static void Update(Player player)
	{
		if (!Incline.CtrlDown)
		{
			return;
		}
		bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		if (Input.GetKeyDown(KeyCode.Z) && !shift)
		{
			Apply(player, undo, redo, "Undid", "Nothing to undo");
		}
		else if (Input.GetKeyDown(KeyCode.Y) || (Input.GetKeyDown(KeyCode.Z) && shift))
		{
			Apply(player, redo, undo, "Redid", "Nothing to redo");
		}
	}

	// Takes the newest step from `from`, records the current state of its vertices in `to` (so it can
	// be reversed) and writes the step's values back.
	private static void Apply(Player player, List<Step> from, List<Step> to, string done, string nothing)
	{
		if (from.Count == 0)
		{
			player.Message(MessageHud.MessageType.TopLeft, nothing);
			return;
		}
		Step step = from[from.Count - 1];
		from.RemoveAt(from.Count - 1);

		TerrainZones.Claim(step.center, step.radius);
		Step reverse = new() { center = step.center, radius = step.radius, time = Time.time };
		HashSet<TerrainComp> changed = new();
		int missing = 0;
		foreach (KeyValuePair<(TerrainComp comp, int i), Vertex> v in step.vertices)
		{
			TerrainComp comp = v.Key.comp;
			int i = v.Key.i;
			if (comp == null || !comp.m_initialized || comp.m_levelDelta == null || i >= comp.m_levelDelta.Length)
			{
				missing++;
				continue;
			}
			reverse.vertices[v.Key] = Read(comp, i);
			comp.m_levelDelta[i] = v.Value.level;
			comp.m_smoothDelta[i] = v.Value.smooth;
			comp.m_modifiedHeight[i] = v.Value.modifiedHeight;
			if (comp.m_paintMask != null)
			{
				comp.m_paintMask[i] = v.Value.paint;
			}
			if (comp.m_modifiedPaint != null)
			{
				comp.m_modifiedPaint[i] = v.Value.modifiedPaint;
			}
			changed.Add(comp);
		}
		foreach (TerrainComp comp in changed)
		{
			comp.m_operations++;
			comp.Save(false);
			if (comp.m_hmap != null)
			{
				comp.m_hmap.Poke(0, false);
			}
		}
		if (ClutterSystem.instance != null)
		{
			ClutterSystem.instance.ResetGrass(step.center, step.radius + 2f);
		}
		if (reverse.vertices.Count > 0)
		{
			to.Add(reverse);
		}
		string left = from.Count > 0 ? $" ({from.Count} more)" : "";
		player.Message(MessageHud.MessageType.TopLeft, missing > 0 && changed.Count == 0 ? "That ground isn't loaded any more; can't change it" : $"{done} hoe strike{left}");
	}
}
