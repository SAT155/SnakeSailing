#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System.Collections.Generic;
using UnityEngine;

namespace SnakeSmartHammer;

internal sealed class DesignerBox
{
	public static readonly Vector3 DefaultSize = new Vector3(8f, 4f, 8f);

	public const float Tolerance = 0.05f;

	public Vector3 Corner;

	public float Yaw;

	public Vector3 Size;

	public Quaternion Rotation => GridMath.Yaw(Yaw);

	public Vector3 Center => Corner + Rotation * (Size * 0.5f);

	public DesignerBox(Vector3 corner, float yaw, Vector3 size)
	{
		Corner = corner;
		Yaw = yaw;
		Size = size;
	}

	public void Resize(int dx, int dy, int dz, float step, float max)
	{
		Size = new Vector3(Clamp(Size.x + (float)dx * step, max), Clamp(Size.y + (float)dy * step, max), Clamp(Size.z + (float)dz * step, max));
	}

	private static float Clamp(float value, float max)
	{
		return Mathf.Clamp(value, 1f, Mathf.Max(1f, max));
	}

	public Vector3 ToLocal(Vector3 world)
	{
		return GridMath.Inverse(Rotation) * (world - Corner);
	}

	public bool Contains(Vector3 world)
	{
		Vector3 vector = ToLocal(world);
		if (vector.x >= -0.05f && vector.x <= Size.x + 0.05f && vector.y >= -0.05f && vector.y <= Size.y + 0.05f && vector.z >= -0.05f)
		{
			return vector.z <= Size.z + 0.05f;
		}
		return false;
	}

	public Vector3[] Corners()
	{
		Vector3[] array = new Vector3[8];
		Quaternion rotation = Rotation;
		for (int i = 0; i < 8; i++)
		{
			Vector3 vector = new Vector3((((uint)i & (true ? 1u : 0u)) != 0) ? Size.x : 0f, (((uint)i & 2u) != 0) ? Size.y : 0f, (((uint)i & 4u) != 0) ? Size.z : 0f);
			array[i] = Corner + rotation * vector;
		}
		return array;
	}

	public static Vector3 CornerFor(Vector3 pivot, float yaw, Quaternion rotation, IList<Vector3> localPoints)
	{
		if (localPoints == null || localPoints.Count == 0)
		{
			return pivot;
		}
		Quaternion quaternion = GridMath.Yaw(yaw);
		Quaternion quaternion2 = GridMath.Inverse(quaternion);
		Vector3 vector = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		foreach (Vector3 localPoint in localPoints)
		{
			vector = Vector3.Min(vector, quaternion2 * (rotation * localPoint));
		}
		return pivot + quaternion * vector;
	}

	public static List<int> MergeSelection(IList<int> current, ICollection<int> boxLast, HashSet<int> excluded, IList<int> insideOrdered)
	{
		HashSet<int> hashSet = new HashSet<int>(current);
		excluded.RemoveWhere(hashSet.Contains);
		foreach (int item in boxLast)
		{
			if (!hashSet.Contains(item))
			{
				excluded.Add(item);
			}
		}
		List<int> list = new List<int>();
		HashSet<int> hashSet2 = new HashSet<int>();
		foreach (int item2 in insideOrdered)
		{
			if (!excluded.Contains(item2) && hashSet2.Add(item2))
			{
				list.Add(item2);
			}
		}
		foreach (int item3 in current)
		{
			if (hashSet2.Add(item3))
			{
				list.Add(item3);
			}
		}
		return list;
	}

	public List<Vector3> SnapPointsNear(Vector3 world, float radius, float spacing)
	{
		List<Vector3> list = new List<Vector3>();
		if (radius <= 0f || spacing <= 0f)
		{
			return list;
		}
		Vector3 vector = ToLocal(world);
		float[][] array = new float[3][]
		{
			Ticks(Size.x, spacing),
			Ticks(Size.y, spacing),
			Ticks(Size.z, spacing)
		};
		float num = radius * radius;
		HashSet<long> hashSet = new HashSet<long>();
		Quaternion rotation = Rotation;
		for (int i = 0; i < 3; i++)
		{
			int num2 = (i + 1) % 3;
			int num3 = (i + 2) % 3;
			int num4 = array[i].Length - 1;
			for (int j = 0; j <= 1; j++)
			{
				int num5 = ((j != 0) ? num4 : 0);
				if (j == 1 && num4 == 0)
				{
					break;
				}
				float num6 = vector[i] - array[i][num5];
				if (num6 * num6 > num)
				{
					continue;
				}
				for (int k = 0; k < array[num2].Length; k++)
				{
					float num7 = vector[num2] - array[num2][k];
					if (num6 * num6 + num7 * num7 > num)
					{
						continue;
					}
					for (int l = 0; l < array[num3].Length; l++)
					{
						float num8 = vector[num3] - array[num3][l];
						if (!(num6 * num6 + num7 * num7 + num8 * num8 > num))
						{
							int[] array2 = new int[3];
							array2[i] = num5;
							array2[num2] = k;
							array2[num3] = l;
							if (hashSet.Add(((long)array2[0] << 40) | ((long)array2[1] << 20) | array2[2]))
							{
								Vector3 vector2 = new Vector3(array[0][array2[0]], array[1][array2[1]], array[2][array2[2]]);
								list.Add(Corner + rotation * vector2);
							}
						}
					}
				}
			}
		}
		return list;
	}

	private static float[] Ticks(float length, float spacing)
	{
		List<float> list = new List<float>();
		for (int i = 0; (float)i * spacing < length - 0.0001f; i++)
		{
			list.Add((float)i * spacing);
		}
		list.Add(length);
		return list.ToArray();
	}

	public static Vector3 SnapTo(Vector3 point, IList<Vector3> candidates, float maxDistance)
	{
		Vector3 result = point;
		float num = maxDistance * maxDistance;
		bool flag = false;
		foreach (Vector3 candidate in candidates)
		{
			float sqrMagnitude = (candidate - point).sqrMagnitude;
			if (sqrMagnitude <= num && (!flag || sqrMagnitude < num))
			{
				result = candidate;
				num = sqrMagnitude;
				flag = true;
			}
		}
		return result;
	}

	public List<int> OriginOrder(IList<Vector3> centres)
	{
		List<int> list = new List<int>(centres.Count);
		for (int i = 0; i < centres.Count; i++)
		{
			list.Add(i);
		}
		Vector3 corner = Corner;
		list.Sort(delegate(int a, int b)
		{
			int num = (centres[a] - corner).sqrMagnitude.CompareTo((centres[b] - corner).sqrMagnitude);
			return (num == 0) ? a.CompareTo(b) : num;
		});
		return list;
	}
}
