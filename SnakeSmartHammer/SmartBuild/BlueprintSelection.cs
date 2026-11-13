#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;

namespace SnakeSmartHammer;

internal sealed class BlueprintSelection<TKey>
{
	private readonly List<TKey> _keys = new List<TKey>();

	private readonly List<WorldPiece> _pieces = new List<WorldPiece>();

	public int Count => _keys.Count;

	public IReadOnlyList<WorldPiece> Pieces => _pieces;

	public IReadOnlyList<TKey> Keys => _keys;

	public bool Contains(TKey key)
	{
		return IndexOf(key) >= 0;
	}

	private int IndexOf(TKey key)
	{
		return _keys.FindIndex((TKey existing) => EqualityComparer<TKey>.Default.Equals(existing, key));
	}

	public bool Toggle(TKey key, WorldPiece piece)
	{
		int num = IndexOf(key);
		if (num >= 0)
		{
			_keys.RemoveAt(num);
			_pieces.RemoveAt(num);
			return false;
		}
		_keys.Add(key);
		_pieces.Add(piece);
		return true;
	}

	public bool Add(TKey key, WorldPiece piece)
	{
		if (Contains(key))
		{
			return false;
		}
		_keys.Add(key);
		_pieces.Add(piece);
		return true;
	}

	public bool Remove(TKey key)
	{
		int num = IndexOf(key);
		if (num < 0)
		{
			return false;
		}
		_keys.RemoveAt(num);
		_pieces.RemoveAt(num);
		return true;
	}

	public void ToggleGroup(IList<KeyValuePair<TKey, WorldPiece>> group)
	{
		if (group == null || group.Count == 0)
		{
			return;
		}
		bool flag = true;
		foreach (KeyValuePair<TKey, WorldPiece> item in group)
		{
			if (!Contains(item.Key))
			{
				flag = false;
				break;
			}
		}
		foreach (KeyValuePair<TKey, WorldPiece> item2 in group)
		{
			bool flag2 = Contains(item2.Key);
			if (flag)
			{
				Toggle(item2.Key, item2.Value);
			}
			else if (!flag2)
			{
				Toggle(item2.Key, item2.Value);
			}
		}
	}

	public void Clear()
	{
		_keys.Clear();
		_pieces.Clear();
	}

	public Blueprint ToBlueprint(string name)
	{
		if (_pieces.Count == 0)
		{
			throw new InvalidOperationException("Select at least one piece first.");
		}
		return Blueprint.FromWorld(name, _pieces);
	}
}
