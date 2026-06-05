using Content.Server.Parallax;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Server.Physics;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server._Classic.Station;

[RegisterComponent, Access(typeof(ClassicStationBiomeSystem))]
public sealed partial class ClassicStationBiomeComponent : Component
{
    [DataField(required: true)]
    public ProtoId<BiomeTemplatePrototype> Biome = "Grasslands";

    [DataField]
    public int? Seed;

    [DataField]
    public Color MapLightColor = Color.Black;

    [DataField]
    public bool MergeStationGrid = true;
}
