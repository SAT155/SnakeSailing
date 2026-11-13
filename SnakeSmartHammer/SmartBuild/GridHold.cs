#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using UnityEngine;

namespace SnakeSmartHammer;

internal static class GridHold
{
	private static Vector3 _position;

	private static Quaternion _rotation;

	private static int _ghostId;

	public static bool Frozen { get; private set; }

	public static void Toggle(Player player)
	{
		if (Frozen)
		{
			Release();
			GameApi.Message(player, "Blueprint released");
			return;
		}
		GameObject placementGhost = player.m_placementGhost;
		if (!(placementGhost == null) && placementGhost.activeSelf)
		{
			_position = placementGhost.transform.position;
			_rotation = placementGhost.transform.rotation * GridMath.Inverse(GridState.TiltRotation);
			_ghostId = placementGhost.GetInstanceID();
			Frozen = true;
			GameApi.Message(player, "Blueprint held (" + ModConfig.KeyHold.Value.ToString() + " again to release)");
		}
	}

	public static void Release()
	{
		Frozen = false;
	}

	public static void Nudge(Player player, int right, int forward, int up, float step)
	{
		if (!Frozen)
		{
			Toggle(player);
		}
		if (Frozen)
		{
			Camera main = Camera.main;
			Vector3 cameraForward = ((main != null) ? main.transform.forward : player.transform.forward);
			_position += GridMath.NudgeOffset(cameraForward, _rotation, right, forward, up, step);
		}
	}

	public static void Apply(Player player)
	{
		if (Frozen)
		{
			GameObject placementGhost = player.m_placementGhost;
			if (placementGhost == null || placementGhost.GetInstanceID() != _ghostId)
			{
				Frozen = false;
				return;
			}
			placementGhost.SetActive(value: true);
			placementGhost.transform.SetPositionAndRotation(_position, _rotation * GridState.TiltRotation);
		}
	}

	public static string OutOfReach(Player player)
	{
		float num = Vector3.Distance(player.transform.position, _position);
		if (num > ModConfig.HoldReach.Value)
		{
			return "Too far from the held grid (" + Mathf.RoundToInt(num) + " m, HoldReach is " + Mathf.RoundToInt(ModConfig.HoldReach.Value) + " m)";
		}
		if (Safe.Call(() => ZNetScene.instance != null && ZNetScene.instance.OutsideActiveArea(_position), fallback: false, "ZNetScene.OutsideActiveArea"))
		{
			return "The held grid is outside the loaded area (" + Mathf.RoundToInt(num) + " m away); move closer to place it";
		}
		return null;
	}
}
