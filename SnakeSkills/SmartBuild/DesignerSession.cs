#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

internal static class DesignerSession
{
	private const float RefreshInterval = 0.5f;

	public const float HoldToRemoveSeconds = 0.5f;

	private static Vector3 _lastSize = DesignerBox.DefaultSize;

	private static float _nextRefresh;

	private static readonly List<Vector3> GhostPoints = new List<Vector3>();

	private static int _measuredGhost;

	private const float FreeAimReach = 50f;

	private static float _freeYaw;

	private static Piece _restorePiece;

	private static bool _restorePending;

	private const float SnapDistance = 0.5f;

	private const float SnapSpacing = 1f;

	private const int MaxSnapPoints = 4000;

	private static readonly List<Transform> NearTransforms = new List<Transform>();

	private static readonly List<Piece> NearPieces = new List<Piece>();

	private static readonly List<Vector3> NearPoints = new List<Vector3>();

	private static GameObject _snapRoot;

	private static readonly List<Transform> SnapPool = new List<Transform>();

	public static DesignerBox Box { get; private set; }

	public static bool Following { get; private set; }

	public static bool Placed
	{
		get
		{
			if (Box != null)
			{
				return !Following;
			}
			return false;
		}
	}

	public static bool Resizing { get; private set; }

	public static bool CanResize
	{
		get
		{
			if (Box != null)
			{
				if (!Following)
				{
					return Resizing;
				}
				return true;
			}
			return false;
		}
	}

	public static int Inside { get; private set; }

	public static bool FreeAiming
	{
		get
		{
			Player localPlayer = Player.m_localPlayer;
			if (Box != null && Following && localPlayer != null)
			{
				return localPlayer.m_placementGhost == null;
			}
			return false;
		}
	}

	public static void OnKey(Player player)
	{
		try
		{
			if (Box == null)
			{
				Start(player);
			}
			else if (Following)
			{
				SetDown(player);
			}
			else if (Resizing)
			{
				FinishResize(player);
			}
			else
			{
				DesignerPrompt.Show();
			}
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("designer-key", "The designer box failed: " + ex);
		}
	}

	private static void Start(Player player)
	{
		GameObject placementGhost = player.m_placementGhost;
		_freeYaw = ((placementGhost != null) ? YawOf(placementGhost.transform.rotation) : Safe.Call(() => (float)player.m_placeRotation * player.m_placeRotationDegrees, 0f, "Player.m_placeRotation"));
		Box = new DesignerBox((placementGhost != null) ? placementGhost.transform.position : player.transform.position, _freeYaw, _lastSize);
		Following = true;
		if (ModConfig.DesignerSelectsRepair.Value)
		{
			SelectRepair(player);
		}
		if (player.m_placementGhost != null)
		{
			Follow(player.m_placementGhost);
		}
		else
		{
			FreeAim(player);
		}
		string text = GridPreview.KeyName(ModConfig.KeyDesigner.Value);
		GameApi.Message(player, (player.m_placementGhost == null) ? ("Designer box: aim where its corner goes, scroll to turn it, resize it with the width / depth / height keys; click or tap " + text + " to set it down, hold " + text + " to turn it off") : ("Designer box: aim it like a piece, resize it with the width / depth / height keys, tap " + text + " (or place a piece) to set it down, hold it to turn the box off"));
	}

	private static void SetDown(Player player)
	{
		Following = false;
		RestorePiece();
		_nextRefresh = 0f;
		Refresh(player);
		GameApi.Message(player, "Designer box set down: " + Inside + ((Inside == 1) ? " piece" : " pieces") + " inside. Build in it, then " + GridPreview.KeyName(ModConfig.GridModifier.Value) + " + " + GridPreview.KeyName(ModConfig.KeyBlueprintSave.Value) + " to save");
	}

	public static void StartResize(Player player)
	{
		if (Placed)
		{
			Resizing = true;
			if (player != null)
			{
				GameApi.Message(player, "Resizing the designer box: " + GridPreview.KeyName(ModConfig.AxisX.Value) + " / " + GridPreview.KeyName(ModConfig.AxisZ.Value) + " / both with Up / Down change its width / depth / height. " + GridPreview.KeyName(ModConfig.KeyDesigner.Value) + " when done");
			}
		}
	}

	private static void FinishResize(Player player)
	{
		Resizing = false;
		_nextRefresh = 0f;
		GameApi.Message(player, "Designer box " + Box.Size.x.ToString("0") + " x " + Box.Size.y.ToString("0") + " x " + Box.Size.z.ToString("0") + " m");
	}

	public static void OnPlaced(Player player)
	{
		if (Box == null || !Following)
		{
			return;
		}
		try
		{
			SetDown(player);
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("designer-place", "Setting the designer box down failed: " + ex);
		}
	}

	public static void TurnOff(Player player)
	{
		if (Box != null)
		{
			RestorePiece();
			BlueprintSession.ForgetBox();
			Box = null;
			Following = false;
			Resizing = false;
			Inside = 0;
			if (player != null)
			{
				GameApi.Message(player, "Designer box off" + ((BlueprintSession.Count > 0) ? ", the selection is kept" : ""));
			}
		}
	}

	public static void Discard(Player player)
	{
		if (Box != null)
		{
			RestorePiece();
			BlueprintSession.Clear();
			Box = null;
			Following = false;
			Resizing = false;
			Inside = 0;
			if (player != null)
			{
				GameApi.Message(player, "Designer box discarded");
			}
		}
	}

	public static void Resize(int dx, int dy, int dz, bool big)
	{
		if (Box != null)
		{
			Box.Resize(dx, dy, dz, big ? 5f : 1f, ModConfig.DesignerMaxSize.Value);
			_lastSize = Box.Size;
		}
	}

	public static void Update(Player player)
	{
		if (_restorePending)
		{
			ApplyRestore(player);
		}
		if (Box == null)
		{
			return;
		}
		try
		{
			if (Following)
			{
				GameObject placementGhost = player.m_placementGhost;
				if (placementGhost != null)
				{
					Follow(placementGhost);
				}
				else
				{
					FreeAim(player);
				}
			}
			else if (Time.time >= _nextRefresh)
			{
				Refresh(player);
			}
		}
		catch (Exception ex)
		{
			Plugin.LogOnce("designer-update", "Updating the designer box failed: " + ex);
		}
	}

	private static void Follow(GameObject ghost)
	{
		Transform transform = ghost.transform;
		if (_measuredGhost != ghost.GetInstanceID())
		{
			Measure(ghost);
		}
		Box.Yaw = YawOf(transform.rotation);
		Box.Corner = DesignerBox.CornerFor(transform.position, Box.Yaw, transform.rotation, GhostPoints);
		if (!Keys.Held(KeyCode.LeftShift) && !Keys.Held(KeyCode.RightShift))
		{
			Box.Corner = SnapCorner(Box.Corner);
		}
	}

	private static void FreeAim(Player player)
	{
		if (!Plugin.AxisKeyHeld() && !Keys.Held(ModConfig.KeyRotation.Value))
		{
			float num = Safe.Call(() => player.m_placeRotationDegrees, 22.5f, "Player.m_placeRotationDegrees");
			_freeYaw += (float)Wheel.Take(player.m_scrollAmountThreshold) * num;
		}
		Box.Yaw = _freeYaw;
		Camera main = Camera.main;
		if (!(main == null) && Physics.Raycast(main.transform.position, main.transform.forward, out var hitInfo, 50f, player.m_placeRayMask))
		{
			Box.Corner = hitInfo.point;
			if (!Keys.Held(KeyCode.LeftShift) && !Keys.Held(KeyCode.RightShift))
			{
				Box.Corner = SnapCorner(Box.Corner);
			}
		}
	}

	private static void SelectRepair(Player player)
	{
		_restorePiece = null;
		_restorePending = false;
		Piece piece = Safe.Call(() => player.GetSelectedPiece(), null, "Player.GetSelectedPiece");
		if (piece != null && piece.m_repairPiece)
		{
			return;
		}
		Piece repair = null;
		PieceTable buildPieces = player.m_buildPieces;
		if (buildPieces != null)
		{
			foreach (GameObject piece3 in buildPieces.m_pieces)
			{
				Piece piece2 = ((piece3 != null) ? piece3.GetComponent<Piece>() : null);
				if (piece2 != null && piece2.m_repairPiece)
				{
					repair = piece2;
					break;
				}
			}
		}
		if (!(repair == null) && Safe.Call(() => player.SetSelectedPiece(repair), fallback: false, "Player.SetSelectedPiece"))
		{
			_restorePiece = piece;
		}
	}

	private static void RestorePiece()
	{
		_restorePending = _restorePiece != null;
	}

	private static void ApplyRestore(Player player)
	{
		_restorePending = false;
		Piece piece = _restorePiece;
		_restorePiece = null;
		if (piece == null)
		{
			return;
		}
		Piece piece2 = Safe.Call(() => player.GetSelectedPiece(), null, "Player.GetSelectedPiece");
		if (!(piece2 == null) && piece2.m_repairPiece)
		{
			Safe.Call(() => player.SetSelectedPiece(piece), fallback: false, "Player.SetSelectedPiece");
		}
	}

	private static Vector3 SnapCorner(Vector3 corner)
	{
		NearTransforms.Clear();
		NearPieces.Clear();
		Piece.GetSnapPoints(corner, 1f, NearTransforms, NearPieces);
		if (NearTransforms.Count == 0)
		{
			return corner;
		}
		NearPoints.Clear();
		foreach (Transform nearTransform in NearTransforms)
		{
			if (nearTransform != null)
			{
				NearPoints.Add(nearTransform.position);
			}
		}
		return DesignerBox.SnapTo(corner, NearPoints, 0.5f);
	}

	public static void AppendSnapPoints(Vector3 point, float radius, List<Transform> points)
	{
		if (!Placed || points == null)
		{
			return;
		}
		List<Vector3> list = Box.SnapPointsNear(point, radius, 1f);
		if (list.Count != 0)
		{
			if (_snapRoot == null)
			{
				_snapRoot = new GameObject("SmartBuildDesignerSnapPoints");
				SnapPool.Clear();
			}
			int num = Mathf.Min(list.Count, 4000);
			while (SnapPool.Count < num)
			{
				Transform transform = new GameObject("designer box").transform;
				transform.SetParent(_snapRoot.transform, worldPositionStays: false);
				SnapPool.Add(transform);
			}
			for (int i = 0; i < num; i++)
			{
				SnapPool[i].position = list[i];
				points.Add(SnapPool[i]);
			}
		}
	}

	private static void Measure(GameObject ghost)
	{
		_measuredGhost = ghost.GetInstanceID();
		GhostPoints.Clear();
		Transform transform = ghost.transform;
		MeshFilter[] componentsInChildren = ghost.GetComponentsInChildren<MeshFilter>();
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			if (!(meshFilter.sharedMesh == null))
			{
				Bounds bounds = meshFilter.sharedMesh.bounds;
				Matrix4x4 localToWorldMatrix = meshFilter.transform.localToWorldMatrix;
				for (int j = 0; j < 8; j++)
				{
					Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((((uint)j & (true ? 1u : 0u)) != 0) ? 1 : (-1), (((uint)j & 2u) != 0) ? 1 : (-1), (((uint)j & 4u) != 0) ? 1 : (-1)));
					GhostPoints.Add(transform.InverseTransformPoint(localToWorldMatrix.MultiplyPoint3x4(point)));
				}
			}
		}
	}

	private static void Refresh(Player player)
	{
		_nextRefresh = Time.time + 0.5f;
		Inside = BlueprintSession.SelectInBox(player, Box);
	}

	private static float YawOf(Quaternion rotation)
	{
		Vector3 vector = rotation * Vector3.forward;
		if (vector.x * vector.x + vector.z * vector.z >= 1E-06f)
		{
			return Mathf.Atan2(vector.x, vector.z) * 57.29578f;
		}
		Vector3 vector2 = rotation * Vector3.right;
		return Mathf.Atan2(0f - vector2.z, vector2.x) * 57.29578f;
	}
}
