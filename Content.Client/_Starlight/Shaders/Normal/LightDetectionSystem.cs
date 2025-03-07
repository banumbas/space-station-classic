using Content.Client._Starlight.Overlay.Cyclorites;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Client.GameStates;
using Content.Shared.Interaction;
using Content.Client.Interactable;
using System.Numerics;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using System.Linq;
using Content.Client._Starlight.Shaders.Normal;

namespace Content.Client._Starlight.Overlay.Night;

public sealed class LightDetectionSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _xformSys = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private readonly TimeSpan _timeLimit = TimeSpan.FromMicroseconds(600);
    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_player.LocalEntity.HasValue || !_player.LocalEntity.Value.IsValid())
            return;

        var start = DateTime.UtcNow;

        var coords = Transform(_player.LocalEntity.Value).Coordinates;

        var entities = _lookup.GetEntitiesInRange<LightPositionForwarderComponent>(coords, 3, LookupFlags.All);
        if (entities.Count == 0)
            return;

        var lights = _lookup.GetEntitiesInRange<PointLightComponent>(coords, 3, LookupFlags.All);
        if (lights.Count == 0)
            return;

        foreach (var ent in entities)
        {
            if (!TryComp<SpriteComponent>(ent, out var sprite) || !sprite.TryGetLayer(ent.Comp.Layer, out var layer)) continue;
            if (DateTime.UtcNow - start > _timeLimit) return;

            var entMapPos = _xformSys.GetMapCoordinates(ent);
            var entPos = Transform(ent);
            var i = 0;
            foreach (var light in lights)
            {
                if (i > 2) break;
                if (light.Comp.Enabled == false) continue;
                var lightMapPos = _xformSys.GetMapCoordinates(light);

                var pos = entPos.LocalRotation.RotateVec(Vector2.Normalize(lightMapPos.Position - entMapPos.Position));
                pos.Y = -pos.Y;
                ent.Comp.Positions[i] = pos;
                ent.Comp.Colors[i] = light.Comp.Color;
                i++;
            }
            if (ent.Comp.Shader is null)
            {
                ent.Comp.Shader = _prototypeManager.Index(ent.Comp.ShaderId).InstanceUnique();
                layer.Shader = ent.Comp.Shader;
            }
            ent.Comp.Shader.SetParameter("lpositions",ent.Comp.Positions);
            ent.Comp.Shader.SetParameter("lcolors", ent.Comp.Colors);
            ent.Comp.Shader.SetParameter("lightCount", i);
        }
    }
}
