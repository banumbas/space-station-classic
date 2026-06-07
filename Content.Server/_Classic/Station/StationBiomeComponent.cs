using Content.Shared.Parallax.Biomes;
using Robust.Shared.Prototypes;

namespace Content.Server._Classic.Station;

/// <summary>
/// Sets up the station's main grid as a planetary biome grid without merging grids at runtime.
/// </summary>
[RegisterComponent, Access(typeof(ClassicStationBiomeSystem))]
public sealed partial class ClassicStationBiomeComponent : Component
{
    [DataField(required: true)]
    public ProtoId<BiomeTemplatePrototype> Biome = "GrasslandsClassic";

    [DataField]
    public int? Seed;

    [DataField]
    public Color MapLightColor = Color.FromHex("#D8B059");

    [DataField]
    public bool RoofStationTiles = true;

    [DataField]
    public HashSet<string> StationRoofTiles = new()
    {
        "Plating",
        "Lattice",
    };

    [DataField]
    public bool DisableGridSplitting = true;
}
