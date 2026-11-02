#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

internal static class BlueprintFile
{
	public const string Extension = ".blueprint";

	private const string Header = "smartbuild-blueprint";

	private const int Version = 2;

	private const int MaxFileNameStem = 100;

	private static readonly char[] UnsafeFileNameCharacters = new char[9] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

	private static readonly string[] ReservedNames = new string[22]
	{
		"CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6",
		"COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7",
		"LPT8", "LPT9"
	};

	private static string Number(float value)
	{
		return value.ToString("G9", CultureInfo.InvariantCulture);
	}

	public static string Serialize(Blueprint blueprint)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("smartbuild-blueprint").Append(' ').Append(2)
			.Append('\n');
		stringBuilder.Append("name ").Append(blueprint.Name).Append('\n');
		foreach (BlueprintItem item in blueprint.Items)
		{
			stringBuilder.Append("item ").Append(item.Prefab).Append(' ')
				.Append(Number(item.Position.x))
				.Append(' ')
				.Append(Number(item.Position.y))
				.Append(' ')
				.Append(Number(item.Position.z))
				.Append(' ')
				.Append(Number(item.Rotation.x))
				.Append(' ')
				.Append(Number(item.Rotation.y))
				.Append(' ')
				.Append(Number(item.Rotation.z))
				.Append(' ')
				.Append(Number(item.Rotation.w))
				.Append('\n');
			if (item.SignText != null)
			{
				stringBuilder.Append("sign ").Append(Escape(item.SignText)).Append('\n');
			}
			if (item.DoorState.HasValue)
			{
				stringBuilder.Append("door ").Append(item.DoorState.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
			}
		}
		return stringBuilder.ToString();
	}

	private static string Escape(string text)
	{
		return text.Replace("\\", "\\\\").Replace("\n", "\\n");
	}

	private static string Unescape(string text)
	{
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] == '\\' && i + 1 < text.Length && (text[i + 1] == '\\' || text[i + 1] == 'n'))
			{
				stringBuilder.Append((text[i + 1] == 'n') ? '\n' : '\\');
				i++;
			}
			else
			{
				stringBuilder.Append(text[i]);
			}
		}
		return stringBuilder.ToString();
	}

	public static bool TryParse(string text, string fileName, out Blueprint blueprint, out string error)
	{
		blueprint = null;
		try
		{
			if (FirstLine(text).StartsWith("smartbuild-blueprint", StringComparison.Ordinal))
			{
				return Parse(text, out blueprint, out error);
			}
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName ?? "");
			if (string.Equals(Path.GetExtension(fileName ?? ""), ".vbuild", StringComparison.OrdinalIgnoreCase))
			{
				return PlanBuildFile.ParseVBuild(text, fileNameWithoutExtension, out blueprint, out error);
			}
			return PlanBuildFile.ParseBlueprint(text, fileNameWithoutExtension, out blueprint, out error);
		}
		catch (Exception ex)
		{
			blueprint = null;
			error = "Unexpected problem reading the file: " + ex.Message;
			return false;
		}
	}

	private static string FirstLine(string text)
	{
		if (text == null)
		{
			return "";
		}
		string[] array = text.Split(new char[1] { '\n' });
		foreach (string text2 in array)
		{
			if (text2.Trim().Length > 0)
			{
				return text2.Trim();
			}
		}
		return "";
	}

	public static bool TryParse(string text, out Blueprint blueprint, out string error)
	{
		blueprint = null;
		try
		{
			return Parse(text, out blueprint, out error);
		}
		catch (Exception ex)
		{
			blueprint = null;
			error = "Unexpected problem reading the file: " + ex.Message;
			return false;
		}
	}

	private static bool Parse(string text, out Blueprint blueprint, out string error)
	{
		blueprint = null;
		if (string.IsNullOrWhiteSpace(text))
		{
			error = "The file is empty.";
			return false;
		}
		string[] array = text.Split(new char[1] { '\n' });
		int result = 0;
		string text2 = null;
		List<BlueprintItem> list = new List<BlueprintItem>();
		bool flag = false;
		bool flag2 = false;
		for (int i = 0; i < array.Length; i++)
		{
			int num = i + 1;
			string text3 = array[i].TrimEnd(new char[1] { '\r' });
			if (text3.Trim().Length == 0 || text3.TrimStart(Array.Empty<char>()).StartsWith("#", StringComparison.Ordinal))
			{
				continue;
			}
			if (result == 0)
			{
				string[] array2 = text3.Trim().Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (array2.Length == 0 || array2[0] != "smartbuild-blueprint")
				{
					error = "Line " + num + ": this is not a SmartBuild blueprint file.";
					return false;
				}
				if (array2.Length != 2 || !int.TryParse(array2[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
				{
					error = "Line " + num + ": the file version is missing.";
					return false;
				}
				if (result != 1 && result != 2)
				{
					error = "Line " + num + ": file version " + result + " is not supported (this SmartBuild reads versions 1 and " + 2 + ").";
					return false;
				}
			}
			else if (text3.StartsWith("name ", StringComparison.Ordinal))
			{
				if (text2 != null)
				{
					error = "Line " + num + ": the name is given twice.";
					return false;
				}
				text2 = text3.Substring(5);
			}
			else if (text3.StartsWith("item ", StringComparison.Ordinal))
			{
				if (!TryParseItem(text3, out var item))
				{
					error = "Line " + num + ": a piece line needs a name and 7 finite numbers.";
					return false;
				}
				list.Add(item);
				flag = (flag2 = false);
			}
			else if (result >= 2 && (text3 == "sign" || text3.StartsWith("sign ", StringComparison.Ordinal)))
			{
				if (list.Count == 0)
				{
					error = "Line " + num + ": sign text needs a piece line above it.";
					return false;
				}
				if (flag)
				{
					error = "Line " + num + ": this piece already has sign text.";
					return false;
				}
				list[list.Count - 1] = WithExtras(list[list.Count - 1], Unescape((text3.Length > 5) ? text3.Substring(5) : ""), list[list.Count - 1].DoorState);
				flag = true;
			}
			else
			{
				if (result < 2 || !text3.StartsWith("door ", StringComparison.Ordinal))
				{
					error = "Line " + num + ": not understood.";
					return false;
				}
				if (list.Count == 0)
				{
					error = "Line " + num + ": a door state needs a piece line above it.";
					return false;
				}
				if (flag2)
				{
					error = "Line " + num + ": this piece already has a door state.";
					return false;
				}
				if (!int.TryParse(text3.Substring(5).Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result2))
				{
					error = "Line " + num + ": a door state is a whole number.";
					return false;
				}
				list[list.Count - 1] = WithExtras(list[list.Count - 1], list[list.Count - 1].SignText, result2);
				flag2 = true;
			}
		}
		if (result == 0)
		{
			error = "The file has no header.";
			return false;
		}
		if (string.IsNullOrWhiteSpace(text2))
		{
			error = "The file has no name.";
			return false;
		}
		if (list.Count == 0)
		{
			error = "The file has no pieces.";
			return false;
		}
		try
		{
			blueprint = new Blueprint(text2, list);
		}
		catch (ArgumentException ex)
		{
			error = ex.Message;
			return false;
		}
		if (result == 1)
		{
			blueprint = blueprint.Rebased();
		}
		error = null;
		return true;
	}

	private static BlueprintItem WithExtras(BlueprintItem item, string signText, int? doorState)
	{
		return new BlueprintItem(item.Prefab, item.Position, item.Rotation, signText, doorState);
	}

	private static bool TryParseItem(string line, out BlueprintItem item)
	{
		item = null;
		string[] array = line.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length != 9)
		{
			return false;
		}
		float[] array2 = new float[7];
		for (int i = 0; i < 7; i++)
		{
			if (!float.TryParse(array[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out array2[i]))
			{
				return false;
			}
			if (float.IsNaN(array2[i]) || float.IsInfinity(array2[i]))
			{
				return false;
			}
		}
		item = new BlueprintItem(array[1], new Vector3(array2[0], array2[1], array2[2]), new Quaternion(array2[3], array2[4], array2[5], array2[6]));
		return true;
	}

	public static string FileNameFor(string name)
	{
		string text = name ?? "";
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		string text2 = text;
		foreach (char c in text2)
		{
			stringBuilder.Append((c < ' ' || Array.IndexOf(UnsafeFileNameCharacters, c) >= 0) ? '_' : c);
		}
		text = stringBuilder.ToString();
		if (text.Length > 100)
		{
			text = text.Substring(0, 100);
		}
		text = text.Trim().TrimEnd('.', ' ');
		if (text.Length == 0)
		{
			text = "blueprint";
		}
		if (Array.IndexOf(ReservedNames, text.ToUpperInvariant()) >= 0)
		{
			text += "_";
		}
		return text + ".blueprint";
	}
}
