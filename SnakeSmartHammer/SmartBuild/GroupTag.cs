#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using UnityEngine;

namespace SnakeSmartHammer;

internal static class GroupTag
{
	private const string Key = "smartbuild_group";

	private static int _hash;

	private static int Hash
	{
		get
		{
			if (_hash == 0)
			{
				return _hash = "smartbuild_group".GetStableHashCode();
			}
			return _hash;
		}
	}

	public static void Apply(GameObject piece, long groupId)
	{
		if (groupId == 0L)
		{
			return;
		}
		Safe.Run(delegate
		{
			ZNetView component = piece.GetComponent<ZNetView>();
			if (component != null && component.IsValid())
			{
				component.GetZDO().Set(Hash, groupId);
			}
		}, "ZDO.Set");
	}

	public static long Read(Piece piece)
	{
		return Safe.Call(delegate
		{
			ZNetView component = piece.GetComponent<ZNetView>();
			return (!(component != null) || !component.IsValid()) ? 0 : component.GetZDO().GetLong(Hash, 0L);
		}, 0L, "ZDO.GetLong");
	}
}
