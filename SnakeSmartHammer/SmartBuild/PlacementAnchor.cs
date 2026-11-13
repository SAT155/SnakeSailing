#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using UnityEngine;

namespace SnakeSmartHammer;

// Fork: the pose the preview was last drawn from. UpdatePlacementGhostPatch records it after every
// other mod has moved the ghost, and TryPlacePiecePatch puts the ghost back on it before a grid or
// blueprint is placed, so what lands is exactly what was shown, whatever moved the ghost since
// (Valheim Plus grid alignment or Advanced Building Mode, gizmo mods, ...).
internal static class PlacementAnchor
{
	private static int _ghostId;

	private static Vector3 _position;

	private static Quaternion _rotation;

	private static bool _valid;

	public static void Record(GameObject ghost)
	{
		_valid = ghost != null && ghost.activeSelf;
		if (_valid)
		{
			_ghostId = ghost.GetInstanceID();
			_position = ghost.transform.position;
			_rotation = ghost.transform.rotation;
		}
	}

	public static void Clear()
	{
		_valid = false;
	}

	// Moves the ghost back to the previewed pose. Logs how far it had moved, which names the
	// culprit when another mod keeps shifting the ghost after SmartBuild drew its preview.
	public static void Restore(GameObject ghost, string what)
	{
		if (!_valid || ghost == null || ghost.GetInstanceID() != _ghostId)
		{
			Log(what + ": no recorded preview pose for this ghost, placing at the ghost (" + Fmt(ghost != null ? ghost.transform.position : Vector3.zero) + ")");
			return;
		}
		float moved = Vector3.Distance(ghost.transform.position, _position);
		float turned = Quaternion.Angle(ghost.transform.rotation, _rotation);
		if (moved > 0.001f || turned > 0.05f)
		{
			Plugin.Log.LogWarning(what + ": the ghost moved " + moved.ToString("0.###") + " m / turned " + turned.ToString("0.##") + " deg after the preview was drawn (another mod moving it?); placing where the preview showed, " + Fmt(_position));
			ghost.transform.SetPositionAndRotation(_position, _rotation);
		}
		else
		{
			Log(what + ": placing at the previewed anchor " + Fmt(_position) + ", yaw " + _rotation.eulerAngles.y.ToString("0.##"));
		}
	}

	public static void Log(string message)
	{
		if (ModConfig.LogPlacement != null && ModConfig.LogPlacement.Value)
		{
			Plugin.Log.LogInfo(message);
		}
	}

	public static string Fmt(Vector3 v)
	{
		return "(" + v.x.ToString("0.###") + ", " + v.y.ToString("0.###") + ", " + v.z.ToString("0.###") + ")";
	}
}
