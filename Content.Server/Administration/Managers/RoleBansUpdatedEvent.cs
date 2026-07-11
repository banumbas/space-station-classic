using Robust.Shared.Network;

namespace Content.Server.Administration.Managers;

/// <summary>
/// Raised when a player's role bans are updated (added or pardoned) while they are connected.
/// </summary>
public sealed class RoleBansUpdatedEvent : EntityEventArgs
{
    public NetUserId UserId { get; }

    public RoleBansUpdatedEvent(NetUserId userId)
    {
        UserId = userId;
    }
}
