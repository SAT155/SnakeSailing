#nullable disable
#pragma warning disable
// Ported from SnakeHammer (private SmartBuild 1.21.0 fork) into SnakeSkills. Do not publish.
using System.Collections.Generic;

namespace SnakeSmartHammer;

internal sealed class BlueprintLoadResult
{
	public readonly List<Blueprint> Blueprints = new List<Blueprint>();

	public readonly List<string> Problems = new List<string>();
}
