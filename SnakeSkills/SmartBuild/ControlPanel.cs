#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace SnakeSkills.SmartBuild;

internal static class ControlPanel
{
	private delegate void AxisSetter(int axis, float value);

	private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

	private const float PanelWidth = 460f;

	private const float LegendWidth = 460f;

	private const float LegendKeyWidth = 180f;

	private const float Pad = 16f;

	private const float RowHeight = 28f;

	private const float RowGap = 6f;

	private const float LabelWidth = 118f;

	private const float SmallButton = 28f;

	private const float FieldWidth = 64f;

	private const float ScreenMargin = 20f;

	private static GameObject _window;

	private static RectTransform _windowRect;

	private static RectTransform _legendRoot;

	private static float _settingsBottom;

	private static float _legendBottom;

	private static readonly List<InputField> _fields = new List<InputField>();

	private static InputField _sizeX;

	private static InputField _sizeY;

	private static InputField _sizeZ;

	private static InputField _gapX;

	private static InputField _gapY;

	private static InputField _gapZ;

	private static InputField _staggerX;

	private static InputField _staggerY;

	private static InputField _staggerZ;

	private static InputField _staggerLayerZ;

	private static InputField _staggerColumnY;

	private static InputField _staggerRowY;

	private static InputField[] _columnTurnFields;

	private static InputField[] _rowTurnFields;

	private static InputField[] _layerTurnFields;

	private static InputField[] _tiltFields;

	private static Toggle _terrainToggle;

	private static Toggle _hudToggle;

	private static Toggle _tiltModeToggle;

	private static Toggle _controlsToggle;

	private static Text _blueprintStatus;

	private static Text _cellCount;

	private static Text _holdLabel;

	private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.14f);

	public static bool IsOpen
	{
		get
		{
			if (_window != null)
			{
				return _window.activeSelf;
			}
			return false;
		}
	}

	public static bool Typing
	{
		get
		{
			foreach (InputField fieldItem in _fields)
			{
				if (fieldItem != null && fieldItem.isFocused)
				{
					return true;
				}
			}
			return false;
		}
	}

	public static bool CapturingInput
	{
		get
		{
			if (!Typing)
			{
				return CursorOverPanel();
			}
			return true;
		}
	}

	public static bool CursorOverPanel()
	{
		if (IsOpen && _windowRect != null)
		{
			return RectTransformUtility.RectangleContainsScreenPoint(_windowRect, Input.mousePosition, null);
		}
		return false;
	}

	public static void Init()
	{
		GUIManager.OnCustomGUIAvailable += Build;
	}

	public static void Toggle()
	{
		Build();
		if (!(_window == null))
		{
			bool flag = !_window.activeSelf;
			_window.SetActive(flag);
			if (flag)
			{
				Refresh();
			}
		}
	}

	public static void Close()
	{
		if (_window != null)
		{
			_window.SetActive(value: false);
		}
	}

	public static void Refresh()
	{
		if (!IsOpen)
		{
			return;
		}
		try
		{
			RefreshLive();
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("panel-refresh", "Updating the control panel failed: " + ex);
		}
	}

	private static void RefreshLive()
	{
		SyncField(_sizeX, GridState.Cols.ToString(Invariant));
		SyncField(_sizeY, GridState.Layers.ToString(Invariant));
		SyncField(_sizeZ, GridState.Rows.ToString(Invariant));
		SyncField(_gapX, Format(GridState.GapX));
		SyncField(_gapY, Format(GridState.GapY));
		SyncField(_gapZ, Format(GridState.GapZ));
		SyncField(_staggerX, Format(GridState.StaggerX));
		SyncField(_staggerY, Format(GridState.StaggerY));
		SyncField(_staggerZ, Format(GridState.StaggerZ));
		SyncField(_staggerLayerZ, Format(GridState.StaggerLayerZ));
		SyncField(_staggerColumnY, Format(GridState.StaggerColumnY));
		SyncField(_staggerRowY, Format(GridState.StaggerRowY));
		SyncTurn(_columnTurnFields, GridState.ColumnTurn);
		SyncTurn(_rowTurnFields, GridState.RowTurn);
		SyncTurn(_layerTurnFields, GridState.LayerTurn);
		SyncTurn(_tiltFields, GridState.Tilt);
		if (_terrainToggle != null)
		{
			_terrainToggle.SetIsOnWithoutNotify(GridState.FollowTerrain);
		}
		if (_tiltModeToggle != null)
		{
			_tiltModeToggle.SetIsOnWithoutNotify(GridState.TiltMode);
		}
		if (_hudToggle != null)
		{
			_hudToggle.SetIsOnWithoutNotify(ModConfig.ShowHud.Value);
		}
		if (_controlsToggle != null)
		{
			_controlsToggle.SetIsOnWithoutNotify(ModConfig.PanelShowControls.Value);
		}
		if (_blueprintStatus != null)
		{
			_blueprintStatus.text = ((BlueprintSession.Count == 0) ? ("Aim at built pieces and press " + GridPreview.KeyName(ModConfig.KeyBlueprintSelect.Value) + " to select them (" + GridPreview.KeyName(ModConfig.BigStepModifier.Value) + " + " + GridPreview.KeyName(ModConfig.KeyBlueprintSelect.Value) + ": the whole group).") : (BlueprintSession.Count + ((BlueprintSession.Count == 1) ? " piece selected. The gold one is the origin." : " pieces selected. The gold one is the origin.")));
		}
		if (_cellCount != null)
		{
			_cellCount.text = GridState.Cells + ((GridState.Cells == 1) ? " piece" : " pieces");
		}
		if (_holdLabel != null)
		{
			_holdLabel.text = (GridHold.Frozen ? "Release hold" : "Hold");
		}
	}

	private static void SyncTurn(InputField[] fields, Vector3 turn)
	{
		if (fields != null && fields.Length >= 3)
		{
			SyncField(fields[0], Format(turn.y));
			SyncField(fields[1], Format(turn.x));
			SyncField(fields[2], Format(turn.z));
		}
	}

	private static void SyncField(InputField fieldItem, string value)
	{
		if (!(fieldItem == null) && !fieldItem.isFocused && !(fieldItem.text == value))
		{
			fieldItem.SetTextWithoutNotify(value);
		}
	}

	private static string Format(float value)
	{
		return value.ToString("0.###", Invariant);
	}

	private static bool TryParse(string text, out float value)
	{
		return float.TryParse(text.Replace(',', '.'), NumberStyles.Float, Invariant, out value);
	}

	private static void Build()
	{
		if (_window != null)
		{
			return;
		}
		try
		{
			BuildWindow();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Could not build the SmartBuild panel: " + ex);
		}
	}

	private static void BuildWindow()
	{
		_fields.Clear();
		GUIManager instance = GUIManager.Instance;
		Transform transform = GUIManager.CustomGUIFront.transform;
		_window = instance.CreateWoodpanel(transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, 920f, 400f);
		_window.name = "SmartBuildPanel";
		_windowRect = (RectTransform)_window.transform;
		_windowRect.pivot = new Vector2(0f, 0.5f);
		_windowRect.anchoredPosition = new Vector2(20f, 0f);
		_window.SetActive(value: false);
		float y = -16f;
		AddTitle(ref y, "SnakeHammer");
		float top = y;
		AddHeader(ref y, "Size (negative grows the other way, 1 = one piece)");
		_sizeX = AddSizeRow(ref y, "Width (X)", 0);
		_sizeY = AddSizeRow(ref y, "Height (Y)", 1);
		_sizeZ = AddSizeRow(ref y, "Depth (Z)", 2);
		AddHeader(ref y, "Gap (m)");
		InputField[] array = AddAxisRow(ref y);
		_gapX = array[0];
		_gapY = array[1];
		_gapZ = array[2];
		WireAxisFields(array, GridState.SetGap);
		AddHeader(ref y, "Stagger (m, accumulates, per piece along ...)");
		(_staggerX, _staggerRowY) = AddStaggerRow(ref y, "Each row", "X", 0, "Up", 5);
		(_staggerColumnY, _staggerZ) = AddStaggerRow(ref y, "Each column", "Up", 4, "Z", 2);
		(_staggerY, _staggerLayerZ) = AddStaggerRow(ref y, "Each layer", "X", 1, "Z", 3);
		AddHeader(ref y, "Rotation (degrees added per piece)");
		_columnTurnFields = AddTurnRow(ref y, "Each column", GridAxis.Columns);
		_rowTurnFields = AddTurnRow(ref y, "Each row", GridAxis.Rows);
		_layerTurnFields = AddTurnRow(ref y, "Each layer", GridAxis.Layers);
		_tiltFields = AddNamedFieldsRow(ref y, "Whole grid (tilt)", 38f, ("Yaw", delegate(float v)
		{
			GridState.SetTilt(TurnAxis.Yaw, v);
		}), ("Pitch", delegate(float v)
		{
			GridState.SetTilt(TurnAxis.Pitch, v);
		}), ("Roll", delegate(float v)
		{
			GridState.SetTilt(TurnAxis.Roll, v);
		}));
		_tiltModeToggle = AddToggle(ref y, "R tilts the whole grid (tap " + GridPreview.KeyName(ModConfig.GridModifier.Value) + ")", delegate(bool v)
		{
			GridState.SetTiltMode(v);
		});
		_terrainToggle = AddToggle(ref y, "Follow terrain (pieces rise and fall with the ground)", delegate(bool v)
		{
			GridState.SetFollowTerrain(v);
		});
		_hudToggle = AddToggle(ref y, "Show HUD (readout and controls legend)", delegate(bool v)
		{
			ModConfig.ShowHud.Value = v;
		});
		_controlsToggle = AddToggle(ref y, "Show the controls list in this panel", delegate(bool v)
		{
			ModConfig.PanelShowControls.Value = v;
			ApplyLayout();
		});
		AddHeader(ref y, "Blueprint");
		_blueprintStatus = AddWrappedText(ref y, "", 40f);
		AddButtonRow(ref y, ("Save...", delegate
		{
			BlueprintSavePrompt.Show(Player.m_localPlayer);
		}), ("Clear", delegate
		{
			BlueprintSession.Clear();
			BlueprintPlacer.Disarm();
		}));
		AddButtonRow(ref y, ("Designer box (" + GridPreview.KeyName(ModConfig.KeyDesigner.Value) + ")", delegate
		{
			DesignerSession.OnKey(Player.m_localPlayer);
		}), ("Turn box off", delegate
		{
			DesignerSession.TurnOff(Player.m_localPlayer);
		}));
		_cellCount = AddPlainText(ref y, "");
		AddButtonRow(ref y, ("Reset", delegate
		{
			GridState.Reset();
		}), ("Hold", delegate
		{
			GridHold.Toggle(Player.m_localPlayer);
		}), ("Undo last", delegate
		{
			PlacementHistory.Undo(Player.m_localPlayer);
		}), ("Close", Close));
		_holdLabel = FindButtonLabel(_window, "Hold");
		AddHeader(ref y, "Nudge (holds the grid)");
		AddButtonRow(ref y, ("Left", delegate
		{
			NudgeClick(-1, 0, 0);
		}), ("Forward", delegate
		{
			NudgeClick(0, 1, 0);
		}), ("Back", delegate
		{
			NudgeClick(0, -1, 0);
		}), ("Right", delegate
		{
			NudgeClick(1, 0, 0);
		}));
		AddButtonRow(ref y, ("Up", delegate
		{
			NudgeClick(0, 0, 1);
		}), ("Down", delegate
		{
			NudgeClick(0, 0, -1);
		}));
		AddNote(ref y, "Relative to the camera. " + ModConfig.NudgeStep.Value.ToString("0.##", Invariant) + " m per click, " + GridPreview.KeyName(ModConfig.BigStepModifier.Value) + ": " + ModConfig.NudgeBigStep.Value.ToString("0.##", Invariant) + " m");
		GameObject gameObject = new GameObject("SmartBuildControls", typeof(RectTransform));
		gameObject.transform.SetParent(_window.transform, worldPositionStays: false);
		_legendRoot = (RectTransform)gameObject.transform;
		RectTransform legendRoot = _legendRoot;
		RectTransform legendRoot2 = _legendRoot;
		Vector2 vector2 = (_legendRoot.pivot = new Vector2(0f, 1f));
		Vector2 anchorMin = (legendRoot2.anchorMax = vector2);
		legendRoot.anchorMin = anchorMin;
		_legendRoot.anchoredPosition = Vector2.zero;
		_legendRoot.sizeDelta = Vector2.zero;
		_settingsBottom = y;
		_legendBottom = BuildLegendColumn(top);
		ApplyLayout();
	}

	private static void ApplyLayout()
	{
		if (!(_window == null) && !(_legendRoot == null))
		{
			bool value = ModConfig.PanelShowControls.Value;
			_legendRoot.gameObject.SetActive(value);
			float num = (value ? Mathf.Min(_settingsBottom, _legendBottom) : _settingsBottom) - 10f;
			_windowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, value ? 920f : 460f);
			_windowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f - num);
		}
	}

	private static float BuildLegendColumn(float top)
	{
		float num = 460f;
		float num2 = 444f;
		float num3 = top - 6f;
		AddColumnText("Controls", num, num3, num2, GUIManager.Instance.AveriaSerifBold, 16, GUIManager.Instance.ValheimOrange);
		num3 -= 24f;
		Text text = AddColumnText("Arrows:  <color=#F24040>red = X (width)</color>   <color=#59E659>green = Y (height)</color>   <color=#598CFF>blue = Z (depth)</color>", num, num3, num2, GUIManager.Instance.AveriaSerifBold, 13, Color.white);
		text.supportRichText = true;
		num3 -= Height(text) + 6f;
		AddDivider(num, num3, num2);
		num3 -= 5f;
		string[] array = GridPreview.LegendLines();
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i];
			int num4 = text2.IndexOf("    ", StringComparison.Ordinal);
			float num5;
			if (i == 0 || num4 < 0)
			{
				num5 = Height(AddColumnText(text2.Trim(), num, num3, num2, (i == 0) ? GUIManager.Instance.AveriaSerifBold : GUIManager.Instance.AveriaSerif, 13, Color.white));
			}
			else
			{
				float num6 = num2 - 180f - 8f;
				Text t = AddColumnText(Capitalize(text2.Substring(num4).Trim()), num, num3, num6, GUIManager.Instance.AveriaSerif, 13, Color.white);
				num5 = Mathf.Max(Height(AddColumnText(text2.Substring(0, num4).Trim(), num + num6 + 8f, num3, 180f, GUIManager.Instance.AveriaSerifBold, 13, Color.white)), Height(t));
			}
			num3 -= num5 + 3f;
			if (i < array.Length - 1)
			{
				AddDivider(num, num3, num2);
			}
			num3 -= 4f;
		}
		return num3;
	}

	private static void AddDivider(float left, float top, float width)
	{
		GameObject gameObject = new GameObject("SmartBuildDivider", typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(_legendRoot, worldPositionStays: false);
		RectTransform obj = (RectTransform)gameObject.transform;
		Vector2 anchorMin = (obj.anchorMax = new Vector2(0f, 1f));
		obj.anchorMin = anchorMin;
		obj.pivot = new Vector2(0f, 1f);
		obj.anchoredPosition = new Vector2(left, top);
		obj.sizeDelta = new Vector2(width, 1f);
		Image component = gameObject.GetComponent<Image>();
		component.color = DividerColor;
		component.raycastTarget = false;
	}

	private static Text AddColumnText(string text, float left, float top, float width, Font font, int fontSize, Color color)
	{
		GameObject gameObject = GUIManager.Instance.CreateText(text, (Transform)_legendRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, font, fontSize, color, true, Color.black, width, (float)fontSize + 6f, false);
		Text component = gameObject.GetComponent<Text>();
		component.alignment = TextAnchor.UpperLeft;
		component.horizontalOverflow = HorizontalWrapMode.Wrap;
		component.verticalOverflow = VerticalWrapMode.Overflow;
		RectTransform obj = (RectTransform)gameObject.transform;
		obj.pivot = new Vector2(0f, 1f);
		obj.anchoredPosition = new Vector2(left, top);
		obj.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Height(component));
		return component;
	}

	private static float Height(Text t)
	{
		return Mathf.Max((float)t.fontSize + 4f, t.preferredHeight);
	}

	private static string Capitalize(string text)
	{
		if (!string.IsNullOrEmpty(text))
		{
			return char.ToUpperInvariant(text[0]) + text.Substring(1);
		}
		return text;
	}

	private static void NudgeClick(int right, int forward, int up)
	{
		Player localPlayer = Player.m_localPlayer;
		if (!(localPlayer == null))
		{
			float step = (Keys.Held(ModConfig.BigStepModifier.Value) ? ModConfig.NudgeBigStep.Value : ModConfig.NudgeStep.Value);
			GridHold.Nudge(localPlayer, right, forward, up, step);
		}
	}

	private static void AddTitle(ref float y, string text)
	{
		float x = 230f;
		GUIManager.Instance.CreateText(text, _window.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y - 14f), GUIManager.Instance.AveriaSerifBold, 20, Color.white, true, Color.black, 428f, 28f, false).GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
		y -= 36f;
	}

	private static void AddHeader(ref float y, string text)
	{
		y -= 6f;
		GUIManager.Instance.CreateText(text, _window.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, y - 11f), GUIManager.Instance.AveriaSerifBold, 16, GUIManager.Instance.ValheimOrange, true, Color.black, 428f, 22f, false).GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
		y -= 24f;
	}

	private static void AddNote(ref float y, string text)
	{
		GUIManager.Instance.CreateText(text, _window.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, y - 9f), GUIManager.Instance.AveriaSerif, 12, new Color(1f, 1f, 1f, 0.75f), false, Color.black, 428f, 20f, false).GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
		y -= 22f;
	}

	private static Text AddPlainText(ref float y, string text, int fontSize = 15, bool bold = false)
	{
		Text component = GUIManager.Instance.CreateText(text, _window.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, y - (float)fontSize * 0.75f - 3f), bold ? GUIManager.Instance.AveriaSerifBold : GUIManager.Instance.AveriaSerif, fontSize, Color.white, true, Color.black, 428f, (float)fontSize + 6f, false).GetComponent<Text>();
		component.alignment = TextAnchor.MiddleLeft;
		component.horizontalOverflow = HorizontalWrapMode.Overflow;
		component.verticalOverflow = VerticalWrapMode.Overflow;
		y -= (float)fontSize + 8f;
		return component;
	}

	private static Text AddWrappedText(ref float y, string text, float height)
	{
		Text component = GUIManager.Instance.CreateText(text, _window.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, y - height / 2f), GUIManager.Instance.AveriaSerif, 14, Color.white, true, Color.black, 428f, height, false).GetComponent<Text>();
		component.alignment = TextAnchor.UpperLeft;
		component.horizontalOverflow = HorizontalWrapMode.Wrap;
		y -= height + 6f;
		return component;
	}

	private static InputField AddSizeRow(ref float y, string label, int axis)
	{
		float centerY = y - 14f;
		AddCenteredText(label, 16f, centerY, 118f, 28f, TextAnchor.MiddleLeft);
		float num = 140f;
		AddButton("-", num, centerY, 28f, 28f, delegate
		{
			GridState.AdjustSize((axis == 0) ? (-1) : 0, (axis == 1) ? (-1) : 0, (axis == 2) ? (-1) : 0);
		});
		num += 34f;
		InputField inputField = AddInputField(num, centerY, 64f, 28f);
		_fields.Add(inputField);
		inputField.onEndEdit.AddListener(delegate(string text)
		{
			if (int.TryParse(text, NumberStyles.Integer, Invariant, out var result))
			{
				GridState.SetSize(axis, result);
			}
		});
		num += 70f;
		AddButton("+", num, centerY, 28f, 28f, delegate
		{
			GridState.AdjustSize((axis == 0) ? 1 : 0, (axis == 1) ? 1 : 0, (axis == 2) ? 1 : 0);
		});
		y -= 34f;
		return inputField;
	}

	private static InputField[] AddAxisRow(ref float y)
	{
		float centerY = y - 14f;
		string[] array = new string[3] { "X", "Y", "Z" };
		InputField[] array2 = new InputField[3];
		float num = 16f;
		float num2 = 142.66667f;
		for (int i = 0; i < 3; i++)
		{
			AddCenteredText(array[i], num, centerY, 18f, 28f, TextAnchor.MiddleLeft);
			InputField inputField = AddInputField(num + 20f, centerY, num2 - 26f, 28f);
			_fields.Add(inputField);
			array2[i] = inputField;
			num += num2;
		}
		y -= 34f;
		return array2;
	}

	private static void WireAxisFields(InputField[] fields, AxisSetter set)
	{
		for (int i = 0; i < fields.Length; i++)
		{
			int captured = i;
			fields[i].onEndEdit.AddListener(delegate(string text)
			{
				if (TryParse(text, out var value))
				{
					set(captured, value);
				}
			});
		}
	}

	private static (InputField, InputField) AddStaggerRow(ref float y, string label, string nameA, int indexA, string nameB, int indexB)
	{
		InputField[] array = AddNamedFieldsRow(ref y, label, 26f, (nameA, delegate(float v)
		{
			GridState.SetStagger(indexA, v);
		}), (nameB, delegate(float v)
		{
			GridState.SetStagger(indexB, v);
		}));
		return (array[0], array[1]);
	}

	private static InputField[] AddTurnRow(ref float y, string label, GridAxis axis)
	{
		return AddNamedFieldsRow(ref y, label, 38f, ("Yaw", delegate(float v)
		{
			GridState.SetTurn(axis, TurnAxis.Yaw, v);
		}), ("Pitch", delegate(float v)
		{
			GridState.SetTurn(axis, TurnAxis.Pitch, v);
		}), ("Roll", delegate(float v)
		{
			GridState.SetTurn(axis, TurnAxis.Roll, v);
		}));
	}

	private static InputField[] AddNamedFieldsRow(ref float y, string label, float nameWidth, params (string name, Action<float> set)[] fields)
	{
		float centerY = y - 14f;
		AddCenteredText(label, 16f, centerY, 118f, 28f, TextAnchor.MiddleLeft);
		float num = 140f;
		float num2 = (444f - num) / (float)fields.Length;
		InputField[] array = new InputField[fields.Length];
		for (int i = 0; i < fields.Length; i++)
		{
			(string, Action<float>) tuple = fields[i];
			string item = tuple.Item1;
			Action<float> set = tuple.Item2;
			float num3 = num + num2 * (float)i;
			AddCenteredText(item, num3, centerY, nameWidth, 28f, TextAnchor.MiddleLeft);
			InputField inputField = AddInputField(num3 + nameWidth + 2f, centerY, num2 - nameWidth - 10f, 28f);
			_fields.Add(inputField);
			inputField.onEndEdit.AddListener(delegate(string text)
			{
				if (TryParse(text, out var value))
				{
					set(value);
				}
			});
			array[i] = inputField;
		}
		y -= 34f;
		return array;
	}

	private static InputField AddLabeledPlusMinusRow(ref float y, string label, Action onMinus, Action onPlus, Action<string> onCommit)
	{
		float centerY = y - 14f;
		AddCenteredText(label, 16f, centerY, 118f, 28f, TextAnchor.MiddleLeft);
		float num = 140f;
		AddButton("-", num, centerY, 28f, 28f, onMinus);
		num += 34f;
		InputField inputField = AddInputField(num, centerY, 64f, 28f);
		_fields.Add(inputField);
		inputField.onEndEdit.AddListener(delegate(string text)
		{
			onCommit(text);
		});
		num += 70f;
		AddButton("+", num, centerY, 28f, 28f, onPlus);
		y -= 34f;
		return inputField;
	}

	private static InputField AddPlusMinusRow(ref float y, string minus, string plus, Action onMinus, Action onPlus, Action<string> onCommit)
	{
		float centerY = y - 14f;
		float num = 16f;
		AddButton(minus, num, centerY, 28f, 28f, onMinus);
		num += 34f;
		InputField inputField = AddInputField(num, centerY, 64f, 28f);
		_fields.Add(inputField);
		inputField.onEndEdit.AddListener(delegate(string text)
		{
			onCommit(text);
		});
		num += 70f;
		AddButton(plus, num, centerY, 28f, 28f, onPlus);
		y -= 34f;
		return inputField;
	}

	private static Toggle AddToggle(ref float y, string label, Action<bool> onChanged)
	{
		float y2 = y - 14f;
		GameObject gameObject = GUIManager.Instance.CreateToggle(_window.transform, 22f, 22f);
		RectTransform obj = (RectTransform)gameObject.transform;
		Vector2 anchorMin = (obj.anchorMax = new Vector2(0f, 1f));
		obj.anchorMin = anchorMin;
		obj.anchoredPosition = new Vector2(27f, y2);
		Text text = gameObject.transform.Find("Label")?.GetComponent<Text>();
		if (text != null)
		{
			text.text = label;
			GUIManager.Instance.ApplyTextStyle(text, GUIManager.Instance.AveriaSerif, Color.white, 14, true);
			RectTransform obj2 = (RectTransform)text.transform;
			obj2.anchorMin = new Vector2(0f, 0.5f);
			obj2.anchorMax = new Vector2(0f, 0.5f);
			obj2.pivot = new Vector2(0f, 0.5f);
			obj2.anchoredPosition = new Vector2(20f, 0f);
			obj2.sizeDelta = new Vector2(398f, 28f);
		}
		Toggle component = gameObject.GetComponent<Toggle>();
		component.onValueChanged.AddListener(delegate(bool v)
		{
			onChanged(v);
		});
		y -= 34f;
		return component;
	}

	private static void AddButtonRow(ref float y, params (string label, Action onClick)[] buttons)
	{
		float centerY = y - 14f;
		float num = 6f;
		float num2 = (428f - num * (float)(buttons.Length - 1)) / (float)buttons.Length;
		float num3 = 16f;
		for (int i = 0; i < buttons.Length; i++)
		{
			var (text, onClick) = buttons[i];
			AddButton(text, num3, centerY, num2, 28f, onClick);
			num3 += num2 + num;
		}
		y -= 34f;
	}

	private static void AddButton(string text, float x, float centerY, float width, float height, Action onClick)
	{
		GUIManager.Instance.CreateButton(text, _window.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + width / 2f, centerY), width, height).GetComponent<Button>().onClick.AddListener(delegate
		{
			onClick();
		});
	}

	private static InputField AddInputField(float x, float centerY, float width, float height)
	{
		return GUIManager.Instance.CreateInputField(_window.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + width / 2f, centerY), InputField.ContentType.Standard, (string)null, 14, width, height).GetComponent<InputField>();
	}

	private static void AddCenteredText(string text, float x, float centerY, float width, float height, TextAnchor anchor)
	{
		GUIManager.Instance.CreateText(text, _window.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + width / 2f, centerY), GUIManager.Instance.AveriaSerif, 14, Color.white, true, Color.black, width, height, false).GetComponent<Text>().alignment = anchor;
	}

	private static Text FindButtonLabel(GameObject window, string initialText)
	{
		Text[] componentsInChildren = window.GetComponentsInChildren<Text>(includeInactive: true);
		foreach (Text text in componentsInChildren)
		{
			if (text.text == initialText)
			{
				return text;
			}
		}
		return null;
	}
}
