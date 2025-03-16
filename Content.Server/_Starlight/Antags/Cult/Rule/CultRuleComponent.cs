using Content.Server.RoundEnd;
using Content.Shared.Dataset;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(CultRuleSystem))]
public sealed partial class CultRuleComponent : Component
{
    [DataField]
    public RoundEndBehavior GodDeadRoundEndBehavior = RoundEndBehavior.ShuttleCall;

    [DataField]
    public string GodDeadRoundEndTextSender = "comms-console-announcement-title-centcom";

    [DataField]
    public string GodDeadRoundEndTextShuttleCall = "clockwork-no-more-threat-announcement-shuttle-call";

    [DataField]
    public string GodDeadRoundEndText = "clockwork-crew-win";

    //

    [DataField]
    public RoundEndBehavior GodReleasedRoundEndBehavior = RoundEndBehavior.DelayedEnd;

    [DataField]
    public string GodReleasedRoundEndTextSender = "ratvar";

    [DataField]
    public string GodReleasedRoundEndTextShuttleCall = "clockwork-no-more-threat-announcement-shuttle-call";

    [DataField]
    public string GodReleasedRoundEndText = "clockwork-ratvar-win";

    //

    [DataField]
    public string AntagList = "clockwork-list-start";

    [DataField]
    public string AntagListNameUser = "clockwork-list-name-user";

    //

    /// <summary>
    ///     A time indicator showing when the cult scenario started.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan StartTime;

    [DataField]
    public TimeSpan WhenPortalPermanent = TimeSpan.FromMinutes(45);

    [DataField]
    public TimeSpan PortalMinTime = TimeSpan.FromSeconds(15);

    [DataField]
    public float PortalLifetimeMultiplier = 0.02f;

    [DataField]
    public TimeSpan EvacShuttleDisabled = TimeSpan.FromMinutes(45);

    [DataField]
    public TimeSpan RoundEndDelay = TimeSpan.FromMinutes(10);

    [DataField]
    public CultWinType? WinType;

    [DataField]
    public EntityUid? TargetStation;

    [DataField]
    public ProtoId<NpcFactionPrototype> Faction = "Clockwork";

    [DataField]
    public SoundSpecifier GreetSoundNotification = new SoundPathSpecifier("/Audio/_Starlight/Ambience/Antag/clockwork.ogg");
}

public enum CultWinType : byte
{
    God,
    Tie,
    Crew,
}