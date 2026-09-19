using Content.Server._Classic.GameTicking.Rules;
using Robust.Shared.Utility;

namespace Content.Server._Classic.GameTicking.Rules.Components;

/// <summary>
/// Merges a map file into an existing map in a Classic station Z-network when the game rule is added.
/// The loaded grids are reported through <see cref="Content.Server.GameTicking.Rules.RuleLoadedGridsEvent"/>.
/// </summary>
[RegisterComponent, Access(typeof(ClassicMergeMapRuleSystem))]
public sealed partial class ClassicMergeMapRuleComponent : Component
{
    /// <summary>
    /// Map file whose contents will be merged into the target Z-level.
    /// </summary>
    [DataField(required: true)]
    public ResPath MapPath;

    /// <summary>
    /// Depth of the existing Classic Z-level that receives the map contents.
    /// </summary>
    [DataField(required: true)]
    public int TargetDepth;

    /// <summary>
    /// When set, generates permanent biome terrain on this Z-depth underneath the merged grids.
    /// </summary>
    [DataField]
    public int? BiomeGroundDepth;
}
