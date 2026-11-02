#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using UnityEngine;

namespace SnakeSkills.SmartBuild;

internal sealed class GridLayout
{
	public readonly int Cols;

	public readonly int Rows;

	public readonly int Layers;

	private readonly GridShape _shape;

	private readonly Vector3[] _colOffset;

	private readonly Vector3[] _rowOffset;

	private readonly Vector3[] _layerOffset;

	private readonly Quaternion[] _colTurn;

	private readonly Quaternion[] _rowTurn;

	private readonly Quaternion[] _layerTurn;

	public int Count => GridMath.CellCount(Cols, Rows, Layers);

	public GridLayout(int cols, int rows, int layers, Steps steps, float rotation, float staggerX, float staggerY, float staggerZ, float rowRotation = 0f, float pivotX = 0.5f, float pivotZ = 0.5f, float staggerLayerZ = 0f, float staggerColumnY = 0f, float staggerRowY = 0f)
		: this(new GridShape
		{
			Cols = cols,
			Rows = rows,
			Layers = layers,
			Steps = steps,
			ColumnTurn = new Vector3(0f, rotation, 0f),
			RowTurn = new Vector3(0f, rowRotation, 0f),
			PivotX = pivotX,
			PivotZ = pivotZ,
			StaggerX = staggerX,
			StaggerY = staggerY,
			StaggerZ = staggerZ,
			StaggerLayerZ = staggerLayerZ,
			StaggerColumnY = staggerColumnY,
			StaggerRowY = staggerRowY
		})
	{
	}

	public GridLayout(GridShape shape)
	{
		_shape = shape;
		Cols = shape.Cols;
		Rows = shape.Rows;
		Layers = shape.Layers;
		BuildChain(shape.Cols, shape.Steps.X, shape.ColumnTurn, shape.PivotX, out _colOffset, out _colTurn);
		BuildChain(shape.Rows, shape.Steps.Z, shape.RowTurn, shape.PivotZ, out _rowOffset, out _rowTurn);
		BuildChain(shape.Layers, shape.Steps.Y, shape.LayerTurn, shape.PivotY, out _layerOffset, out _layerTurn);
	}

	private static void BuildChain(int size, Vector3 step, Vector3 turn, float p, out Vector3[] offsets, out Quaternion[] turns)
	{
		int num = Math.Abs(size);
		int num2 = Math.Sign(size);
		offsets = new Vector3[num];
		turns = new Quaternion[num];
		Quaternion quaternion = GridMath.Turn(turn);
		if (num2 < 0)
		{
			quaternion = GridMath.Inverse(quaternion);
		}
		Vector3 vector = step * (1f - p);
		Vector3 vector2 = step * p;
		Vector3 zero = Vector3.zero;
		Quaternion quaternion2 = Quaternion.identity;
		for (int i = 0; i < num; i++)
		{
			offsets[i] = zero;
			turns[i] = quaternion2;
			Quaternion quaternion3 = GridMath.Normalized(quaternion2 * quaternion);
			if (num2 > 0)
			{
				zero += quaternion2 * vector + quaternion3 * vector2;
			}
			else
			{
				zero -= quaternion3 * vector + quaternion2 * vector2;
			}
			quaternion2 = quaternion3;
		}
	}

	public void Pose(Vector3 anchor, Quaternion rotation, int index, out Vector3 position, out Quaternion cellRotation)
	{
		Cell(index, out Vector3 offset, out Quaternion turn);
		position = anchor + rotation * offset;
		cellRotation = rotation * turn;
	}

	public void Cell(int index, out Vector3 offset, out Quaternion turn)
	{
		Indices(index, out var k, out var rk, out var lk, out var col, out var row, out var layer);
		GridShape shape = _shape;
		Vector3 vector = new Vector3(shape.StaggerX * (float)row + shape.StaggerY * (float)layer, shape.StaggerColumnY * (float)col + shape.StaggerRowY * (float)row, shape.StaggerZ * (float)col + shape.StaggerLayerZ * (float)layer);
		Quaternion quaternion = _colTurn[k];
		Quaternion quaternion2 = _rowTurn[rk];
		turn = quaternion * quaternion2 * _layerTurn[lk];
		offset = _colOffset[k] + quaternion * (_rowOffset[rk] + quaternion2 * (_layerOffset[lk] + vector));
	}

	public void Cell(int index, out Vector3 offset, out float yaw)
	{
		Cell(index, out offset, out Quaternion _);
		Indices(index, out var _, out var _, out var _, out var col, out var row, out var layer);
		yaw = (float)col * _shape.ColumnTurn.y + (float)row * _shape.RowTurn.y + (float)layer * _shape.LayerTurn.y;
	}

	private void Indices(int index, out int k, out int rk, out int lk, out int col, out int row, out int layer)
	{
		int num = Math.Abs(Cols);
		int num2 = Math.Abs(Rows);
		k = index % num;
		rk = index / num % num2;
		lk = index / (num * num2);
		col = k * Math.Sign(Cols);
		row = rk * Math.Sign(Rows);
		layer = lk * Math.Sign(Layers);
	}
}
