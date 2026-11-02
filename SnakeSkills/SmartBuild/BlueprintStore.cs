#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SnakeSkills.SmartBuild;

internal sealed class BlueprintStore
{
	private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public string Directory { get; }

	public BlueprintStore(string directory)
	{
		Directory = directory;
	}

	private string PathFor(string name)
	{
		if (!_files.TryGetValue(name, out var value))
		{
			return Path.Combine(Directory, BlueprintFile.FileNameFor(name));
		}
		return value;
	}

	public string Save(Blueprint blueprint)
	{
		System.IO.Directory.CreateDirectory(Directory);
		string text = PathFor(blueprint.Name);
		File.WriteAllText(text, BlueprintFile.Serialize(blueprint), Utf8WithoutBom);
		_files[blueprint.Name] = text;
		return text;
	}

	public BlueprintLoadResult LoadAll()
	{
		BlueprintLoadResult blueprintLoadResult = new BlueprintLoadResult();
		_files.Clear();
		if (!System.IO.Directory.Exists(Directory))
		{
			return blueprintLoadResult;
		}
		List<string> list = new List<string>();
		string[] files = System.IO.Directory.GetFiles(Directory);
		foreach (string text in files)
		{
			if (text.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase) || text.EndsWith(".vbuild", StringComparison.OrdinalIgnoreCase))
			{
				list.Add(text);
			}
		}
		list.Sort((string a, string b) => string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase));
		foreach (string item in list)
		{
			string fileName = Path.GetFileName(item);
			string text2;
			try
			{
				text2 = File.ReadAllText(item, Encoding.UTF8);
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
				blueprintLoadResult.Problems.Add(fileName + ": could not be read (" + ex.Message + ")");
				continue;
			}
			if (BlueprintFile.TryParse(text2, fileName, out var blueprint, out var error))
			{
				blueprintLoadResult.Blueprints.Add(blueprint);
				_files[blueprint.Name] = item;
			}
			else
			{
				blueprintLoadResult.Problems.Add(fileName + ": " + error);
			}
		}
		return blueprintLoadResult;
	}

	public bool Delete(string name)
	{
		string path = PathFor(name);
		_files.Remove(name);
		if (!File.Exists(path))
		{
			return false;
		}
		File.Delete(path);
		return true;
	}
}
