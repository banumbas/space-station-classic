using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Shaders.Normal;

[RegisterComponent]
public sealed partial class LightPositionForwarderComponent : Component
{
    [DataField]
    public Vector2[] Positions { get; set; } = new Vector2[3];

    [DataField]
    public Color[] Colors { get; set; } = new Color[3];

    [DataField]
    public int Layer { get; set; } = 1;

    [DataField]
    public ProtoId<ShaderPrototype> ShaderId { get; set; } = "NormalShader";

    public ShaderInstance? Shader;
}
