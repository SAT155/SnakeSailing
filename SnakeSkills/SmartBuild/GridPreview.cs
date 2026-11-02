#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SnakeSkills.SmartBuild;

internal static class GridPreview
{
	private const float ValidateInterval = 0.1f;

	private static readonly List<GameObject> Clones = new List<GameObject>();

	private static float _nextValidate;

	private static int _sourceGhostId;

	private static float _nextAffordCheck;

	private static bool _affordable = true;

	public static Footprint Foot;

	public static int Blocked;

	public static bool OutlineMode;

	public static Steps Step;

	public static GridLayout Layout;

	private static Texture2D _line;

	private const float ReadoutHoldSeconds = 1.5f;

	private static float _lastValueChangeTime = -1.5f;

	private static bool _snapshotInit;

	private static int _sCols;

	private static int _sLayers;

	private static int _sRows;

	private static Vector3 _sColumnTurn;

	private static Vector3 _sRowTurn;

	private static Vector3 _sLayerTurn;

	private static Vector3 _sTilt;

	private static float _sGapX;

	private static float _sGapY;

	private static float _sGapZ;

	private static float _sStaggerX;

	private static float _sStaggerY;

	private static float _sStaggerZ;

	private static float _sStaggerLayerZ;

	private static float _sStaggerColumnY;

	private static float _sStaggerRowY;

	private static bool _sFollowTerrain;

	private static bool _sTiltMode;

	private static readonly Color AxisColorX = new Color(0.95f, 0.25f, 0.25f, 0.95f);

	private static readonly Color AxisColorY = new Color(0.35f, 0.9f, 0.35f, 0.95f);

	private static readonly Color AxisColorZ = new Color(0.35f, 0.55f, 1f, 0.95f);

	private const float ArrowLength = 1.3f;

	private const float ArrowHeadLength = 0.3f;

	private const float ArrowHeadWidth = 0.12f;

	private const float ArrowLabelGap = 0.2f;

	private static GUIStyle _axisLabelStyle;

	private static Blueprint _bpMeasured;

	private static Dictionary<string, Piece> _bpPieces;

	private static Dictionary<string, PieceBounds> _bpBounds;

	private static Vector3 _bpMin;

	private static Vector3 _bpMax;

	private static int _bpHiddenGhostId;

	private const int BlueprintOutlineCap = 300;

	private static readonly List<GameObject> BpClones = new List<GameObject>();

	private static Blueprint _bpClonesFor;

	private static int _bpItemsPerCopy;

	private static int _bpGhostedItems;

	private static bool _anchorBlocked;

	private static Text _readoutText;

	private static readonly Color BadColor = new Color(1f, 0.35f, 0.3f);

	private static readonly Color OutlineColor = new Color(0.4f, 0.9f, 1f, 0.9f);

	private static readonly Color DesignerColor = new Color(1f, 0.6f, 0.15f, 0.95f);

	private static GUIStyle _designerStyle;

	private static bool ShowReadout => Time.time - _lastValueChangeTime <= 1.5f;

	public static Dictionary<string, Piece> ArmedBlueprintPieces
	{
		get
		{
			if (_bpMeasured != BlueprintPlacer.Active)
			{
				return null;
			}
			return _bpPieces;
		}
	}

	public static Dictionary<string, PieceBounds> ArmedBlueprintBounds
	{
		get
		{
			if (_bpMeasured != BlueprintPlacer.Active)
			{
				return null;
			}
			return _bpBounds;
		}
	}

	private static string RotationAxisText()
	{
		return "  |  " + KeyName(ModConfig.KeyRotation.Value) + ": " + GridState.AxisLabel(GridState.RotationAxis) + (GridState.RotationAxisChoice.HasValue ? ", pinned" : "");
	}

	private static string TurnText()
	{
		if (!GridState.HasTurn && !GridState.HasTilt)
		{
			return "";
		}
		List<string> parts = new List<string>();
		Add(GridState.ColumnTurn, "columns");
		Add(GridState.RowTurn, "rows");
		Add(GridState.LayerTurn, "layers");
		Add(GridState.Tilt, "whole grid");
		return "  |  rotation: " + string.Join(", ", parts);
		void Add(Vector3 turn, string what)
		{
			if (turn.y != 0f)
			{
				parts.Add(what + " yaw " + turn.y.ToString("0.##") + "°");
			}
			if (turn.x != 0f)
			{
				parts.Add(what + " pitch " + turn.x.ToString("0.##") + "°");
			}
			if (turn.z != 0f)
			{
				parts.Add(what + " roll " + turn.z.ToString("0.##") + "°");
			}
		}
	}

	private static string StaggerText()
	{
		if (!GridState.HasStagger)
		{
			return "";
		}
		List<string> parts = new List<string>();
		Add(GridState.StaggerX, "rows along X");
		Add(GridState.StaggerRowY, "rows up");
		Add(GridState.StaggerZ, "columns along Z");
		Add(GridState.StaggerColumnY, "columns up");
		Add(GridState.StaggerY, "layers along X");
		Add(GridState.StaggerLayerZ, "layers along Z");
		return "  |  stagger: " + string.Join(", ", parts);
		void Add(float value, string what)
		{
			if (value != 0f)
			{
				parts.Add(what + " " + value.ToString("0.##") + " m");
			}
		}
	}

	private static bool NoteValueSnapshot()
	{
		bool result = !_snapshotInit || _sCols != GridState.Cols || _sLayers != GridState.Layers || _sRows != GridState.Rows || _sGapX != GridState.GapX || _sGapY != GridState.GapY || _sGapZ != GridState.GapZ || _sColumnTurn != GridState.ColumnTurn || _sRowTurn != GridState.RowTurn || _sLayerTurn != GridState.LayerTurn || _sTilt != GridState.Tilt || _sTiltMode != GridState.TiltMode || _sStaggerX != GridState.StaggerX || _sStaggerY != GridState.StaggerY || _sStaggerZ != GridState.StaggerZ || _sStaggerLayerZ != GridState.StaggerLayerZ || _sStaggerColumnY != GridState.StaggerColumnY || _sStaggerRowY != GridState.StaggerRowY || _sFollowTerrain != GridState.FollowTerrain;
		_snapshotInit = true;
		_sCols = GridState.Cols;
		_sLayers = GridState.Layers;
		_sRows = GridState.Rows;
		_sGapX = GridState.GapX;
		_sGapY = GridState.GapY;
		_sGapZ = GridState.GapZ;
		_sColumnTurn = GridState.ColumnTurn;
		_sRowTurn = GridState.RowTurn;
		_sLayerTurn = GridState.LayerTurn;
		_sTilt = GridState.Tilt;
		_sTiltMode = GridState.TiltMode;
		_sStaggerX = GridState.StaggerX;
		_sStaggerY = GridState.StaggerY;
		_sStaggerZ = GridState.StaggerZ;
		_sStaggerLayerZ = GridState.StaggerLayerZ;
		_sStaggerColumnY = GridState.StaggerColumnY;
		_sStaggerRowY = GridState.StaggerRowY;
		_sFollowTerrain = GridState.FollowTerrain;
		return result;
	}

	public static void Update(Player player)
	{
		if (BlueprintPlacer.Active != null)
		{
			UpdateBlueprint(player);
			return;
		}
		HideBlueprintClones();
		GameObject placementGhost = player.m_placementGhost;
		Piece piece = ((placementGhost != null) ? placementGhost.GetComponent<Piece>() : null);
		if (placementGhost == null || piece == null || !GridState.IsGrid)
		{
			Hide();
			Blocked = 0;
			_anchorBlocked = false;
			return;
		}
		Foot = Footprint.Measure(placementGhost);
		Step = GridState.StepsFor(Foot);
		Layout = GridState.LayoutFor(Foot);
		if (Time.time >= _nextAffordCheck)
		{
			_nextAffordCheck = Time.time + 0.25f;
			_affordable = GridCost.CanAfford(player, piece, GridState.Cells);
		}
		OutlineMode = GridState.Cells > ModConfig.PreviewMaxCells.Value;
		if (OutlineMode)
		{
			Hide();
			Blocked = -1;
			_anchorBlocked = false;
			return;
		}
		if (placementGhost.GetInstanceID() != _sourceGhostId)
		{
			Hide();
			_sourceGhostId = placementGhost.GetInstanceID();
			Plugin.Log.LogInfo("Grid spacing for " + placementGhost.name + ": X " + Foot.SpanX.ToString("F2") + ", Y " + Foot.SpanY.ToString("F2") + ", Z " + Foot.SpanZ.ToString("F2") + " (mesh " + Foot.Width.ToString("F2") + " x " + Foot.Height.ToString("F2") + " x " + Foot.Depth.ToString("F2") + ", " + Foot.SnapCount + " snap points)");
		}
		EnsureClones(player, GridState.Cells - 1);
		Vector3 position = placementGhost.transform.position;
		Quaternion rotation = placementGhost.transform.rotation;
		bool flag = Time.time >= _nextValidate;
		if (flag)
		{
			_nextValidate = Time.time + 0.1f;
			Blocked = 0;
		}
		float? num = (GridState.FollowTerrain ? GameApi.GroundHeight(position) : null);
		for (int i = 0; i < Clones.Count; i++)
		{
			Layout.Pose(position, rotation, i + 1, out var position2, out var cellRotation);
			if (num.HasValue)
			{
				position2 = GameApi.FollowTerrain(num.Value, position2);
			}
			Clones[i].transform.SetPositionAndRotation(position2, cellRotation);
		}
		if (flag)
		{
			Physics.SyncTransforms();
			_anchorBlocked = !CellValidator.IsValid(player, piece, placementGhost, position);
			if (_anchorBlocked)
			{
				Blocked++;
			}
			foreach (GameObject clone in Clones)
			{
				bool flag2 = CellValidator.IsValid(player, piece, clone, clone.transform.position);
				GameApi.Highlight(clone, flag2);
				if (!flag2)
				{
					Blocked++;
				}
			}
		}
		if (_anchorBlocked)
		{
			GameApi.Highlight(placementGhost, valid: false);
		}
	}

	private static void UpdateBlueprint(Player player)
	{
		HideGridClones();
		Blueprint active = BlueprintPlacer.Active;
		if (active != _bpMeasured)
		{
			_bpMeasured = null;
			if (!BlueprintPlacer.TryMeasure(player, active, out _bpPieces, out _bpBounds, out var missing))
			{
				GameApi.Message(player, "Missing piece: " + missing + " (this blueprint needs a mod that isn't loaded)");
				BlueprintPlacer.Disarm();
				HideBlueprintClones();
				return;
			}
			PieceBounds value;
			(Vector3 Min, Vector3 Max) tuple = active.LocalBounds((string prefab) => (!_bpBounds.TryGetValue(prefab, out value)) ? default(PieceBounds) : value);
			_bpMin = tuple.Min;
			_bpMax = tuple.Max;
			_bpMeasured = active;
		}
		GameObject placementGhost = player.m_placementGhost;
		if (placementGhost == null)
		{
			_bpGhostedItems = 0;
			return;
		}
		if (placementGhost.GetInstanceID() != _bpHiddenGhostId)
		{
			_bpHiddenGhostId = placementGhost.GetInstanceID();
			Renderer[] componentsInChildren = placementGhost.GetComponentsInChildren<Renderer>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = false;
			}
		}
		GridLayout gridLayout = BlueprintPlacer.RepeatsFor(active, _bpBounds);
		int count = active.Items.Count;
		int count2 = Mathf.Min(gridLayout.Count * count, ModConfig.PreviewMaxCells.Value);
		EnsureBlueprintClones(player, active, count, count2);
		_bpGhostedItems = BpClones.Count;
		Vector3 position = placementGhost.transform.position;
		Quaternion rotation = BlueprintPlacer.AnchorRotation(placementGhost);
		float? num = (GridState.FollowTerrain ? GameApi.GroundHeight(position) : null);
		int num2 = -1;
		float num3 = 0f;
		for (int j = 0; j < BpClones.Count; j++)
		{
			int num4 = j / count;
			int index = j % count;
			gridLayout.Pose(position, rotation, num4, out var position2, out var cellRotation);
			active.PoseOf(index, position2, cellRotation, out var position3, out var rotation2);
			if (num.HasValue)
			{
				if (num4 != num2)
				{
					num2 = num4;
					num3 = GameApi.TerrainShift(num.Value, position2);
				}
				position3.y += num3;
			}
			BpClones[j].transform.SetPositionAndRotation(position3, rotation2);
		}
	}

	private static void EnsureBlueprintClones(Player player, Blueprint blueprint, int itemsPerCopy, int count)
	{
		if (_bpClonesFor != blueprint || _bpItemsPerCopy != itemsPerCopy)
		{
			HideBlueprintClones();
			_bpClonesFor = blueprint;
			_bpItemsPerCopy = itemsPerCopy;
		}
		for (int num = BpClones.Count - 1; num >= 0; num--)
		{
			if (BpClones[num] == null)
			{
				BpClones.RemoveAt(num);
			}
		}
		while (BpClones.Count < count)
		{
			string prefab = blueprint.Items[BpClones.Count % itemsPerCopy].Prefab;
			GameObject gameObject = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(prefab) : null);
			if (gameObject == null)
			{
				break;
			}
			GameObject gameObject2 = GameApi.CloneGhost(gameObject, player);
			gameObject2.name = "SmartBuildBlueprintGhost";
			gameObject2.SetActive(value: true);
			BpClones.Add(gameObject2);
		}
		while (BpClones.Count > count)
		{
			UnityEngine.Object.Destroy(BpClones[BpClones.Count - 1]);
			BpClones.RemoveAt(BpClones.Count - 1);
		}
	}

	private static void HideBlueprintClones()
	{
		if (BpClones.Count == 0 && _bpClonesFor == null)
		{
			return;
		}
		foreach (GameObject bpClone in BpClones)
		{
			if (bpClone != null)
			{
				UnityEngine.Object.Destroy(bpClone);
			}
		}
		BpClones.Clear();
		_bpClonesFor = null;
		_bpItemsPerCopy = 0;
		_bpGhostedItems = 0;
	}

	private static void EnsureClones(Player player, int count)
	{
		for (int num = Clones.Count - 1; num >= 0; num--)
		{
			if (Clones[num] == null)
			{
				Clones.RemoveAt(num);
			}
		}
		while (Clones.Count < count)
		{
			GameObject gameObject = GameApi.CloneGhost(player);
			gameObject.name = "SmartBuildGhost";
			gameObject.SetActive(value: true);
			Clones.Add(gameObject);
		}
		while (Clones.Count > count)
		{
			UnityEngine.Object.Destroy(Clones[Clones.Count - 1]);
			Clones.RemoveAt(Clones.Count - 1);
		}
	}

	public static void Hide()
	{
		HideGridClones();
		HideBlueprintClones();
	}

	private static void HideGridClones()
	{
		foreach (GameObject clone in Clones)
		{
			if (clone != null)
			{
				UnityEngine.Object.Destroy(clone);
			}
		}
		Clones.Clear();
	}

	public static void Draw(Player player)
	{
		SetReadoutActive(active: false);
		if (player == null || !SmartHammer.Equipped(player) || !player.InPlaceMode())
		{
			return;
		}
		if (NoteValueSnapshot())
		{
			_lastValueChangeTime = Time.time;
		}
		bool value = ModConfig.ShowHud.Value;
		if (value && ModConfig.ShowControlsHint.Value && !ControlPanel.IsOpen)
		{
			DrawControls();
		}
		DrawDesigner();
		bool flag = Plugin.AxisKeyHeld() || Keys.Held(ModConfig.GapModifier.Value) || Keys.Held(ModConfig.KeyRotation.Value) || Keys.Held(ModConfig.KeyStagger.Value);
		if (BlueprintPlacer.Active != null)
		{
			DrawBlueprint(player);
		}
		else if (GridState.IsGrid || flag || GridHold.Frozen)
		{
			if (OutlineMode && Layout != null && player.m_placementGhost != null)
			{
				DrawOutline(player.m_placementGhost);
			}
			if (GridState.IsGrid)
			{
				DrawAxisArrows(player.m_placementGhost, Foot.MaxY);
			}
			else if (Plugin.AxisKeyHeld() && player.m_placementGhost != null)
			{
				DrawAxisArrows(player.m_placementGhost, Footprint.Measure(player.m_placementGhost).MaxY);
			}
			if (value && ShowReadout)
			{
				string text = ((Blocked < 0) ? "blocked cells checked on placement" : (Blocked + " blocked"));
				DrawText((GridHold.Frozen ? "HELD  |  " : "") + (GridState.TiltMode ? "TILT MODE  |  " : "") + "X " + GridState.Cols + "  Y " + GridState.Layers + "  Z " + GridState.Rows + "  |  gap " + GridState.GapX.ToString("0.##") + " / " + GridState.GapY.ToString("0.##") + " / " + GridState.GapZ.ToString("0.##") + " m" + RotationAxisText() + TurnText() + (GridState.FollowTerrain ? "  |  follows terrain" : "") + StaggerText() + "  |  " + GridState.Cells + " pieces  |  " + text, Blocked > 0 || !_affordable);
			}
		}
	}

	private static void EnsureReadoutText()
	{
		if (!(_readoutText != null) && GUIManager.Instance != null && !(GUIManager.CustomGUIFront == null))
		{
			GameObject gameObject = GUIManager.Instance.CreateText("", GUIManager.CustomGUIFront.transform, new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.18f), Vector2.zero, GUIManager.Instance.AveriaSerifBold, 14, GUIManager.Instance.ValheimOrange, true, Color.black, 1000f, 26f, false);
			gameObject.name = "SmartBuildReadout";
			_readoutText = gameObject.GetComponent<Text>();
			_readoutText.alignment = TextAnchor.MiddleCenter;
			_readoutText.horizontalOverflow = HorizontalWrapMode.Overflow;
			_readoutText.gameObject.SetActive(value: false);
		}
	}

	private static void SetReadoutActive(bool active)
	{
		if (_readoutText != null)
		{
			_readoutText.gameObject.SetActive(active);
		}
	}

	private static void DrawText(string text, bool bad)
	{
		EnsureReadoutText();
		if (!(_readoutText == null))
		{
			_readoutText.text = text;
			_readoutText.color = (bad ? BadColor : GUIManager.Instance.ValheimOrange);
			_readoutText.gameObject.SetActive(value: true);
		}
	}

	private static void DrawOutline(GameObject ghost)
	{
		Camera main = Camera.main;
		if (main == null)
		{
			return;
		}
		if (_line == null)
		{
			_line = new Texture2D(1, 1);
			_line.SetPixel(0, 0, Color.white);
			_line.Apply();
		}
		if (GridState.HasTurn || GridState.HasStagger)
		{
			int num = Math.Abs(GridState.Cols);
			int num2 = Math.Abs(GridState.Rows);
			int num3 = Math.Abs(GridState.Layers);
			int num4 = num - 1;
			int num5 = (num2 - 1) * num;
			int num6 = (num3 - 1) * num * num2;
			int[] array = ((num3 <= 1) ? new int[1] : new int[2] { 0, num6 });
			foreach (int num7 in array)
			{
				int[] array2 = new int[4]
				{
					0,
					num4,
					num5,
					num5 + num4
				};
				foreach (int num8 in array2)
				{
					DrawCellBox(main, ghost, num7 + num8);
				}
			}
			return;
		}
		Vector3 vector = Step.X * (GridState.Cols - Math.Sign(GridState.Cols));
		Vector3 vector2 = Step.Y * (GridState.Layers - Math.Sign(GridState.Layers));
		Vector3 vector3 = Step.Z * (GridState.Rows - Math.Sign(GridState.Rows));
		Vector3 vector4 = new Vector3(Foot.MinX, Foot.MinY, Foot.MinZ) + ((GridState.Cols < 0) ? vector : Vector3.zero) + ((GridState.Layers < 0) ? vector2 : Vector3.zero) + ((GridState.Rows < 0) ? vector3 : Vector3.zero);
		Vector3 vector5 = new Vector3(Foot.Width, 0f, 0f) + ((GridState.Cols < 0) ? (-vector) : vector);
		Vector3 vector6 = new Vector3(0f, Foot.Height, 0f) + ((GridState.Layers < 0) ? (-vector2) : vector2);
		Vector3 vector7 = new Vector3(0f, 0f, Foot.Depth) + ((GridState.Rows < 0) ? (-vector3) : vector3);
		Vector3[] array3 = new Vector3[8];
		for (int k = 0; k < 8; k++)
		{
			array3[k] = ghost.transform.TransformPoint(vector4 + vector5 * (k & 1) + vector6 * ((k >> 1) & 1) + vector7 * ((k >> 2) & 1));
		}
		for (int l = 0; l < 8; l++)
		{
			for (int num9 = 1; num9 <= 4; num9 <<= 1)
			{
				if ((l & num9) == 0)
				{
					DrawLine(main, array3[l], array3[l | num9]);
				}
			}
		}
	}

	private static void DrawBlueprint(Player player)
	{
		Blueprint active = BlueprintPlacer.Active;
		bool value = ModConfig.ShowHud.Value;
		GameObject placementGhost = player.m_placementGhost;
		int num = 0;
		if (_bpMeasured != null && placementGhost != null)
		{
			GridLayout gridLayout = BlueprintPlacer.RepeatsFor(active, _bpBounds);
			num = gridLayout.Count;
			int num2 = Mathf.Max(1, active.Items.Count);
			int from = Mathf.Min(_bpGhostedItems / num2, num);
			int to = Mathf.Min(num, 300);
			Quaternion quaternion = BlueprintPlacer.AnchorRotation(placementGhost);
			DrawBlueprintOutline(placementGhost.transform.position, quaternion, gridLayout, from, to);
			DrawAxisArrows(placementGhost, _bpMax.y, quaternion);
		}
		if (value && ShowReadout)
		{
			int num3 = num * active.Items.Count;
			string text = ((num > 300) ? (" (preview shows the first " + 300 + ")") : "");
			DrawText((GridState.TiltMode ? "TILT MODE  |  " : "") + "Blueprint '" + active.Name + "'  |  X " + GridState.Cols + "  Y " + GridState.Layers + "  Z " + GridState.Rows + "  |  gap " + GridState.GapX.ToString("0.##") + " / " + GridState.GapY.ToString("0.##") + " / " + GridState.GapZ.ToString("0.##") + " m" + RotationAxisText() + TurnText() + (GridState.FollowTerrain ? "  |  follows terrain" : "") + StaggerText() + "  |  " + num + ((num == 1) ? " copy" : " copies") + ", " + num3 + " pieces" + text + "  |  click to place, " + ModConfig.KeyPanel.Value.ToString() + " to manage", bad: false);
		}
	}

	private static void DrawBlueprintOutline(Vector3 anchor, Quaternion anchorRot, GridLayout repeats, int from, int to)
	{
		Camera main = Camera.main;
		if (main == null)
		{
			return;
		}
		if (_line == null)
		{
			_line = new Texture2D(1, 1);
			_line.SetPixel(0, 0, Color.white);
			_line.Apply();
		}
		for (int i = from; i < to; i++)
		{
			repeats.Cell(i, out Vector3 offset, out Quaternion turn);
			Vector3[] array = new Vector3[8];
			for (int j = 0; j < 8; j++)
			{
				Vector3 vector = new Vector3(((j & 1) == 0) ? _bpMin.x : _bpMax.x, ((j & 2) == 0) ? _bpMin.y : _bpMax.y, ((j & 4) == 0) ? _bpMin.z : _bpMax.z);
				array[j] = anchor + anchorRot * (offset + turn * vector);
			}
			for (int k = 0; k < 8; k++)
			{
				for (int num = 1; num <= 4; num <<= 1)
				{
					if ((k & num) == 0)
					{
						DrawLine(main, array[k], array[k | num]);
					}
				}
			}
		}
	}

	private static void DrawCellBox(Camera cam, GameObject ghost, int index)
	{
		Layout.Cell(index, out Vector3 offset, out Quaternion turn);
		Vector3 vector = new Vector3(Foot.MinX, Foot.MinY, Foot.MinZ);
		Vector3[] array = new Vector3[8];
		for (int i = 0; i < 8; i++)
		{
			Vector3 vector2 = vector + new Vector3(Foot.Width * (float)(i & 1), Foot.Height * (float)((i >> 1) & 1), Foot.Depth * (float)((i >> 2) & 1));
			array[i] = ghost.transform.TransformPoint(offset + turn * vector2);
		}
		for (int j = 0; j < 8; j++)
		{
			for (int num = 1; num <= 4; num <<= 1)
			{
				if ((j & num) == 0)
				{
					DrawLine(cam, array[j], array[j | num]);
				}
			}
		}
	}

	internal static string[] LegendLines()
	{
		string text = KeyName(ModConfig.KeyUp.Value) + " / " + KeyName(ModConfig.KeyDown.Value);
		bool flag = ModConfig.KeyUp.Value == KeyCode.UpArrow && ModConfig.KeyDown.Value == KeyCode.DownArrow;
		bool flag2 = ModConfig.KeyNudgeForward.Value == KeyCode.UpArrow && ModConfig.KeyNudgeBack.Value == KeyCode.DownArrow && ModConfig.KeyNudgeLeft.Value == KeyCode.LeftArrow && ModConfig.KeyNudgeRight.Value == KeyCode.RightArrow;
		string text2 = (flag2 ? "Arrow keys" : (KeyName(ModConfig.KeyNudgeForward.Value) + " / " + KeyName(ModConfig.KeyNudgeBack.Value) + " / " + KeyName(ModConfig.KeyNudgeLeft.Value) + " / " + KeyName(ModConfig.KeyNudgeRight.Value))) + " / " + KeyName(ModConfig.KeyNudgeRaise.Value) + " / " + KeyName(ModConfig.KeyNudgeLower.Value);
		return new string[21]
		{
			flag ? "Up / Down = the Up and Down arrow keys, or the mouse wheel" : (text + " (or the mouse wheel) change the held setting"),
			KeyName(ModConfig.AxisX.Value) + " + " + text + "    width (X)",
			KeyName(ModConfig.AxisX.Value) + " + " + KeyName(ModConfig.AxisZ.Value) + " + " + text + "    height (Y)",
			KeyName(ModConfig.AxisZ.Value) + " + " + text + "    depth (Z)",
			KeyName(ModConfig.GapModifier.Value) + " + width/depth/height keys    gap",
			KeyName(ModConfig.KeyStagger.Value) + " + " + KeyName(ModConfig.AxisX.Value) + " / " + KeyName(ModConfig.AxisZ.Value) + " / both    stagger along X / Z / up (Y), whichever way the grid runs",
			KeyName(ModConfig.KeyRotation.Value) + " + " + text + "    rotate each piece a bit more than the last: yaw (about green Y), pieces still meet flush",
			KeyName(ModConfig.KeyRotation.Value) + " + " + KeyName(ModConfig.AxisX.Value) + " / " + KeyName(ModConfig.AxisZ.Value) + " + " + text + "    pitch (about red X) / roll (about blue Z), along whichever way the grid runs",
			KeyName(ModConfig.KeyRotationAxis.Value) + "    choose which axis " + KeyName(ModConfig.KeyRotation.Value) + " turns: auto (whichever way the grid runs), columns (X), rows (Z), layers (Y)",
			KeyName(ModConfig.GridModifier.Value) + " (tap)    switch R between turning each piece and tilting the whole grid (first piece included)",
			KeyName(ModConfig.KeyHold.Value) + "    Hold (freeze / release)",
			"While held: " + text2 + "    nudge: move it forward / back / left / right (relative to the camera), up / down (" + KeyName(ModConfig.BigStepModifier.Value) + ": bigger steps)",
			KeyName(ModConfig.KeyTerrain.Value) + "    follow terrain on / off",
			KeyName(ModConfig.KeyBlueprintSelect.Value) + "    select a piece for a blueprint (hold and sweep: many; " + KeyName(ModConfig.BigStepModifier.Value) + ": its whole group)",
			"middle mouse on a selected piece (2+ selected)    delete the whole selection (asks first)",
			KeyName(ModConfig.KeyDeleteBlueprint.Value) + "    delete the blueprint selected in the build menu",
			KeyName(ModConfig.KeyDesigner.Value) + "    designer box: tap to start it, set it down (placing a piece does too); tap again for Save / Discard / Turn off; hold to turn it off. What is inside joins the selection (F still works); the size keys resize it while it follows",
			KeyName(ModConfig.KeyPanel.Value) + "    control panel (type exact values)",
			KeyName(ModConfig.GridModifier.Value) + " + " + KeyName(ModConfig.KeyBlueprintSave.Value) + "    save the current blueprint selection",
			KeyName(ModConfig.BigStepModifier.Value) + " = bigger steps (size " + ModConfig.BigStep.Value + ", gap " + ModConfig.GapBigStep.Value.ToString("0.##") + " m, rotation " + ModConfig.RotationBigStep.Value.ToString("0.##") + "°, nudge " + ModConfig.NudgeBigStep.Value.ToString("0.##") + " m)",
			KeyName(ModConfig.GridModifier.Value) + " + " + KeyName(ModConfig.UndoKey.Value) + "    undo last grid"
		};
	}

	private static void DrawControls()
	{
		string text = "Press " + KeyName(ModConfig.KeyPanel.Value) + " to view build panel + controls";
		GUIStyle gUIStyle = new GUIStyle(GUI.skin.label)
		{
			alignment = TextAnchor.UpperLeft,
			fontSize = 15,
			fontStyle = FontStyle.Bold
		};
		Rect position = new Rect(16f, (float)Screen.height * 0.5f, 360f, 22f);
		gUIStyle.normal.textColor = Color.black;
		GUI.Label(new Rect(position.x + 1f, position.y + 1f, position.width, position.height), text, gUIStyle);
		gUIStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
		GUI.Label(position, text, gUIStyle);
	}

	internal static string KeyName(KeyCode key)
	{
		switch (key)
		{
		case KeyCode.RightControl:
		case KeyCode.LeftControl:
			return "Ctrl";
		case KeyCode.RightShift:
		case KeyCode.LeftShift:
			return "Shift";
		case KeyCode.LeftArrow:
			return "Left";
		case KeyCode.RightArrow:
			return "Right";
		case KeyCode.UpArrow:
			return "Up";
		case KeyCode.DownArrow:
			return "Down";
		case KeyCode.PageUp:
			return "PgUp";
		case KeyCode.PageDown:
			return "PgDn";
		default:
			return key.ToString();
		}
	}

	private static void DrawLine(Camera cam, Vector3 a, Vector3 b)
	{
		DrawLine(cam, a, b, OutlineColor);
	}

	private static void DrawLine(Camera cam, Vector3 a, Vector3 b, Color color)
	{
		Vector3 vector = cam.WorldToScreenPoint(a);
		Vector3 vector2 = cam.WorldToScreenPoint(b);
		if (!(vector.z <= 0f) && !(vector2.z <= 0f))
		{
			Vector2 vector3 = new Vector2(vector.x, (float)Screen.height - vector.y);
			Vector2 vector4 = new Vector2(vector2.x, (float)Screen.height - vector2.y) - vector3;
			float angle = Mathf.Atan2(vector4.y, vector4.x) * 57.29578f;
			Matrix4x4 matrix = GUI.matrix;
			GUIUtility.RotateAroundPivot(angle, vector3);
			Color color2 = GUI.color;
			GUI.color = color;
			GUI.DrawTexture(new Rect(vector3.x, vector3.y - 1.5f, vector4.magnitude, 3f), _line);
			GUI.color = color2;
			GUI.matrix = matrix;
		}
	}

	private static void DrawAxisArrows(GameObject ghost, float heightAbove)
	{
		DrawAxisArrows(ghost, heightAbove, (ghost != null) ? ghost.transform.rotation : Quaternion.identity);
	}

	private static void DrawAxisArrows(GameObject ghost, float heightAbove, Quaternion rotation)
	{
		Camera main = Camera.main;
		if (!(main == null) && !(ghost == null))
		{
			if (_line == null)
			{
				_line = new Texture2D(1, 1);
				_line.SetPixel(0, 0, Color.white);
				_line.Apply();
			}
			Vector3 origin = ghost.transform.position + Vector3.up * (heightAbove + 0.4f);
			DrawArrow(main, origin, rotation * Vector3.right, AxisColorX, "X");
			DrawArrow(main, origin, rotation * Vector3.up, AxisColorY, "Y");
			DrawArrow(main, origin, rotation * Vector3.forward, AxisColorZ, "Z");
		}
	}

	private static void DrawArrow(Camera cam, Vector3 origin, Vector3 direction, Color color, string label)
	{
		Vector3 vector = origin + direction * 1.3f;
		DrawLine(cam, origin, vector, color);
		DrawAxisLabel(cam, vector + direction * 0.2f, color, label);
		Vector3 vector2 = Vector3.Cross(direction, cam.transform.forward);
		if (!(vector2.sqrMagnitude < 1E-06f))
		{
			vector2 = vector2.normalized;
			Vector3 vector3 = vector - direction * 0.3f;
			DrawLine(cam, vector, vector3 + vector2 * 0.12f, color);
			DrawLine(cam, vector, vector3 - vector2 * 0.12f, color);
		}
	}

	private static void DrawDesigner()
	{
		DesignerBox box = DesignerSession.Box;
		Camera main = Camera.main;
		if (box == null || main == null)
		{
			return;
		}
		if (_line == null)
		{
			_line = new Texture2D(1, 1);
			_line.SetPixel(0, 0, Color.white);
			_line.Apply();
		}
		Vector3[] array = box.Corners();
		for (int i = 0; i < 8; i++)
		{
			for (int num = 1; num < 8; num <<= 1)
			{
				if ((i & num) == 0 && i != 0)
				{
					DrawLine(main, array[i], array[i | num], DesignerColor);
				}
			}
		}
		DrawBoxAxis(main, array[0], array[1], AxisColorX, "X");
		DrawBoxAxis(main, array[0], array[2], AxisColorY, "Y");
		DrawBoxAxis(main, array[0], array[4], AxisColorZ, "Z");
		Vector3 vector = main.WorldToScreenPoint(box.Center + Vector3.up * (box.Size.y * 0.5f + 0.4f));
		if (!(vector.z <= 0f))
		{
			string text = "Designer box  " + box.Size.x.ToString("0") + " x " + box.Size.y.ToString("0") + " x " + box.Size.z.ToString("0") + " m" + (DesignerSession.Following ? ("  |  " + KeyName(ModConfig.KeyDesigner.Value) + ": set down") : ("  |  " + DesignerSession.Inside + ((DesignerSession.Inside == 1) ? " piece inside" : " pieces inside") + "  |  " + KeyName(ModConfig.KeyDesigner.Value) + (DesignerSession.Resizing ? ": done resizing" : ": save / discard / off / resize")));
			if (_designerStyle == null)
			{
				_designerStyle = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontSize = 16,
					fontStyle = FontStyle.Bold
				};
				_designerStyle.normal.textColor = Color.white;
			}
			GUIStyle designerStyle = _designerStyle;
			Rect position = new Rect(vector.x - 360f, (float)Screen.height - vector.y - 14f, 720f, 28f);
			Color color = GUI.color;
			GUI.color = new Color(0f, 0f, 0f, 0.8f);
			GUI.Label(new Rect(position.x + 1.5f, position.y + 1.5f, position.width, position.height), text, designerStyle);
			GUI.color = DesignerColor;
			GUI.Label(position, text, designerStyle);
			GUI.color = color;
		}
	}

	private static void DrawBoxAxis(Camera cam, Vector3 from, Vector3 to, Color color, string label)
	{
		DrawLine(cam, from, to, color);
		Vector3 normalized = (to - from).normalized;
		DrawAxisLabel(cam, to + normalized * 0.4f, color, label);
	}

	private static void DrawAxisLabel(Camera cam, Vector3 at, Color color, string label)
	{
		Vector3 vector = cam.WorldToScreenPoint(at);
		if (!(vector.z <= 0f))
		{
			if (_axisLabelStyle == null)
			{
				_axisLabelStyle = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontSize = 22,
					fontStyle = FontStyle.Bold
				};
				_axisLabelStyle.normal.textColor = Color.white;
			}
			Rect position = new Rect(vector.x - 20f, (float)Screen.height - vector.y - 15f, 40f, 30f);
			Color color2 = GUI.color;
			GUI.color = new Color(0f, 0f, 0f, 0.8f);
			GUI.Label(new Rect(position.x + 1.5f, position.y + 1.5f, position.width, position.height), label, _axisLabelStyle);
			GUI.color = new Color(color.r, color.g, color.b, 1f);
			GUI.Label(position, label, _axisLabelStyle);
			GUI.color = color2;
		}
	}
}
