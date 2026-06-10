using Content.Server.Cargo.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Classic.Salvage.Fulton;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Classic.Salvage.Fulton;

public sealed partial class ClassicCargoFultonSystem : SharedClassicCargoFultonSystem
{
    [Dependency] private CargoSystem _cargo = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StationSystem _station = default!;

    private static readonly TimeSpan SaleDelay = TimeSpan.FromSeconds(0.8);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClassicFultonSoldComponent, ComponentStartup>(OnFultonedStartup);
        SubscribeLocalEvent<ClassicFultonSoldComponent, ComponentShutdown>(OnFultonedShutdown);
    }

    private void OnFultonedShutdown(EntityUid uid, ClassicFultonSoldComponent component, ComponentShutdown args)
    {
        Del(component.Effect);
        component.Effect = EntityUid.Invalid;
    }

    private void OnFultonedStartup(EntityUid uid, ClassicFultonSoldComponent component, ComponentStartup args)
    {
        if (Exists(component.Effect))
            return;

        component.Effect = Spawn(EffectProto, new EntityCoordinates(uid, EffectOffset));
        Dirty(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ClassicFultonSoldComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.SaleTime is { } saleTime)
            {
                if (_timing.CurTime >= saleTime)
                    FinishSale(uid, component);

                continue;
            }

            if (component.NextFulton <= _timing.CurTime)
                LaunchSale(uid, component);
        }
    }

    protected override bool CanCompleteCargoFulton(
        EntityUid fultonUid,
        EntityUid targetUid,
        EntityUid userUid,
        ClassicCargoFultonComponent component)
    {
        if (_cargo.ClassicCanSellEntity(targetUid) && GetSaleStation(targetUid) != null)
            return true;

        _popup.PopupEntity(Loc.GetString("fulton-invalid"), targetUid, userUid);
        return false;
    }

    protected override void OnCargoFultonApplied(
        EntityUid fultonUid,
        EntityUid targetUid,
        EntityUid userUid,
        ClassicCargoFultonComponent fulton,
        ClassicFultonSoldComponent fultoned)
    {
        fultoned.SaleStation = GetSaleStation(targetUid);
    }

    private void LaunchSale(EntityUid uid, ClassicFultonSoldComponent component)
    {
        var station = component.SaleStation;
        if (station == null || Deleted(station.Value))
            station = GetSaleStation(uid);

        if (!_cargo.ClassicCanSellEntity(uid) ||
            !CanCargoFulton(uid) ||
            station is not { } saleStation)
        {
            CancelSale(uid);
            return;
        }

        component.SaleStation = saleStation;
        component.OriginalCoordinates = Transform(uid).Coordinates;
        component.SaleTime = _timing.CurTime + SaleDelay;
        Dirty(uid, component);

        PlayFultonAnimation(uid, component, component.OriginalCoordinates.Value);
        TransformSystem.DetachEntity(uid);
    }

    private EntityUid? GetSaleStation(EntityUid target)
    {
        if (TryFindActiveSensorTower(target, out var towerUid))
            return _station.GetOwningStation(towerUid) ?? _station.GetOwningStation(target);

        return _station.GetOwningStation(target);
    }

    private void FinishSale(EntityUid uid, ClassicFultonSoldComponent component)
    {
        if (component.SaleStation is { } station && _cargo.ClassicTrySellEntity(uid, station, out _))
            return;

        if (component.OriginalCoordinates is { } coordinates && coordinates.IsValid(EntityManager))
            TransformSystem.SetCoordinates(uid, coordinates);

        CancelSale(uid);
    }

    private void CancelSale(EntityUid uid)
    {
        RemCompDeferred<ClassicFultonSoldComponent>(uid);
    }

    private void PlayFultonAnimation(EntityUid uid, ClassicFultonSoldComponent component, EntityCoordinates oldCoords)
    {
        var metadata = MetaData(uid);

        RaiseNetworkEvent(new ClassicFultonAnimationMessage()
        {
            Entity = GetNetEntity(uid, metadata),
            Coordinates = GetNetCoordinates(oldCoords),
        });

        Audio.PlayPvs(component.Sound, uid);
    }
}
