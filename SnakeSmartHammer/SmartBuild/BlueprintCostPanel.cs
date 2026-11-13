#nullable disable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnakeSmartHammer;

/// <summary>
/// SnakeSkills: the game's build info panel has only a few material slots, so a blueprint that
/// needs more shows every material in a grid beside it (or above it when there's no room) (icon, name, have/need, red when short).
/// Filled each frame by <see cref="HudRequirementsPatch"/> and drawn with IMGUI from Plugin.OnGUI.
/// </summary>
internal static class BlueprintCostPanel
{
	internal struct Entry
	{
		public Sprite Icon;

		public string Name;

		public int Need;

		public int Have;

		public bool Enough;
	}

	private const float CellWidth = 176f;

	private const float CellHeight = 40f;

	private const float Icon = 34f;

	private const float Pad = 8f;

	private static readonly List<Entry> Entries = new List<Entry>();

	private static int _frame = -10;

	// The info panel's bounds in GUI coordinates (top-left origin).
	private static Rect _anchor;

	private static GUIStyle _name;

	private static GUIStyle _count;

	private static GUIStyle _title;

	internal static bool Enabled => ModConfig.CostPanel == null || ModConfig.CostPanel.Value;

	internal static void Show(Hud hud, List<Entry> entries)
	{
		Entries.Clear();
		Entries.AddRange(entries);
		_frame = Time.frameCount;
		_anchor = InfoPanelRect(hud);
	}

	internal static void Draw()
	{
		if (Time.frameCount - _frame > 2 || Entries.Count == 0 || _anchor.width <= 0f)
		{
			return;
		}
		EnsureStyles();
		float right = Screen.width - 12f;
		float left = _anchor.xMax + 10f;
		float bottom = _anchor.yMax;
		float maxHeight = Mathf.Min(bottom - 40f, Screen.height * 0.45f);
		int columnsFit = Mathf.FloorToInt((right - left - Pad * 2f) / CellWidth);
		int rowsFit = Mathf.Max(1, Mathf.FloorToInt((maxHeight - Pad * 2f - 24f) / CellHeight));
		int columns = Mathf.CeilToInt(Entries.Count / (float)rowsFit);
		bool beside = columns <= columnsFit;
		if (!beside)
		{
			// Not enough room on the right: a wide grid just above the info panel, about 3 rows.
			int maxColumns = Mathf.Max(1, Mathf.FloorToInt((Screen.width - 24f - Pad * 2f) / CellWidth));
			columns = Mathf.Clamp(Mathf.CeilToInt(Entries.Count / 3f), 1, maxColumns);
			bottom = _anchor.yMin - 6f;
		}
		int rows = Mathf.CeilToInt(Entries.Count / (float)columns);
		float width = columns * CellWidth + Pad * 2f;
		float height = rows * CellHeight + Pad * 2f + 24f;
		float x = beside ? left : _anchor.center.x - width / 2f;
		Rect rect = new Rect(Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - width)), Mathf.Max(0f, bottom - height), width, height);
		GUI.Box(rect, GUIContent.none);
		GUI.Box(rect, GUIContent.none);
		int short_ = 0;
		foreach (Entry entry in Entries)
		{
			if (!entry.Enough)
			{
				short_++;
			}
		}
		GUI.Label(new Rect(rect.x + Pad, rect.y + 4f, rect.width - Pad * 2f, 22f), "All materials (" + Entries.Count + ")" + (short_ > 0 ? "  <color=#ff6050>" + short_ + " short</color>" : ""), _title);
		for (int i = 0; i < Entries.Count; i++)
		{
			Entry entry = Entries[i];
			int column = i / rows;
			int row = i % rows;
			Rect cell = new Rect(rect.x + Pad + column * CellWidth, rect.y + Pad + 24f + row * CellHeight, CellWidth - 4f, CellHeight - 4f);
			DrawSprite(new Rect(cell.x, cell.y + (cell.height - Icon) / 2f, Icon, Icon), entry.Icon);
			string color = entry.Enough ? "#ffffff" : "#ff6050";
			GUI.Label(new Rect(cell.x + Icon + 4f, cell.y, cell.width - Icon - 4f, 18f), entry.Name, _name);
			GUI.Label(new Rect(cell.x + Icon + 4f, cell.y + 17f, cell.width - Icon - 4f, 18f), "<color=" + color + ">" + entry.Have + " / " + entry.Need + "</color>", _count);
		}
	}

	private static void DrawSprite(Rect rect, Sprite sprite)
	{
		if (sprite == null || sprite.texture == null)
		{
			return;
		}
		try
		{
			Texture2D texture = sprite.texture;
			Rect source = sprite.textureRect;
			Rect uv = new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height);
			GUI.DrawTextureWithTexCoords(rect, texture, uv);
		}
		catch (Exception)
		{
			// Tightly packed atlas sprites have no textureRect; leave the icon out.
		}
	}

	// Union of the build info's title, description and visible material slots, in GUI pixels.
	private static Rect InfoPanelRect(Hud hud)
	{
		float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
		List<RectTransform> parts = new List<RectTransform>();
		if (hud.m_buildSelection != null)
		{
			parts.Add(hud.m_buildSelection.rectTransform);
		}
		if (hud.m_pieceDescription != null)
		{
			parts.Add(hud.m_pieceDescription.rectTransform);
		}
		foreach (GameObject slot in hud.m_requirementItems)
		{
			if (slot != null && slot.activeInHierarchy)
			{
				parts.Add(slot.GetComponent<RectTransform>());
			}
		}
		Vector3[] corners = new Vector3[4];
		foreach (RectTransform part in parts)
		{
			if (part == null)
			{
				continue;
			}
			Canvas canvas = part.GetComponentInParent<Canvas>();
			Camera camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
			part.GetWorldCorners(corners);
			foreach (Vector3 corner in corners)
			{
				Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corner);
				float y = Screen.height - screen.y;
				xMin = Mathf.Min(xMin, screen.x);
				xMax = Mathf.Max(xMax, screen.x);
				yMin = Mathf.Min(yMin, y);
				yMax = Mathf.Max(yMax, y);
			}
		}
		return xMax > xMin ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : default;
	}

	private static void EnsureStyles()
	{
		if (_name != null)
		{
			return;
		}
		_name = new GUIStyle(GUI.skin.label)
		{
			fontSize = 13,
			clipping = TextClipping.Clip,
			wordWrap = false
		};
		_count = new GUIStyle(_name)
		{
			fontSize = 14,
			fontStyle = FontStyle.Bold,
			richText = true
		};
		_title = new GUIStyle(GUI.skin.label)
		{
			fontSize = 15,
			fontStyle = FontStyle.Bold,
			richText = true
		};
		_title.normal.textColor = new Color(1f, 0.631f, 0.235f, 1f);
	}
}
