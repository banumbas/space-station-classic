using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Railroading;

[RegisterComponent]
public sealed partial class RailroadFaxOnChosenComponent : Component
{
    [DataField(required: true)]
    public List<LocId> Text = [];

    [DataField]
    public bool PlaySound = true;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public Color? Color;
}
