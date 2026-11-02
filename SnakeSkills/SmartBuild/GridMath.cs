#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

internal static class GridMath
{
	public static int CellCount(int cols, int rows, int layers)
	{
		return Math.Abs(cols) * Math.Abs(rows) * Math.Abs(layers);
	}

	public static int StepSigned(int value, int delta, int max)
	{
		int num = ((value > 0) ? (value - 1) : (value + 1));
		num += delta;
		return ClampInt((num >= 0) ? (num + 1) : (num - 1), -max, max);
	}

	public static Vector3 Step(Vector3 span, float gap)
	{
		if (!(span.sqrMagnitude < 1E-06f))
		{
			return span + span.normalized * gap;
		}
		return span;
	}

	public static Quaternion Yaw(float degrees)
	{
		float f = degrees * (Mathf.PI / 180f) * 0.5f;
		return new Quaternion(0f, Mathf.Sin(f), 0f, Mathf.Cos(f));
	}

	public static Quaternion Pitch(float degrees)
	{
		float f = degrees * (Mathf.PI / 180f) * 0.5f;
		return new Quaternion(Mathf.Sin(f), 0f, 0f, Mathf.Cos(f));
	}

	public static Quaternion Roll(float degrees)
	{
		float f = degrees * (Mathf.PI / 180f) * 0.5f;
		return new Quaternion(0f, 0f, Mathf.Sin(f), Mathf.Cos(f));
	}

	public static Quaternion Turn(Vector3 pitchYawRoll)
	{
		return Yaw(pitchYawRoll.y) * Pitch(pitchYawRoll.x) * Roll(pitchYawRoll.z);
	}

	public static Quaternion Inverse(Quaternion q)
	{
		return new Quaternion(0f - q.x, 0f - q.y, 0f - q.z, q.w);
	}

	public static Quaternion Normalized(Quaternion q)
	{
		float num = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
		if (!(num < 1E-06f))
		{
			return new Quaternion(q.x / num, q.y / num, q.z / num, q.w / num);
		}
		return Quaternion.identity;
	}

	public static Vector3 NudgeOffset(Vector3 cameraForward, Quaternion heldRotation, int right, int forward, int up, float step)
	{
		Vector3 rhs = new Vector3(cameraForward.x, 0f, cameraForward.z);
		Vector3 vector = Flat(heldRotation * Vector3.forward);
		Vector3 vector2 = Flat(heldRotation * Vector3.right);
		Vector3[] obj = new Vector3[4]
		{
			vector,
			-vector,
			vector2,
			-vector2
		};
		Vector3 vector3 = Vector3.forward;
		float num = float.NegativeInfinity;
		Vector3[] array = obj;
		foreach (Vector3 vector4 in array)
		{
			if (!(vector4 == Vector3.zero))
			{
				float num2 = Vector3.Dot(vector4, rhs);
				if (num2 > num + 1E-05f)
				{
					num = num2;
					vector3 = vector4;
				}
			}
		}
		Vector3 vector5 = new Vector3(vector3.z, 0f, 0f - vector3.x);
		return (vector3 * forward + vector5 * right + Vector3.up * up) * step;
	}

	private static Vector3 Flat(Vector3 v)
	{
		v.y = 0f;
		float num = Mathf.Sqrt(v.x * v.x + v.z * v.z);
		if (!(num < 0.0001f))
		{
			return v / num;
		}
		return Vector3.zero;
	}

	public static float PivotFraction(float min, float max)
	{
		float num = max - min;
		if (num < 0.0001f)
		{
			return 0.5f;
		}
		float num2 = (0f - min) / num;
		if (!(num2 < 0f))
		{
			if (!(num2 > 1f))
			{
				return num2;
			}
			return 1f;
		}
		return 0f;
	}

	public static int ClampInt(int value, int min, int max)
	{
		if (value >= min)
		{
			if (value <= max)
			{
				return value;
			}
			return max;
		}
		return min;
	}

	public static float ClampGap(float value, float max)
	{
		if (!(value < 0f - max))
		{
			if (!(value > max))
			{
				return value;
			}
			return max;
		}
		return 0f - max;
	}

	public static float SnapClamp(float value, float max)
	{
		return (float)Math.Round(Mathf.Clamp(value, 0f - max, max), 3);
	}
}
