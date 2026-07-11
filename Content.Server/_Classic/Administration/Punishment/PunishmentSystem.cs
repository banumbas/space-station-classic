using Content.Server.Administration.Managers;
using Content.Shared._Classic.Administration.Punishment;
using Content.Shared.Chat;
using Content.Shared.CombatMode.Pacification;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Classic.Administration.Punishment;

public sealed class PunishmentSystem : EntitySystem
{
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _nextUpdateTime = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<RoleBansUpdatedEvent>(OnRoleBansUpdated);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdateTime)
            return;

        _nextUpdateTime = _timing.CurTime + TimeSpan.FromSeconds(1);

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is { } entity)
                UpdatePunishments(entity, session.UserId);
        }
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        UpdatePunishments(args.Entity, args.Player.UserId);
    }

    private void OnRoleBansUpdated(RoleBansUpdatedEvent args)
    {
        if (_playerManager.TryGetSessionById(args.UserId, out var session) && session.AttachedEntity is { } entity)
        {
            UpdatePunishments(entity, args.UserId);
        }
    }

    private void UpdatePunishments(EntityUid uid, NetUserId userId)
    {
        var punishments = _banManager.GetPunishments(userId);
        
        var mutedChannels = ChatChannel.None;
        var paperMuted = false;
        var pacifism = false;

        if (punishments != null)
        {
            foreach (var ban in punishments)
            {
                var type = ban.Role[BanManager.PrefixPunishment.Length..];
                
                if (type == "Pacifism")
                    pacifism = true;
                else if (type == "Mute:Paper")
                    paperMuted = true;
                else if (type.StartsWith("Mute:"))
                {
                    var channelStr = type["Mute:".Length..];
                    if (Enum.TryParse<ChatChannel>(channelStr, out var channel))
                    {
                        mutedChannels |= channel;
                    }
                }
            }
        }

        if (mutedChannels == ChatChannel.None && !paperMuted && !pacifism)
        {
            RemComp<PunishmentComponent>(uid);
            
            // Only remove pacifism if they don't have it from somewhere else?
            // Actually, if we just remove PacifiedComponent, it's fine, but let's check if they still need it.
            // For now, removing it directly is okay, as pacifism is usually applied via component.
            // However, other things might apply pacifism. A safe way is to track ForcedPacifism.
            // If they had ForcedPacifism, we can remove PacifiedComponent.
            if (TryComp<PacifiedComponent>(uid, out var _))
                RemComp<PacifiedComponent>(uid);
            return;
        }

        var comp = EnsureComp<PunishmentComponent>(uid);
        
        if (comp.MutedChannels == mutedChannels && comp.PaperMuted == paperMuted && comp.ForcedPacifism == pacifism)
            return;

        comp.MutedChannels = mutedChannels;
        comp.PaperMuted = paperMuted;
        
        if (pacifism && !comp.ForcedPacifism)
        {
            EnsureComp<PacifiedComponent>(uid);
        }
        else if (!pacifism && comp.ForcedPacifism)
        {
            RemComp<PacifiedComponent>(uid);
        }
        
        comp.ForcedPacifism = pacifism;
        Dirty(uid, comp);
    }
}
