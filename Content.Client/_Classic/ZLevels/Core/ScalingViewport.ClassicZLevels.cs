/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client._Classic.ZLevels.Core;
using Content.Shared.CCVar;
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared._Classic.ZLevels.Core.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private IConfigurationManager _configuration = default!;

    private ClassicClientZLevelsSystem? _zLevels;
    private SharedMapSystem? _mapSystem;
    private readonly ClassicZLevelOpeningCache _openingCache = new();

    private EntityQuery<TransformComponent>? _xformQuery;
    private EntityQuery<MapComponent>? _mapQuery;

    private IEye? _fallbackEye;

    /// <summary>
    /// We are looking for at least one empty tile on the screen.
    /// This is used to ensure that it makes sense to draw the z-planes and that they are visible.
    /// </summary>
    public bool TryFindEmptyTiles(EntityUid mapUid, IClydeViewport viewport)
    {
        if (_xformQuery is null || !_xformQuery.Value.TryComp(mapUid, out var xform))
            return true;

        var mapId = xform.MapID;

        // IClydeViewport.LocalToWorld expects viewport-local pixels. The old code fed
        // control-local pixels into IEyeManager.ScreenToMap, whose contract requires
        // absolute screen coordinates, which could create false opening detections.
        var size = viewport.Size;
        var corner0 = viewport.LocalToWorld(Vector2.Zero).Position;
        var corner1 = viewport.LocalToWorld(new Vector2(size.X, 0)).Position;
        var corner2 = viewport.LocalToWorld(new Vector2(0, size.Y)).Position;
        var corner3 = viewport.LocalToWorld(size).Position;

        var minX = MathF.Min(MathF.Min(corner0.X, corner1.X), MathF.Min(corner2.X, corner3.X));
        var minY = MathF.Min(MathF.Min(corner0.Y, corner1.Y), MathF.Min(corner2.Y, corner3.Y));
        var maxX = MathF.Max(MathF.Max(corner0.X, corner1.X), MathF.Max(corner2.X, corner3.X));
        var maxY = MathF.Max(MathF.Max(corner0.Y, corner1.Y), MathF.Max(corner2.Y, corner3.Y));

        var mapCoordsBottomLeft = new MapCoordinates(new Vector2(minX, minY), mapId);
        var mapCoordsTopRight = new MapCoordinates(new Vector2(maxX, maxY), mapId);

        if (_mapSystem is null || !_mapSystem.TryFindGridAt(mapUid, mapCoordsBottomLeft.Position, out var gridUid, out var grid))
            return true;

        var tileBottomLeft = _mapSystem.TileIndicesFor(gridUid, grid, mapCoordsBottomLeft);
        var tileTopRight = _mapSystem.TileIndicesFor(gridUid, grid, mapCoordsTopRight);

        return _openingCache.HasOpeningInTileBounds(
            (gridUid, grid),
            tileBottomLeft - Vector2i.One,
            tileTopRight + Vector2i.One,
            _mapSystem,
            _tile);
    }

    private void RenderZLevels(IClydeViewport viewport)
    {
        if (_eye is null)
            return;

        _fallbackEye = _eye;
        var fallbackClearColor = viewport.ClearColor;

        // Auxiliary camera/admin viewports have their own eye. The Z renderer is
        // player-map based, so running it there both renders the wrong map and repeats
        // all world passes unnecessarily.
        if (!ReferenceEquals(_eyeManager.MainViewport, this))
        {
            viewport.Render();
            return;
        }

        // Cache frequently accessed components/systems
        _xformQuery ??= _entityManager.GetEntityQuery<TransformComponent>();
        _mapQuery ??= _entityManager.GetEntityQuery<MapComponent>();

        // Cache systems and components
        _zLevels ??= _entityManager.System<ClassicClientZLevelsSystem>();
        _mapSystem ??= _entityManager.System<SharedMapSystem>();

        if (_player.LocalEntity is null)
        {
            viewport.Render();
            return;
        }

        if (!_entityManager.TryGetComponent<ClassicZLevelViewerComponent>(_player.LocalEntity.Value, out var zLevelViewer))
        {
            viewport.Render();
            return;
        }

        if (!_xformQuery.Value.TryComp(_player.LocalEntity, out var playerXform))
        {
            viewport.Render();
            return;
        }

        if (playerXform.MapUid is null)
        {
            viewport.Render();
            return;
        }

        var lookUp = 0;
        if (zLevelViewer.LookUp)
            lookUp = _zLevels.GetVisibleZLevelsAbove(_player.LocalEntity.Value, playerXform.MapUid);

        var maxLevelsBelow = Math.Clamp(
            _configuration.GetCVar(CCVars.ClassicZLevelsRenderingMaxZLevelsBelowRendering),
            0,
            ClassicSharedZLevelsSystem.MaxZLevelsBelowRendering);

        var lowestDepth = 0;
        for (var i = 0; i >= -maxLevelsBelow; i--)
        {
            var checkingMap = playerXform.MapUid.Value;

            if (i != 0)
            {
                if (!_zLevels.TryMapOffset(playerXform.MapUid.Value, i, out var mapUidBelow))
                    continue;

                checkingMap = mapUidBelow;
            }

            lowestDepth = i;

            // The opening state of the deepest permitted level cannot reveal another
            // level, so scanning its tiles cannot affect this frame's render range.
            if (i == -maxLevelsBelow)
                break;

            if (!TryFindEmptyTiles(checkingMap, viewport))
                break;
        }

        var configuredLightBlur = _configuration.GetCVar(CVars.LightBlur);
        var suppressLowerLightBlur = configuredLightBlur &&
                                     !_configuration.GetCVar(CCVars.ClassicZLevelsRenderingLowerLevelLightBlur);
        var lightBlurSuppressed = false;
        var drawLowerContentLightBlur = _configuration.GetCVar(CCVars.ClassicZLevelsRenderingLowerLevelContentLightBlur);
        var drawLowerAmbientOcclusion = _configuration.GetCVar(CCVars.ClassicZLevelsRenderingLowerLevelAmbientOcclusion);
        var useDirectLowerLightTarget =
            _configuration.GetCVar(CCVars.ClassicZLevelsRenderingLowerLevelDirectLightTarget) &&
            !drawLowerContentLightBlur;

        var configuredSoftShadows = _configuration.GetCVar(CVars.LightSoftShadows);
        var suppressLowerSoftShadows = configuredSoftShadows &&
                                       !_configuration.GetCVar(CCVars.ClassicZLevelsRenderingLowerLevelSoftShadows);
        var softShadowsSuppressed = false;

        //From the lowest depth to the highest, render each level
        try
        {
            for (var depth = lowestDepth; depth <= lookUp; depth++)
            {
                if (depth == 0)
                    viewport.Eye = _fallbackEye;
                else
                {
                    if (!_zLevels.TryMapOffset(playerXform.MapUid.Value, depth, out var mapUidBelow))
                        continue;

                    if (!_mapQuery.Value.TryComp(mapUidBelow, out var mapComp))
                        continue;

                    Angle rotation = _fallbackEye.Rotation * -1;
                    var offset = rotation.ToWorldVec() * ClassicClientZLevelsSystem.ZLevelOffset * depth;

                    viewport.Eye = new ZEye(lowestDepth, depth, lookUp)
                    {
                        Position = new MapCoordinates(_fallbackEye.Position.Position, mapComp.MapId),
                        DrawFov = _fallbackEye.DrawFov && depth >= 0,
                        DrawLight = _fallbackEye.DrawLight,
                        Offset = _fallbackEye.Offset + offset,
                        Rotation = _fallbackEye.Rotation,
                        Scale = _fallbackEye.Scale,
                        DrawContentLightBlur = depth >= 0 || drawLowerContentLightBlur,
                        DrawAmbientOcclusion = depth >= 0 || drawLowerAmbientOcclusion,
                        UseDirectContentLightTarget = depth < 0 && useDirectLowerLightTarget,
                    };
                }

                var shouldSuppressLightBlur = suppressLowerLightBlur && depth < 0;
                if (shouldSuppressLightBlur != lightBlurSuppressed)
                {
                    _configuration.SetCVar(CVars.LightBlur, shouldSuppressLightBlur ? false : configuredLightBlur);
                    lightBlurSuppressed = shouldSuppressLightBlur;
                }

                var shouldSuppressSoftShadows = suppressLowerSoftShadows && depth < 0;
                if (shouldSuppressSoftShadows != softShadowsSuppressed)
                {
                    _configuration.SetCVar(
                        CVars.LightSoftShadows,
                        shouldSuppressSoftShadows ? false : configuredSoftShadows);
                    softShadowsSuppressed = shouldSuppressSoftShadows;
                }

                viewport.ClearColor = depth == lowestDepth ? Color.Black : null;
                viewport.Render();
            }
        }
        finally
        {
            if (lightBlurSuppressed)
                _configuration.SetCVar(CVars.LightBlur, configuredLightBlur);
            if (softShadowsSuppressed)
                _configuration.SetCVar(CVars.LightSoftShadows, configuredSoftShadows);

            // Restore the Eye and render state even if a viewport pass fails.
            Eye = _fallbackEye;
            viewport.Eye = Eye;
            viewport.ClearColor = fallbackClearColor;
        }
    }

    public sealed partial class ZEye(int lowest, int depth, int high) : Robust.Shared.Graphics.Eye, IClassicZLevelRenderQuality
    {
        public int LowestDepth = lowest;
        public int Depth = depth;
        public int HighestDepth = high;

        public bool DrawContentLightBlur { get; init; } = true;
        public bool DrawAmbientOcclusion { get; init; } = true;
        public bool UseDirectContentLightTarget { get; init; }
    }
}
