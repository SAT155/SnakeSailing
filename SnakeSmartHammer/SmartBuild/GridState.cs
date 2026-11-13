#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System;
using UnityEngine;

namespace SnakeSmartHammer;

internal static class GridState
{
	private static int _piecesPerCell = 1;

	private static string _selectedPiece;

	public static int Cols { get; private set; } = 1;


	public static int Layers { get; private set; } = 1;


	public static int Rows { get; private set; } = 1;


	public static float GapX { get; private set; }

	public static float GapY { get; private set; }

	public static float GapZ { get; private set; }

	public static Vector3 ColumnTurn { get; private set; }

	public static Vector3 RowTurn { get; private set; }

	public static Vector3 LayerTurn { get; private set; }

	public static bool HasTurn
	{
		get
		{
			if (!(ColumnTurn != Vector3.zero) && !(RowTurn != Vector3.zero))
			{
				return LayerTurn != Vector3.zero;
			}
			return true;
		}
	}

	public static Vector3 Tilt { get; private set; }

	public static bool HasTilt => Tilt != Vector3.zero;

	public static bool TiltMode { get; private set; }

	public static Quaternion TiltRotation => GridMath.Turn(Tilt);

	public static float Rotation => ColumnTurn.y;

	public static float RotationZ => RowTurn.y;

	public static float StaggerX { get; private set; }

	public static float StaggerY { get; private set; }

	public static float StaggerZ { get; private set; }

	public static float StaggerLayerZ { get; private set; }

	public static float StaggerColumnY { get; private set; }

	public static float StaggerRowY { get; private set; }

	public static int Cells => GridMath.CellCount(Cols, Rows, Layers);

	public static bool IsGrid => Cells > 1;

	public static int PiecesPerCell => _piecesPerCell;

	public static long TotalPieces => (long)Cells * (long)_piecesPerCell;

	public static bool FollowTerrain { get; private set; }

	public static bool HasStagger
	{
		get
		{
			if (StaggerX == 0f && StaggerY == 0f && StaggerZ == 0f && StaggerLayerZ == 0f && StaggerColumnY == 0f)
			{
				return StaggerRowY != 0f;
			}
			return true;
		}
	}

	public static GridAxis? RotationAxisChoice { get; private set; }

	public static GridAxis AutoRotationAxis
	{
		get
		{
			if (Cols == 1)
			{
				if (Rows == 1)
				{
					if (Layers == 1)
					{
						return GridAxis.Columns;
					}
					return GridAxis.Layers;
				}
				return GridAxis.Rows;
			}
			return GridAxis.Columns;
		}
	}

	public static GridAxis RotationAxis => RotationAxisChoice ?? AutoRotationAxis;

	public static void ToggleTiltMode()
	{
		TiltMode = !TiltMode;
	}

	public static void SetTiltMode(bool on)
	{
		TiltMode = on;
	}

	public static TurnAxis TiltTarget(bool xHeld, bool zHeld)
	{
		if (xHeld != zHeld)
		{
			if (!xHeld)
			{
				return TurnAxis.Roll;
			}
			return TurnAxis.Pitch;
		}
		return TurnAxis.Yaw;
	}

	public static void AdjustTilt(TurnAxis axis, float degrees)
	{
		SetTilt(axis, axis switch
		{
			TurnAxis.Yaw => Tilt.y, 
			TurnAxis.Pitch => Tilt.x, 
			_ => Tilt.z, 
		} + degrees);
	}

	public static void SetTilt(TurnAxis axis, float degrees)
	{
		Vector3 tilt = Tilt;
		float num = GridMath.SnapClamp(degrees, 180f);
		switch (axis)
		{
		case TurnAxis.Pitch:
			tilt.x = num;
			break;
		case TurnAxis.Yaw:
			tilt.y = num;
			break;
		default:
			tilt.z = num;
			break;
		}
		Tilt = tilt;
	}

	public static void SetPiecesPerCell(int pieces)
	{
		_piecesPerCell = Math.Max(1, pieces);
	}

	private static bool WithinPieceLimit(int cols, int rows, int layers)
	{
		return (long)GridMath.CellCount(cols, rows, layers) * (long)_piecesPerCell <= ModConfig.MaxCells.Value;
	}

	public static void SetFollowTerrain(bool on)
	{
		FollowTerrain = on;
	}

	public static void AdjustSize(int dX, int dY, int dZ)
	{
		int value = ModConfig.MaxGridSize.Value;
		int cols = GridMath.StepSigned(Cols, dX, value);
		int layers = GridMath.StepSigned(Layers, dY, value);
		int rows = GridMath.StepSigned(Rows, dZ, value);
		if (WithinPieceLimit(cols, rows, layers))
		{
			Cols = cols;
			Layers = layers;
			Rows = rows;
			ClearGapsOnSinglePieceAxes();
		}
	}

	public static void AdjustGap(float dX, float dY, float dZ)
	{
		float value = ModConfig.MaxGap.Value;
		if (Cols != 1)
		{
			GapX = GridMath.SnapClamp(GapX + dX, value);
		}
		if (Layers != 1)
		{
			GapY = GridMath.SnapClamp(GapY + dY, value);
		}
		if (Rows != 1)
		{
			GapZ = GridMath.SnapClamp(GapZ + dZ, value);
		}
	}

	public static bool HasGapAxis(int axis)
	{
		return axis switch
		{
			1 => Layers, 
			0 => Cols, 
			_ => Rows, 
		} != 1;
	}

	private static void ClearGapsOnSinglePieceAxes()
	{
		if (Cols == 1)
		{
			GapX = 0f;
		}
		if (Layers == 1)
		{
			GapY = 0f;
		}
		if (Rows == 1)
		{
			GapZ = 0f;
		}
	}

	public static void AdjustRotation(float degrees)
	{
		AdjustTurn(GridAxis.Columns, TurnAxis.Yaw, degrees);
	}

	public static void AdjustRotationZ(float degrees)
	{
		AdjustTurn(GridAxis.Rows, TurnAxis.Yaw, degrees);
	}

	public static (GridAxis axis, TurnAxis turn) RotationTarget(bool xHeld, bool zHeld)
	{
		TurnAxis item = ((xHeld == zHeld) ? TurnAxis.Yaw : ((!xHeld) ? TurnAxis.Roll : TurnAxis.Pitch));
		return (axis: RotationAxis, turn: item);
	}

	public static void CycleRotationAxis()
	{
		if (!RotationAxisChoice.HasValue)
		{
			RotationAxisChoice = GridAxis.Columns;
		}
		else if (RotationAxisChoice == GridAxis.Columns)
		{
			RotationAxisChoice = GridAxis.Rows;
		}
		else if (RotationAxisChoice.GetValueOrDefault() == GridAxis.Rows)
		{
			RotationAxisChoice = GridAxis.Layers;
		}
		else
		{
			RotationAxisChoice = null;
		}
	}

	public static bool AxisRuns(GridAxis axis)
	{
		return axis switch
		{
			GridAxis.Rows => Rows, 
			GridAxis.Columns => Cols, 
			_ => Layers, 
		} != 1;
	}

	public static string AxisLabel(GridAxis axis)
	{
		return axis switch
		{
			GridAxis.Rows => "rows (Z)", 
			GridAxis.Columns => "columns (X)", 
			_ => "layers (Y)", 
		};
	}

	public static bool AnnounceAxisChange(GridAxis before, bool beforeRan, GridAxis now)
	{
		return before != now && beforeRan;
	}

	public static void AdjustTurn(GridAxis axis, TurnAxis turn, float degrees)
	{
		SetTurn(axis, turn, Component(TurnOf(axis), turn) + degrees);
	}

	public static void SetTurn(GridAxis axis, TurnAxis turn, float degrees)
	{
		Vector3 vector = TurnOf(axis);
		float num = GridMath.SnapClamp(degrees, 90f);
		switch (turn)
		{
		case TurnAxis.Pitch:
			vector.x = num;
			break;
		case TurnAxis.Yaw:
			vector.y = num;
			break;
		default:
			vector.z = num;
			break;
		}
		switch (axis)
		{
		case GridAxis.Columns:
			ColumnTurn = vector;
			break;
		case GridAxis.Rows:
			RowTurn = vector;
			break;
		default:
			LayerTurn = vector;
			break;
		}
	}

	public static Vector3 TurnOf(GridAxis axis)
	{
		return axis switch
		{
			GridAxis.Rows => RowTurn, 
			GridAxis.Columns => ColumnTurn, 
			_ => LayerTurn, 
		};
	}

	private static float Component(Vector3 v, TurnAxis turn)
	{
		return turn switch
		{
			TurnAxis.Yaw => v.y, 
			TurnAxis.Pitch => v.x, 
			_ => v.z, 
		};
	}

	public static StaggerAxis StaggerTarget(bool xHeld, bool zHeld)
	{
		if (xHeld && zHeld)
		{
			if (Cols != 1 || Rows == 1)
			{
				return StaggerAxis.ColumnsAlongY;
			}
			return StaggerAxis.RowsAlongY;
		}
		if (xHeld)
		{
			if (Rows != 1 || Layers == 1)
			{
				return StaggerAxis.RowsAlongX;
			}
			return StaggerAxis.LayersAlongX;
		}
		if (Cols != 1 || Layers == 1)
		{
			return StaggerAxis.ColumnsAlongZ;
		}
		return StaggerAxis.LayersAlongZ;
	}

	public static void AdjustStagger(StaggerAxis axis, float delta)
	{
		switch (axis)
		{
		case StaggerAxis.RowsAlongX:
			AdjustStagger(delta, 0f, 0f);
			break;
		case StaggerAxis.LayersAlongX:
			AdjustStagger(0f, delta, 0f);
			break;
		case StaggerAxis.ColumnsAlongZ:
			AdjustStagger(0f, 0f, delta);
			break;
		case StaggerAxis.LayersAlongZ:
			StaggerLayerZ = GridMath.SnapClamp(StaggerLayerZ + delta, ModConfig.MaxStagger.Value);
			break;
		case StaggerAxis.ColumnsAlongY:
			StaggerColumnY = GridMath.SnapClamp(StaggerColumnY + delta, ModConfig.MaxStagger.Value);
			break;
		default:
			StaggerRowY = GridMath.SnapClamp(StaggerRowY + delta, ModConfig.MaxStagger.Value);
			break;
		}
	}

	public static void AdjustStagger(float dX, float dY, float dZ)
	{
		float value = ModConfig.MaxStagger.Value;
		StaggerX = GridMath.SnapClamp(StaggerX + dX, value);
		StaggerY = GridMath.SnapClamp(StaggerY + dY, value);
		StaggerZ = GridMath.SnapClamp(StaggerZ + dZ, value);
	}

	public static void SetSize(int axis, int value)
	{
		int value2 = ModConfig.MaxGridSize.Value;
		int num = ((value == 0 || value == -1) ? 1 : GridMath.ClampInt(value, -value2, value2));
		int cols = ((axis == 0) ? num : Cols);
		int layers = ((axis == 1) ? num : Layers);
		int rows = ((axis == 2) ? num : Rows);
		if (WithinPieceLimit(cols, rows, layers))
		{
			Cols = cols;
			Layers = layers;
			Rows = rows;
			ClearGapsOnSinglePieceAxes();
		}
	}

	public static void SetGap(int axis, float value)
	{
		if (HasGapAxis(axis))
		{
			float num = GridMath.ClampGap(value, ModConfig.MaxGap.Value);
			switch (axis)
			{
			case 0:
				GapX = num;
				break;
			case 1:
				GapY = num;
				break;
			default:
				GapZ = num;
				break;
			}
		}
	}

	public static void SetRotation(float degrees)
	{
		SetTurn(GridAxis.Columns, TurnAxis.Yaw, degrees);
	}

	public static void SetRotationZ(float degrees)
	{
		SetTurn(GridAxis.Rows, TurnAxis.Yaw, degrees);
	}

	public static void SetStagger(int axis, float value)
	{
		float num = GridMath.SnapClamp(value, ModConfig.MaxStagger.Value);
		switch (axis)
		{
		case 0:
			StaggerX = num;
			break;
		case 1:
			StaggerY = num;
			break;
		case 2:
			StaggerZ = num;
			break;
		case 3:
			StaggerLayerZ = num;
			break;
		case 4:
			StaggerColumnY = num;
			break;
		default:
			StaggerRowY = num;
			break;
		}
	}

	public static Steps StepsFor(Footprint fp)
	{
		Steps result = default(Steps);
		result.X = GridMath.Step(fp.SpanX, GapX);
		result.Y = GridMath.Step(fp.SpanY, GapY);
		result.Z = GridMath.Step(fp.SpanZ, GapZ);
		return result;
	}

	public static GridLayout LayoutFor(Footprint fp)
	{
		return new GridLayout(new GridShape
		{
			Cols = Cols,
			Rows = Rows,
			Layers = Layers,
			Steps = StepsFor(fp),
			ColumnTurn = ColumnTurn,
			RowTurn = RowTurn,
			LayerTurn = LayerTurn,
			PivotX = GridMath.PivotFraction(fp.MinX, fp.MaxX),
			PivotY = GridMath.PivotFraction(fp.MinY, fp.MaxY),
			PivotZ = GridMath.PivotFraction(fp.MinZ, fp.MaxZ),
			StaggerX = StaggerX,
			StaggerY = StaggerY,
			StaggerZ = StaggerZ,
			StaggerLayerZ = StaggerLayerZ,
			StaggerColumnY = StaggerColumnY,
			StaggerRowY = StaggerRowY
		});
	}

	public static void NoteSelectedPiece(string pieceName)
	{
		if (_selectedPiece != null && _selectedPiece != pieceName)
		{
			ResetAll();
		}
		_selectedPiece = pieceName;
	}

	public static void ResetAll()
	{
		Reset();
		FollowTerrain = false;
		TiltMode = false;
		RotationAxisChoice = null;
	}

	public static void Reset()
	{
		Cols = 1;
		Layers = 1;
		Rows = 1;
		GapX = (GapY = (GapZ = 0f));
		ColumnTurn = (RowTurn = (LayerTurn = (Tilt = Vector3.zero)));
		StaggerX = (StaggerY = (StaggerZ = (StaggerLayerZ = (StaggerColumnY = (StaggerRowY = 0f)))));
	}
}
