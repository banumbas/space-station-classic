namespace Content.Server._Classic.ZCollapse;

/// <summary>
/// Keeps a floating grid aloft: when anchored, seeds the tile it stands on with
/// <see cref="LevitationForce"/> stability, which then flood-fills outward across the grid.
/// </summary>
[RegisterComponent]
public sealed partial class ClassicGridStabilityCoreComponent : Component
{
    [DataField]
    public int LevitationForce = 20;
}


