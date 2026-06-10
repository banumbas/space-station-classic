namespace Content.Server._Classic.Geyser;

[RegisterComponent, Access(typeof(ClassicGeyserSystem))]
public sealed partial class ClassicGeyserGeneratorComponent : Component
{
    [DataField]
    public float IdleSupply;

    [ViewVariables]
    public TimeSpan PoweredUntil = TimeSpan.Zero;

    [ViewVariables]
    public bool PoweredByGeyser;
}
