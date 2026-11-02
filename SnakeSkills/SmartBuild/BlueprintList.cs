#nullable disable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

/// <summary>
/// SnakeSkills: a plain list of the saved blueprints (Blueprint List Key, Y by default, with the
/// hammer out). Click a name to build it, type to filter, save the current selection from here.
/// Drawn with IMGUI from Plugin.OnGUI; the mouse is freed and player input blocked while open
/// (the Panel*Patch classes check <see cref="IsOpen"/>).
/// </summary>
internal static class BlueprintList
{
	private const string SearchControl = "SnakeSkillsBlueprintSearch";

	private static bool _open;

	private static string _search = "";

	private static Vector2 _scroll;

	private static string _confirmDelete;

	private static bool _focusSearch;

	private static GUIStyle _rowStyle;

	private static GUIStyle _titleStyle;

	private static GUIStyle _pictureStyle;

	private static GUIStyle _smallStyle;

	public static bool IsOpen => _open;

	/// <summary>True while the search box has keyboard focus (set in OnGUI).</summary>
	public static bool Typing { get; private set; }

	/// <summary>The frame the list closed, so the Escape that closed it doesn't also open the menu.</summary>
	public static int ClosedFrame { get; private set; } = -1;

	public static void Open(Player player)
	{
		_open = true;
		_confirmDelete = null;
		_focusSearch = true;
		Typing = false;
	}

	public static void Close()
	{
		if (_open)
		{
			_open = false;
			Typing = false;
			ClosedFrame = Time.frameCount;
		}
	}

	public static void Draw(Player player)
	{
		if (!_open)
		{
			return;
		}
		if (player == null)
		{
			Close();
			return;
		}
		EnsureStyles();
		float width = Mathf.Min(620f, Screen.width - 40f);
		float height = Mathf.Min(720f, Screen.height - 80f);
		Rect rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
		GUI.Box(rect, GUIContent.none);
		GUI.Box(rect, GUIContent.none);
		GUILayout.BeginArea(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, rect.height - 20f));
		GUILayout.Label("Blueprints (" + BlueprintLibrary.All.Count + ")", _titleStyle);
		GUILayout.BeginHorizontal();
		GUILayout.Label("Search", GUILayout.Width(60f));
		GUI.SetNextControlName(SearchControl);
		_search = GUILayout.TextField(_search ?? "", 40);
		if (GUILayout.Button("x", GUILayout.Width(28f)))
		{
			_search = "";
		}
		GUILayout.EndHorizontal();
		if (_focusSearch)
		{
			GUI.FocusControl(SearchControl);
			_focusSearch = false;
		}
		Typing = GUI.GetNameOfFocusedControl() == SearchControl;
		GUILayout.Space(6f);
		_scroll = GUILayout.BeginScrollView(_scroll);
		List<Blueprint> shown = Filtered();
		if (BlueprintLibrary.All.Count == 0)
		{
			GUILayout.Label("No blueprints saved yet. Select built pieces (see below), then press Save.", _smallStyle);
		}
		else if (shown.Count == 0)
		{
			GUILayout.Label("Nothing matches '" + _search + "'.", _smallStyle);
		}
		Blueprint pick = null;
		string delete = null;
		foreach (Blueprint blueprint in shown)
		{
			GUILayout.BeginHorizontal();
			string label = blueprint.Name + "   <color=#bbbbbb>" + blueprint.Items.Count + (blueprint.Items.Count == 1 ? " piece" : " pieces") + "</color>";
			bool pictures = ModConfig.ListThumbnails.Value;
			float rowHeight = pictures ? 64f : 30f;
			if (pictures)
			{
				Texture2D picture = BlueprintThumbnails.Get(blueprint.Name);
				if (picture != null ? GUILayout.Button(picture, _pictureStyle, GUILayout.Width(64f), GUILayout.Height(64f)) : GUILayout.Button("...", _pictureStyle, GUILayout.Width(64f), GUILayout.Height(64f)))
				{
					pick = blueprint;
				}
			}
			if (GUILayout.Button(label, _rowStyle, GUILayout.Height(rowHeight)))
			{
				pick = blueprint;
			}
			bool confirming = _confirmDelete == blueprint.Name;
			if (GUILayout.Button(confirming ? "Sure?" : "Delete", GUILayout.Width(64f), GUILayout.Height(rowHeight)))
			{
				if (confirming)
				{
					delete = blueprint.Name;
				}
				else
				{
					_confirmDelete = blueprint.Name;
				}
			}
			GUILayout.EndHorizontal();
		}
		GUILayout.EndScrollView();
		GUILayout.Space(6f);
		int selected = BlueprintSession.Count;
		GUILayout.Label(selected == 0
			? "To make a blueprint: close this, aim at built pieces and press " + GridPreview.KeyName(ModConfig.KeyBlueprintSelect.Value) + " (Shift: the group it was built with, Alt: everything connected), or " + GridPreview.KeyName(ModConfig.KeyDesigner.Value) + " for a box. Then open this list again and Save."
			: selected + (selected == 1 ? " piece" : " pieces") + " selected.", _smallStyle);
		GUILayout.BeginHorizontal();
		GUI.enabled = selected > 0;
		if (GUILayout.Button("Save selection as blueprint...", GUILayout.Height(30f)))
		{
			Close();
			BlueprintSavePrompt.Show(player);
		}
		GUI.enabled = true;
		if (GUILayout.Button("Close (" + GridPreview.KeyName(ModConfig.KeyBlueprintList.Value) + " / Esc)", GUILayout.Width(150f), GUILayout.Height(30f)))
		{
			Close();
		}
		GUILayout.EndHorizontal();
		GUILayout.EndArea();
		if (delete != null)
		{
			_confirmDelete = null;
			Delete(player, delete);
		}
		else if (pick != null)
		{
			Select(player, pick);
		}
	}

	private static List<Blueprint> Filtered()
	{
		List<Blueprint> list = new List<Blueprint>();
		string search = (_search ?? "").Trim();
		foreach (Blueprint blueprint in BlueprintLibrary.All)
		{
			if (search.Length == 0 || blueprint.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				list.Add(blueprint);
			}
		}
		return list;
	}

	private static void Select(Player player, Blueprint blueprint)
	{
		Close();
		SmartHammer.SetOn(player, on: true, announce: false);
		Piece stub = BlueprintPieces.StubPiece(blueprint.Name);
		if (stub == null)
		{
			BlueprintPieces.Register(blueprint, player);
			stub = BlueprintPieces.StubPiece(blueprint.Name);
		}
		SmartHammer.Sync();
		if (stub == null || !TrySelect(player, stub))
		{
			GameApi.Message(player, "Could not pick '" + blueprint.Name + "', see the BepInEx log");
			Plugin.Log.LogWarning("Blueprint list: '" + blueprint.Name + "' is not in the hammer's build menu");
			return;
		}
		GameApi.Message(player, "Building '" + blueprint.Name + "'");
	}

	private static bool TrySelect(Player player, Piece stub)
	{
		if (Safe.Call(() => player.SetSelectedPiece(stub), fallback: false, "Player.SetSelectedPiece"))
		{
			return true;
		}
		// Not in the build menu's lists yet: learn it (no cost, so it's known at once) and rebuild them.
		Safe.Run(delegate
		{
			player.UpdateKnownRecipesList();
			player.UpdateAvailablePiecesList();
		}, "Player.UpdateAvailablePiecesList");
		return Safe.Call(() => player.SetSelectedPiece(stub), fallback: false, "Player.SetSelectedPiece");
	}

	private static void Delete(Player player, string name)
	{
		GameObject ghost = player.m_placementGhost;
		if (ghost != null && BlueprintPieces.TryGetBlueprint(ghost.name, out var shown) && shown.Name == name)
		{
			BlueprintPlacer.Disarm();
		}
		BlueprintLibrary.Delete(name);
		SmartHammer.Sync();
		GameApi.Message(player, "Deleted blueprint '" + name + "'");
	}

	private static void EnsureStyles()
	{
		if (_rowStyle != null)
		{
			return;
		}
		_rowStyle = new GUIStyle(GUI.skin.button)
		{
			alignment = TextAnchor.MiddleLeft,
			richText = true,
			fontSize = 15
		};
		_pictureStyle = new GUIStyle(GUI.skin.button)
		{
			padding = new RectOffset(2, 2, 2, 2),
			margin = new RectOffset(0, 4, 2, 2)
		};
		_titleStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = 18,
			fontStyle = FontStyle.Bold
		};
		_titleStyle.normal.textColor = new Color(1f, 0.631f, 0.235f, 1f);
		_smallStyle = new GUIStyle(GUI.skin.label)
		{
			wordWrap = true,
			fontSize = 13
		};
	}
}
