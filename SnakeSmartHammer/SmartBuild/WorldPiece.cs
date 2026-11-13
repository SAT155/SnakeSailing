#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using UnityEngine;

namespace SnakeSmartHammer;

internal readonly struct WorldPiece
{
	public readonly string Prefab;

	public readonly Vector3 Position;

	public readonly Quaternion Rotation;

	public readonly string SignText;

	public readonly int? DoorState;

	public WorldPiece(string prefab, Vector3 position, Quaternion rotation, string signText = null, int? doorState = null)
	{
		Prefab = prefab;
		Position = position;
		Rotation = rotation;
		SignText = signText;
		DoorState = doorState;
	}
}
