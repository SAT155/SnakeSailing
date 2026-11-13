#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SnakeSmartHammer;

internal sealed class Blueprint
{
	private readonly List<BlueprintItem> _items;

	private const float SharedSnapDistance = 0.01f;

	private const float DefaultLabelTolerance = 0.05f;

	public string Name { get; }

	public IReadOnlyList<BlueprintItem> Items => _items;

	public Blueprint(string name, IEnumerable<BlueprintItem> items)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new ArgumentException("A blueprint needs a name.", "name");
		}
		if (name.IndexOf('\n') >= 0 || name.IndexOf('\r') >= 0)
		{
			throw new ArgumentException("A blueprint name is one line.", "name");
		}
		if (items == null)
		{
			throw new ArgumentException("A blueprint needs at least one item.", "items");
		}
		_items = new List<BlueprintItem>(items);
		if (_items.Count == 0)
		{
			throw new ArgumentException("A blueprint needs at least one item.", "items");
		}
		foreach (BlueprintItem item in _items)
		{
			if (item == null || !IsSingleWord(item.Prefab))
			{
				throw new ArgumentException("A piece name is one word with no spaces.", "items");
			}
		}
		Name = name;
	}

	internal static bool IsSingleWord(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		for (int i = 0; i < text.Length; i++)
		{
			if (char.IsWhiteSpace(text[i]))
			{
				return false;
			}
		}
		return true;
	}

	public IEnumerable<string> PrefabsByUsage()
	{
		Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
		List<string> list = new List<string>();
		foreach (BlueprintItem item in _items)
		{
			if (!counts.ContainsKey(item.Prefab))
			{
				list.Add(item.Prefab);
			}
			counts.TryGetValue(item.Prefab, out var value);
			counts[item.Prefab] = value + 1;
		}
		return list.OrderByDescending((string name) => counts[name]);
	}

	private static Quaternion Inverse(Quaternion q)
	{
		float num = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
		return new Quaternion((0f - q.x) / num, (0f - q.y) / num, (0f - q.z) / num, q.w / num);
	}

	public static Blueprint FromWorld(string name, IList<WorldPiece> pieces)
	{
		Vector3 anchorPosition;
		Quaternion anchorRotation;
		return FromWorld(name, pieces, out anchorPosition, out anchorRotation);
	}

	public static Blueprint FromWorld(string name, IList<WorldPiece> pieces, out Vector3 anchorPosition, out Quaternion anchorRotation)
	{
		if (pieces == null || pieces.Count == 0)
		{
			throw new ArgumentException("A blueprint needs at least one piece.", "pieces");
		}
		WorldPiece worldPiece = pieces[0];
		Quaternion quaternion = FacingOf(worldPiece.Rotation);
		Quaternion quaternion2 = Inverse(quaternion);
		Vector3[] array = new Vector3[pieces.Count];
		Vector3 vector = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		for (int i = 0; i < pieces.Count; i++)
		{
			array[i] = quaternion2 * (pieces[i].Position - worldPiece.Position);
			vector = new Vector3(Mathf.Min(vector.x, array[i].x), Mathf.Min(vector.y, array[i].y), Mathf.Min(vector.z, array[i].z));
		}
		List<BlueprintItem> list = new List<BlueprintItem>(pieces.Count);
		for (int j = 0; j < pieces.Count; j++)
		{
			WorldPiece worldPiece2 = pieces[j];
			list.Add(new BlueprintItem(worldPiece2.Prefab, array[j] - vector, quaternion2 * worldPiece2.Rotation, worldPiece2.SignText, worldPiece2.DoorState));
		}
		anchorPosition = worldPiece.Position + quaternion * vector;
		anchorRotation = quaternion;
		return new Blueprint(name, list);
	}

	private static Quaternion FacingOf(Quaternion rotation)
	{
		Vector3 vector = rotation * Vector3.forward;
		float degrees;
		if (Mathf.Sqrt(vector.x * vector.x + vector.z * vector.z) >= 0.001f)
		{
			degrees = Mathf.Atan2(vector.x, vector.z) * 57.29578f;
		}
		else
		{
			Vector3 vector2 = rotation * Vector3.right;
			degrees = Mathf.Atan2(0f - vector2.z, vector2.x) * 57.29578f;
		}
		return GridMath.Yaw(degrees);
	}

	public Blueprint Rebased()
	{
		List<WorldPiece> list = new List<WorldPiece>(_items.Count);
		foreach (BlueprintItem item in _items)
		{
			list.Add(new WorldPiece(item.Prefab, item.Position, item.Rotation, item.SignText, item.DoorState));
		}
		return FromWorld(Name, list);
	}

	public int[] PlacementOrder()
	{
		int[] array = new int[_items.Count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = i;
		}
		Array.Sort(array, delegate(int a, int b)
		{
			Vector3 position = _items[a].Position;
			Vector3 position2 = _items[b].Position;
			int num = position.y.CompareTo(position2.y);
			if (num == 0)
			{
				num = position.x.CompareTo(position2.x);
			}
			if (num == 0)
			{
				num = position.z.CompareTo(position2.z);
			}
			return (num == 0) ? a.CompareTo(b) : num;
		});
		return array;
	}

	public Vector3[] OuterSnapPoints(Func<string, PieceBounds> localBounds)
	{
		if (localBounds == null)
		{
			throw new ArgumentNullException("localBounds");
		}
		Vector3[] array = PooledSnapPoints(localBounds);
		Dictionary<(long, long, long), List<int>> dictionary = new Dictionary<(long, long, long), List<int>>();
		for (int i = 0; i < array.Length; i++)
		{
			(long, long, long) key = BucketOf(array[i]);
			if (!dictionary.TryGetValue(key, out var value))
			{
				value = (dictionary[key] = new List<int>());
			}
			value.Add(i);
		}
		List<Vector3> list2 = new List<Vector3>();
		for (int j = 0; j < array.Length; j++)
		{
			if (!HasNeighbour(array, dictionary, j))
			{
				list2.Add(array[j]);
			}
		}
		return list2.ToArray();
	}

	private static (long, long, long) BucketOf(Vector3 point)
	{
		return ((long)Math.Floor(point.x / 0.01f), (long)Math.Floor(point.y / 0.01f), (long)Math.Floor(point.z / 0.01f));
	}

	private static bool HasNeighbour(Vector3[] points, Dictionary<(long, long, long), List<int>> buckets, int index)
	{
		(long, long, long) tuple = BucketOf(points[index]);
		long item = tuple.Item1;
		long item2 = tuple.Item2;
		long item3 = tuple.Item3;
		for (long num = -1L; num <= 1; num++)
		{
			for (long num2 = -1L; num2 <= 1; num2++)
			{
				for (long num3 = -1L; num3 <= 1; num3++)
				{
					if (!buckets.TryGetValue((item + num, item2 + num2, item3 + num3), out var value))
					{
						continue;
					}
					foreach (int item4 in value)
					{
						if (item4 != index && (points[item4] - points[index]).magnitude <= 0.01f)
						{
							return true;
						}
					}
				}
			}
		}
		return false;
	}

	public void PoseOf(int index, Vector3 anchorPosition, Quaternion anchorRotation, out Vector3 position, out Quaternion rotation)
	{
		if (index < 0 || index >= _items.Count)
		{
			throw new ArgumentOutOfRangeException("index");
		}
		BlueprintItem blueprintItem = _items[index];
		position = anchorPosition + anchorRotation * blueprintItem.Position;
		rotation = anchorRotation * blueprintItem.Rotation;
	}

	public static List<KeyValuePair<string, int>> CombineAmounts(IEnumerable<KeyValuePair<string, int>> parts)
	{
		List<string> list = new List<string>();
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (KeyValuePair<string, int> part in parts)
		{
			if (part.Value != 0)
			{
				if (!dictionary.TryGetValue(part.Key, out var value))
				{
					list.Add(part.Key);
				}
				dictionary[part.Key] = value + part.Value;
			}
		}
		List<KeyValuePair<string, int>> list2 = new List<KeyValuePair<string, int>>(list.Count);
		foreach (string item in list)
		{
			list2.Add(new KeyValuePair<string, int>(item, dictionary[item]));
		}
		return list2;
	}

	public Dictionary<string, int> PieceCounts(int copies)
	{
		if (copies < 1)
		{
			throw new ArgumentOutOfRangeException("copies", "A count needs at least one repeat.");
		}
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (BlueprintItem item in _items)
		{
			dictionary.TryGetValue(item.Prefab, out var value);
			dictionary[item.Prefab] = value + copies;
		}
		return dictionary;
	}

	public List<string> MissingPieces(Func<string, bool> isAvailable)
	{
		if (isAvailable == null)
		{
			throw new ArgumentNullException("isAvailable");
		}
		List<string> list = new List<string>();
		foreach (BlueprintItem item in _items)
		{
			if (!list.Contains(item.Prefab) && !isAvailable(item.Prefab))
			{
				list.Add(item.Prefab);
			}
		}
		return list;
	}

	public (Vector3 Min, Vector3 Max) LocalBounds(Func<string, PieceBounds> localBounds)
	{
		if (localBounds == null)
		{
			throw new ArgumentNullException("localBounds");
		}
		Vector3 item = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		Vector3 item2 = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		foreach (BlueprintItem item3 in _items)
		{
			PieceBounds pieceBounds = localBounds(item3.Prefab);
			for (int i = 0; i < 8; i++)
			{
				Vector3 vector = new Vector3(((i & 1) == 0) ? pieceBounds.Min.x : pieceBounds.Max.x, ((i & 2) == 0) ? pieceBounds.Min.y : pieceBounds.Max.y, ((i & 4) == 0) ? pieceBounds.Min.z : pieceBounds.Max.z);
				Vector3 vector2 = item3.Position + item3.Rotation * vector;
				item = new Vector3(Mathf.Min(item.x, vector2.x), Mathf.Min(item.y, vector2.y), Mathf.Min(item.z, vector2.z));
				item2 = new Vector3(Mathf.Max(item2.x, vector2.x), Mathf.Max(item2.y, vector2.y), Mathf.Max(item2.z, vector2.z));
			}
		}
		return (Min: item, Max: item2);
	}

	public Vector3 Extent(Func<string, PieceBounds> localBounds)
	{
		(Vector3 Min, Vector3 Max) tuple = LocalBounds(localBounds);
		var (vector, _) = tuple;
		return tuple.Max - vector;
	}

	public static string SnapPointLabel(Vector3 point, Vector3 min, Vector3 max, float tolerance = 0.05f)
	{
		List<string> list = new List<string>(3);
		if (Mathf.Abs(point.y - min.y) <= tolerance)
		{
			list.Add("bottom");
		}
		else if (Mathf.Abs(point.y - max.y) <= tolerance)
		{
			list.Add("top");
		}
		if (Mathf.Abs(point.z - min.z) <= tolerance)
		{
			list.Add("front");
		}
		else if (Mathf.Abs(point.z - max.z) <= tolerance)
		{
			list.Add("back");
		}
		if (Mathf.Abs(point.x - min.x) <= tolerance)
		{
			list.Add("left");
		}
		else if (Mathf.Abs(point.x - max.x) <= tolerance)
		{
			list.Add("right");
		}
		if (list.Count <= 0)
		{
			return "snappoint";
		}
		return string.Join("-", list);
	}

	public Steps FootprintSteps(Func<string, PieceBounds> localBounds)
	{
		Vector3 vector = Extent(localBounds);
		Vector3[] originPoints = OriginSnapPoints(localBounds);
		Vector3[] pooledPoints = PooledSnapPoints(localBounds);
		Steps result = default(Steps);
		result.X = ProjectedSpan(originPoints, pooledPoints, 0) ?? new Vector3(vector.x, 0f, 0f);
		result.Y = ProjectedSpan(originPoints, pooledPoints, 1) ?? new Vector3(0f, vector.y, 0f);
		result.Z = ProjectedSpan(originPoints, pooledPoints, 2) ?? new Vector3(0f, 0f, vector.z);
		return result;
	}

	private static Vector3? ProjectedSpan(Vector3[] originPoints, Vector3[] pooledPoints, int axis)
	{
		Vector3? result = Footprint.SnapSpan(originPoints, axis);
		if (!result.HasValue || pooledPoints.Length == 0)
		{
			return result;
		}
		Vector3 normalized = result.Value.normalized;
		float num = float.MaxValue;
		float num2 = float.MinValue;
		for (int i = 0; i < pooledPoints.Length; i++)
		{
			float b = Vector3.Dot(pooledPoints[i], normalized);
			num = Mathf.Min(num, b);
			num2 = Mathf.Max(num2, b);
		}
		return normalized * (num2 - num);
	}

	private Vector3[] OriginSnapPoints(Func<string, PieceBounds> localBounds)
	{
		if (_items.Count == 0)
		{
			return Array.Empty<Vector3>();
		}
		BlueprintItem blueprintItem = _items[0];
		Vector3[] snapPoints = localBounds(blueprintItem.Prefab).SnapPoints;
		if (snapPoints == null)
		{
			return Array.Empty<Vector3>();
		}
		Vector3[] array = new Vector3[snapPoints.Length];
		for (int i = 0; i < snapPoints.Length; i++)
		{
			array[i] = blueprintItem.Position + blueprintItem.Rotation * snapPoints[i];
		}
		return array;
	}

	private Vector3[] PooledSnapPoints(Func<string, PieceBounds> localBounds)
	{
		List<Vector3> list = new List<Vector3>();
		foreach (BlueprintItem item in _items)
		{
			Vector3[] snapPoints = localBounds(item.Prefab).SnapPoints;
			if (snapPoints != null)
			{
				Vector3[] array = snapPoints;
				foreach (Vector3 vector in array)
				{
					list.Add(item.Position + item.Rotation * vector);
				}
			}
		}
		return list.ToArray();
	}
}
