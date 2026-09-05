// Namespace does not match folder structure
#pragma warning disable IDE0130
namespace Content.Shared.Maps;

public sealed partial class ContentTileDefinition
{
    /// <summary>
    /// Marks naturally generated terrain. Classic mining and structural systems use this
    /// independently from <see cref="Indestructible"/>, so geology can be excavated without
    /// being treated as ordinary player-built flooring.
    /// </summary>
    [DataField]
    public bool NaturalTerrain;
}
