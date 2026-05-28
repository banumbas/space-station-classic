using System.Numerics;
using Content.Shared.Atmos.Components;
using Content.Shared._Starlight.VentCrawl.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Content.Shared._Starlight.VentCrawl.EntitySystems;

namespace Content.Client._Starlight.VentCrawl;

public sealed partial class VentCrawPipeOverlay : Robust.Client.Graphics.Overlay
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly SpriteSystem _spriteSystem;
    private readonly EntityLookupSystem _lookup;
    private readonly SharedTransformSystem _xformSys = default!;

    private static readonly Color PipeGlowColor = new(0.4f, 0.8f, 1.0f, 0.35f);
    private static readonly Color PipeBaseColor = new(0.75f, 0.92f, 1.0f, 1.0f);
    private const float GlowRadius = 0.015f;

    private const float IndicatorRadius = 0.11f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public VentCrawPipeOverlay()
    {
        IoCManager.InjectDependencies(this);
        _spriteSystem = _entityManager.System<SpriteSystem>();
        _lookup = _entityManager.System<EntityLookupSystem>();
        _xformSys = _entityManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalSession?.AttachedEntity;
        if (player == null) return;

        if (!_entityManager.TryGetComponent<BeingVentCrawlComponent>(player, out var beingCrawl))
            return;

        if (!_entityManager.TryGetComponent<VentCrawlHolderComponent>(beingCrawl.Holder, out var holder))
            return;

        var playerLayer = ResolvePlayerLayer(holder);

        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(null);

        var eyeRot = _entityManager.GetComponent<EyeComponent>(player.Value).Rotation;

        var entities = _lookup.GetEntitiesIntersecting(args.MapId, args.WorldBounds, LookupFlags.Uncontained);

        foreach (var uid in entities)
        {
            if (!_entityManager.HasComponent<PipeAppearanceComponent>(uid))
                continue;
            if (!_entityManager.TryGetComponent<SpriteComponent>(uid, out var sprite) || !sprite.Visible)
                continue;

            if (!_entityManager.HasComponent<VentCrawlManifoldComponent>(uid))
            {
                if (!_entityManager.TryGetComponent<AtmosPipeLayersComponent>(uid, out var pipeLayer))
                    continue;
                if (pipeLayer.CurrentPipeLayer != playerLayer)
                    continue;
            }

            var worldPos = _xformSys.GetWorldPosition(uid);
            var worldRot = _xformSys.GetWorldRotation(uid);

            var oldColor = sprite.Color;

            _spriteSystem.SetColor((uid, sprite), PipeGlowColor);
            foreach (var offset in GlowOffsets)
                _spriteSystem.RenderSprite((uid, sprite), worldHandle, eyeRot, worldRot, worldPos + offset);

            _spriteSystem.SetColor((uid, sprite), PipeBaseColor);
            _spriteSystem.RenderSprite((uid, sprite), worldHandle, eyeRot, worldRot, worldPos);

            _spriteSystem.SetColor((uid, sprite), oldColor);
        }

        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(null);

        DrawPositionIndicator(worldHandle, beingCrawl.Holder);
    }

    private void DrawPositionIndicator(DrawingHandleWorld handle, EntityUid holder)
    {
        var pos = _xformSys.GetWorldPosition(holder);

        var t = (float)_timing.CurTime.TotalSeconds;
        var pulse = (MathF.Sin(t * MathF.PI) + 1f) * 0.5f;

        var glowAlpha = 0.04f + pulse * 0.10f;
        handle.DrawCircle(pos, IndicatorRadius * 2.4f, new Color(1.0f, 0.62f, 0.0f, glowAlpha));
        handle.DrawCircle(pos, IndicatorRadius * 1.9f, new Color(1.0f, 0.62f, 0.0f, glowAlpha * 1.7f));
        handle.DrawCircle(pos, IndicatorRadius * 1.55f, new Color(1.0f, 0.62f, 0.0f, glowAlpha * 2.5f));
    }

    private AtmosPipeLayer ResolvePlayerLayer(VentCrawlHolderComponent holder)
    {
        if (holder.CurrentTube != null
            && _entityManager.HasComponent<VentCrawlManifoldComponent>(holder.CurrentTube.Value)
            && holder.ManifoldLayer != null)
            return SharedVentCrawlSystem.TransformFromManifoldLayer(holder.ManifoldLayer.Value);

        if (holder.CurrentTube != null
            && _entityManager.TryGetComponent<AtmosPipeLayersComponent>(holder.CurrentTube.Value, out var cur))
            return cur.CurrentPipeLayer;

        if (holder.NextTube != null
            && _entityManager.TryGetComponent<AtmosPipeLayersComponent>(holder.NextTube.Value, out var next))
            return next.CurrentPipeLayer;

        return AtmosPipeLayer.Primary;
    }

    private static readonly Vector2[] GlowOffsets =
    {
        new(-GlowRadius, 0), new(GlowRadius, 0),
        new(0, -GlowRadius), new(0,  GlowRadius),
    };
}
