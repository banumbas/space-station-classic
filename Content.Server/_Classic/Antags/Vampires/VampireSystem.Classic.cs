using Content.Server.GameTicking;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Parallax.Biomes;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._Starlight.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    private double GetPlanetDaylightMultiplier(EntityUid uid, TransformComponent xform)
    {
        if (xform.GridUid == null)
            return 0.0;

        var gridUid = xform.GridUid.Value;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return 0.0;

        // Ensure it is a planet grid
        if (!HasComp<SunShadowCycleComponent>(gridUid))
            return 0.0;

        var mapUid = _map.GetMapOrInvalid(xform.MapID);
        if (!TryComp<LightCycleComponent>(mapUid, out var lightCycle))
            return 0.0;

        if (TryComp<RoofComponent>(gridUid, out var roof))
        {
            var roofSys = EntityManager.System<SharedRoofSystem>();
            var index = _map.GetTileRef(gridUid, grid, xform.Coordinates).GridIndices;
            if (roofSys.IsRooved((gridUid, grid, roof), index))
                return 0.0;
        }

        var time = (_timing.CurTime + lightCycle.Offset).TotalSeconds;
        var lightLevel = SharedLightCycleSystem.CalculateLightLevel(lightCycle, (float)time);

        // We consider < 0.2 as night (no burn), 1.0+ as peak day
        var minThreshold = 0.2;
        var maxThreshold = 1.0;

        if (lightLevel <= minThreshold)
            return 0.0;

        if (lightLevel >= maxThreshold)
            return 1.0;

        return (lightLevel - minThreshold) / (maxThreshold - minThreshold);
    }
}
