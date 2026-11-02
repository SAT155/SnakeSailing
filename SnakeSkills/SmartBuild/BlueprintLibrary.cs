#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace SnakeSkills.SmartBuild;

internal static class BlueprintLibrary
{
	private static BlueprintStore _store;

	public static readonly List<Blueprint> All = new List<Blueprint>();

	public static string Folder
	{
		get
		{
			if (_store == null)
			{
				return "";
			}
			return _store.Directory;
		}
	}

	public static void Load()
	{
		_store = new BlueprintStore(Path.Combine(Paths.ConfigPath, "SmartBuild", "Blueprints"));
		All.Clear();
		BlueprintLoadResult blueprintLoadResult = _store.LoadAll();
		All.AddRange(blueprintLoadResult.Blueprints);
		Plugin.Log.LogInfo("Loaded " + All.Count + " blueprint(s) from " + _store.Directory);
		foreach (string problem in blueprintLoadResult.Problems)
		{
			Plugin.Log.LogWarning("Skipped a blueprint file: " + problem);
		}
	}

	public static void Save(Blueprint blueprint, Player player)
	{
		_store.Save(blueprint);
		All.RemoveAll((Blueprint existing) => string.Equals(existing.Name, blueprint.Name, StringComparison.OrdinalIgnoreCase));
		All.Add(blueprint);
		All.Sort((Blueprint a, Blueprint b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
		BlueprintPieces.Register(blueprint, player);
	}

	public static bool Delete(string name)
	{
		bool result = _store.Delete(name);
		All.RemoveAll((Blueprint existing) => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase));
		BlueprintPieces.Unregister(name);
		return result;
	}
}
