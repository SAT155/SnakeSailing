#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SnakeSmartHammer;

internal static class PlanBuildFile
{
	private enum Section
	{
		Pieces,
		Other
	}

	public const string VBuildExtension = ".vbuild";

	private const string NameHeader = "#Name:";

	public static bool ParseBlueprint(string text, string fallbackName, out Blueprint blueprint, out string error)
	{
		blueprint = null;
		string text2 = null;
		List<BlueprintItem> list = new List<BlueprintItem>();
		Section section = Section.Pieces;
		string[] array = (text ?? "").Split(new char[1] { '\n' });
		for (int i = 0; i < array.Length; i++)
		{
			int num = i + 1;
			string text3 = array[i].TrimEnd(new char[1] { '\r' });
			if (text3.Trim().Length == 0)
			{
				continue;
			}
			if (text3.StartsWith("#Name:", StringComparison.Ordinal))
			{
				text2 = text3.Substring("#Name:".Length);
			}
			else if (text3.StartsWith("#", StringComparison.Ordinal))
			{
				switch (text3.Trim())
				{
				case "#Pieces":
					section = Section.Pieces;
					break;
				case "#SnapPoints":
				case "#Terrain":
					section = Section.Other;
					break;
				}
			}
			else if (section == Section.Pieces)
			{
				if (!TryParseBlueprintLine(text3, out var item, out var problem))
				{
					error = "Line " + num + ": " + problem;
					return false;
				}
				list.Add(item);
			}
		}
		return Finish(string.IsNullOrWhiteSpace(text2) ? fallbackName : text2, list, out blueprint, out error);
	}

	public static bool ParseVBuild(string text, string fallbackName, out Blueprint blueprint, out string error)
	{
		blueprint = null;
		List<BlueprintItem> list = new List<BlueprintItem>();
		string[] array = (text ?? "").Split(new char[1] { '\n' });
		for (int i = 0; i < array.Length; i++)
		{
			int num = i + 1;
			string text2 = array[i].Trim();
			if (text2.Length == 0 || text2.StartsWith("#", StringComparison.Ordinal))
			{
				continue;
			}
			string[] array2 = text2.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (array2.Length < 8)
			{
				error = "Line " + num + ": a VBuild piece line needs a name, a rotation (4 numbers) and a position (3 numbers).";
				return false;
			}
			float[] array3 = new float[7];
			for (int j = 0; j < 7; j++)
			{
				if (!TryNumber(array2[j + 1], out array3[j]))
				{
					error = "Line " + num + ": '" + array2[j + 1] + "' is not a number.";
					return false;
				}
			}
			if (!TryNormalised(new Quaternion(array3[0], array3[1], array3[2], array3[3]), out var normalised))
			{
				error = "Line " + num + ": the rotation is zero.";
				return false;
			}
			list.Add(new BlueprintItem(array2[0], new Vector3(array3[4], array3[5], array3[6]), normalised));
		}
		return Finish(fallbackName, list, out blueprint, out error);
	}

	private static bool TryParseBlueprintLine(string line, out BlueprintItem item, out string problem)
	{
		item = null;
		string[] array = line.Split(new char[1] { ';' });
		if (array.Length < 9)
		{
			problem = "a PlanBuild piece line needs a name, a category, a position (3 numbers) and a rotation (4 numbers).";
			return false;
		}
		string text = array[0].Trim();
		if (!Blueprint.IsSingleWord(text))
		{
			problem = "a piece name is one word with no spaces.";
			return false;
		}
		float[] array2 = new float[7];
		for (int i = 0; i < 7; i++)
		{
			if (!TryNumber(array[i + 2], out array2[i]))
			{
				problem = "'" + array[i + 2] + "' is not a number.";
				return false;
			}
		}
		if (!TryNormalised(new Quaternion(array2[3], array2[4], array2[5], array2[6]), out var normalised))
		{
			problem = "the rotation is zero.";
			return false;
		}
		string signText = ((array.Length > 9) ? Info(array[9]) : null);
		item = new BlueprintItem(text, new Vector3(array2[0], array2[1], array2[2]), normalised, signText);
		problem = null;
		return true;
	}

	private static bool TryNumber(string text, out float value)
	{
		if (!float.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
		{
			return false;
		}
		if (!float.IsNaN(value))
		{
			return !float.IsInfinity(value);
		}
		return false;
	}

	private static bool TryNormalised(Quaternion q, out Quaternion normalised)
	{
		float num = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
		normalised = ((num < 1E-06f) ? q : new Quaternion(q.x / num, q.y / num, q.z / num, q.w / num));
		return num >= 1E-06f;
	}

	private static string Info(string column)
	{
		if (column.Length == 0 || column == "\"\"")
		{
			return null;
		}
		if (!TryJsonString(column, out var value))
		{
			return column;
		}
		return value;
	}

	private static bool TryJsonString(string json, out string value)
	{
		value = null;
		if (json.Length < 2 || json[0] != '"' || json[json.Length - 1] != '"')
		{
			return false;
		}
		StringBuilder stringBuilder = new StringBuilder(json.Length);
		for (int i = 1; i < json.Length - 1; i++)
		{
			char c = json[i];
			switch (c)
			{
			case '"':
				return false;
			default:
				stringBuilder.Append(c);
				break;
			case '\\':
				if (++i >= json.Length - 1)
				{
					return false;
				}
				switch (json[i])
				{
				case '"':
					stringBuilder.Append('"');
					break;
				case '\\':
					stringBuilder.Append('\\');
					break;
				case '/':
					stringBuilder.Append('/');
					break;
				case 'b':
					stringBuilder.Append('\b');
					break;
				case 'f':
					stringBuilder.Append('\f');
					break;
				case 'n':
					stringBuilder.Append('\n');
					break;
				case 'r':
					stringBuilder.Append('\r');
					break;
				case 't':
					stringBuilder.Append('\t');
					break;
				case 'u':
				{
					if (i + 4 > json.Length - 2 || !int.TryParse(json.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
					{
						return false;
					}
					stringBuilder.Append((char)result);
					i += 4;
					break;
				}
				default:
					return false;
				}
				break;
			}
		}
		value = stringBuilder.ToString();
		return true;
	}

	private static bool Finish(string name, List<BlueprintItem> items, out Blueprint blueprint, out string error)
	{
		blueprint = null;
		if (items.Count == 0)
		{
			error = "The file has no pieces.";
			return false;
		}
		if (string.IsNullOrWhiteSpace(name))
		{
			name = "blueprint";
		}
		try
		{
			blueprint = new Blueprint(name, items);
		}
		catch (ArgumentException ex)
		{
			error = ex.Message;
			return false;
		}
		error = null;
		return true;
	}
}
