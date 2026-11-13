#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using UnityEngine;

namespace SnakeSmartHammer;

internal sealed class GridShape
{
	public int Cols = 1;

	public int Rows = 1;

	public int Layers = 1;

	public Steps Steps;

	public Vector3 ColumnTurn;

	public Vector3 RowTurn;

	public Vector3 LayerTurn;

	public float PivotX = 0.5f;

	public float PivotY = 0.5f;

	public float PivotZ = 0.5f;

	public float StaggerX;

	public float StaggerY;

	public float StaggerZ;

	public float StaggerLayerZ;

	public float StaggerColumnY;

	public float StaggerRowY;
}
