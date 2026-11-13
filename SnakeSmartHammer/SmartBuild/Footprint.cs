#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System.Collections.Generic;
using UnityEngine;

namespace SnakeSmartHammer;

internal struct Footprint
{
	private const float MinSnapSpan = 0.1f;

	private const float SnapGroupTolerance = 0.05f;

	public float MinX;

	public float MaxX;

	public float MinY;

	public float MaxY;

	public float MinZ;

	public float MaxZ;

	public Vector3 SpanX;

	public Vector3 SpanY;

	public Vector3 SpanZ;

	public int SnapCount;

	public Vector3[] Snaps;

	public float Width => MaxX - MinX;

	public float Height => MaxY - MinY;

	public float Depth => MaxZ - MinZ;

	public static Footprint Measure(GameObject ghost)
	{
		Footprint footprint = default(Footprint);
		footprint.MinX = float.MaxValue;
		footprint.MaxX = float.MinValue;
		footprint.MinY = float.MaxValue;
		footprint.MaxY = float.MinValue;
		footprint.MinZ = float.MaxValue;
		footprint.MaxZ = float.MinValue;
		Footprint result = footprint;
		bool flag = false;
		MeshFilter[] componentsInChildren = ghost.GetComponentsInChildren<MeshFilter>();
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			if (meshFilter.sharedMesh == null)
			{
				continue;
			}
			Bounds bounds = meshFilter.sharedMesh.bounds;
			Matrix4x4 localToWorldMatrix = meshFilter.transform.localToWorldMatrix;
			for (int j = 0; j < 8; j++)
			{
				Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((((uint)j & (true ? 1u : 0u)) != 0) ? 1 : (-1), (((uint)j & 2u) != 0) ? 1 : (-1), (((uint)j & 4u) != 0) ? 1 : (-1)));
				Vector3 vector = ghost.transform.InverseTransformPoint(localToWorldMatrix.MultiplyPoint3x4(point));
				if (vector.x < result.MinX)
				{
					result.MinX = vector.x;
				}
				if (vector.x > result.MaxX)
				{
					result.MaxX = vector.x;
				}
				if (vector.y < result.MinY)
				{
					result.MinY = vector.y;
				}
				if (vector.y > result.MaxY)
				{
					result.MaxY = vector.y;
				}
				if (vector.z < result.MinZ)
				{
					result.MinZ = vector.z;
				}
				if (vector.z > result.MaxZ)
				{
					result.MaxZ = vector.z;
				}
				flag = true;
			}
		}
		if (!flag)
		{
			footprint = default(Footprint);
			footprint.MinX = -1f;
			footprint.MaxX = 1f;
			footprint.MinY = 0f;
			footprint.MaxY = 1f;
			footprint.MinZ = -1f;
			footprint.MaxZ = 1f;
			result = footprint;
		}
		Vector3[] array = SnapPoints(ghost);
		result.SnapCount = array.Length;
		result.Snaps = array;
		result.SpanX = SnapSpan(array, 0) ?? new Vector3(result.Width, 0f, 0f);
		result.SpanY = SnapSpan(array, 1) ?? new Vector3(0f, result.Height, 0f);
		result.SpanZ = SnapSpan(array, 2) ?? new Vector3(0f, 0f, result.Depth);
		return result;
	}

	private static Vector3[] SnapPoints(GameObject ghost)
	{
		List<Transform> list = new List<Transform>();
		Piece component = ghost.GetComponent<Piece>();
		if (component != null)
		{
			component.GetSnapPoints(list);
		}
		Vector3[] array = new Vector3[list.Count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ghost.transform.InverseTransformPoint(list[i].position);
		}
		return array;
	}

	internal static Vector3? SnapSpan(Vector3[] points, int axis)
	{
		if (points.Length < 2)
		{
			return null;
		}
		float num = float.MaxValue;
		float num2 = float.MinValue;
		Vector3[] array = points;
		for (int i = 0; i < array.Length; i++)
		{
			Vector3 vector = array[i];
			num = Mathf.Min(num, vector[axis]);
			num2 = Mathf.Max(num2, vector[axis]);
		}
		if (num2 - num < 0.1f)
		{
			return null;
		}
		Vector3 zero = Vector3.zero;
		Vector3 zero2 = Vector3.zero;
		int num3 = 0;
		int num4 = 0;
		array = points;
		for (int i = 0; i < array.Length; i++)
		{
			Vector3 vector2 = array[i];
			if (vector2[axis] <= num + 0.05f)
			{
				zero += vector2;
				num3++;
			}
			if (vector2[axis] >= num2 - 0.05f)
			{
				zero2 += vector2;
				num4++;
			}
		}
		return zero2 / num4 - zero / num3;
	}
}
