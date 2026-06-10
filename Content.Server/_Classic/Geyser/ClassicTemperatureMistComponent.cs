using Content.Shared.Damage;

namespace Content.Server._Classic.Geyser;

[RegisterComponent, Access(typeof(ClassicGeyserSystem))]
public sealed partial class ClassicTemperatureMistComponent : Component
{
    [DataField]
    public float Radius = 0.8f;

    [DataField]
    public DamageSpecifier Damage = new();

    [DataField]
    public float HeatPerSecond = 30000f;

    [DataField]
    public float TickInterval = 1f;

    [DataField]
    public bool IgnoreResistances;

    [ViewVariables]
    public TimeSpan NextTick = TimeSpan.Zero;
}
