#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using UnityEngine;

namespace SnakeSkills.SmartBuild;

internal readonly struct PieceBounds
{
	public readonly Vector3 Min;

	public readonly Vector3 Max;

	public readonly Vector3[] SnapPoints;

	public PieceBounds(Vector3 min, Vector3 max, Vector3[] snapPoints = null)
	{
		Min = min;
		Max = max;
		SnapPoints = snapPoints;
	}
}
