#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System.Collections.Generic;

namespace SnakeSmartHammer;

internal static class GridCost
{
	public static bool CanAfford(Player player, Piece piece, int count)
	{
		if (!GameApi.IsFree(player, piece))
		{
			return Safe.Call(() => Probe(player, piece, count, -1), fallback: true, "Player.HaveRequirements");
		}
		return true;
	}

	public static string Shortage(Player player, Piece piece, int count)
	{
		Piece.Requirement[] resources = piece.m_resources;
		for (int i = 0; i < resources.Length; i++)
		{
			Piece.Requirement requirement = resources[i];
			if (!(requirement.m_resItem == null) && requirement.m_amount > 0)
			{
				int index = i;
				if (!Safe.Call(() => Probe(player, piece, count, index), fallback: true, "Player.HaveRequirements"))
				{
					return "Need " + requirement.m_amount * count + " " + GameApi.Localize(requirement.m_resItem.m_itemData.m_shared.m_name);
				}
			}
		}
		return "Not enough materials for " + count + " pieces";
	}

	public static List<Piece.Requirement> Combined(Player player, Blueprint blueprint, Dictionary<string, Piece> pieces, Dictionary<string, int> counts)
	{
		List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
		Dictionary<string, ItemDrop> dictionary = new Dictionary<string, ItemDrop>();
		HashSet<string> hashSet = new HashSet<string>();
		foreach (BlueprintItem item in blueprint.Items)
		{
			if (!hashSet.Add(item.Prefab) || !pieces.TryGetValue(item.Prefab, out var value) || !counts.TryGetValue(item.Prefab, out var value2) || value2 <= 0 || GameApi.IsFree(player, value))
			{
				continue;
			}
			Piece.Requirement[] resources = value.m_resources;
			foreach (Piece.Requirement requirement in resources)
			{
				if (!(requirement.m_resItem == null) && requirement.m_amount > 0)
				{
					string name = requirement.m_resItem.name;
					dictionary[name] = requirement.m_resItem;
					list.Add(new KeyValuePair<string, int>(name, requirement.m_amount * value2));
				}
			}
		}
		List<Piece.Requirement> list2 = new List<Piece.Requirement>();
		foreach (KeyValuePair<string, int> item2 in Blueprint.CombineAmounts(list))
		{
			list2.Add(new Piece.Requirement
			{
				m_resItem = dictionary[item2.Key],
				m_amount = item2.Value,
				m_recover = true
			});
		}
		return list2;
	}

	public static string CombinedShortage(Player player, Piece host, List<Piece.Requirement> combined)
	{
		if (host == null || combined.Count == 0)
		{
			return null;
		}
		if (Safe.Call(() => ProbeWith(player, host, combined.ToArray()), fallback: true, "Player.HaveRequirements"))
		{
			return null;
		}
		foreach (Piece.Requirement req in combined)
		{
			if (!Safe.Call(() => ProbeWith(player, host, new Piece.Requirement[1] { req }), fallback: true, "Player.HaveRequirements"))
			{
				return "Need " + req.m_amount + " " + GameApi.Localize(req.m_resItem.m_itemData.m_shared.m_name);
			}
		}
		return "Not enough materials for this blueprint";
	}

	private static bool ProbeWith(Player player, Piece host, Piece.Requirement[] requirements)
	{
		Piece.Requirement[] resources = host.m_resources;
		try
		{
			host.m_resources = requirements;
			return player.HaveRequirements(host, Player.RequirementMode.CanBuild);
		}
		finally
		{
			host.m_resources = resources;
		}
	}

	private static bool Probe(Player player, Piece piece, int count, int only)
	{
		Piece.Requirement[] resources = piece.m_resources;
		int[] array = new int[resources.Length];
		for (int i = 0; i < resources.Length; i++)
		{
			array[i] = resources[i].m_amount;
		}
		try
		{
			for (int j = 0; j < resources.Length; j++)
			{
				resources[j].m_amount = ((only < 0 || only == j) ? (array[j] * count) : 0);
			}
			return player.HaveRequirements(piece, Player.RequirementMode.CanBuild);
		}
		finally
		{
			for (int k = 0; k < resources.Length; k++)
			{
				resources[k].m_amount = array[k];
			}
		}
	}
}
