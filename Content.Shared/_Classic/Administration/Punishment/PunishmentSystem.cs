using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Paper;
using Robust.Shared.Network;

namespace Content.Shared._Classic.Administration.Punishment;

public sealed class PunishmentSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PunishmentComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PunishmentComponent, PaperWriteAttemptEvent>(OnPaperWriteAttempt);
    }

    private void OnExamined(Entity<PunishmentComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.MutedChannels != ChatChannel.None)
        {
            var channelsStr = ent.Comp.MutedChannels.ToString();
            args.PushMarkup(Loc.GetString("punishment-examine-muted-channels", ("channels", channelsStr)));
        }

        if (ent.Comp.PaperMuted)
        {
            args.PushMarkup(Loc.GetString("punishment-examine-paper-muted"));
        }

        if (ent.Comp.ForcedPacifism)
        {
            args.PushMarkup(Loc.GetString("punishment-examine-pacified"));
        }
    }

    private void OnPaperWriteAttempt(Entity<PunishmentComponent> ent, ref PaperWriteAttemptEvent args)
    {
        if (!ent.Comp.PaperMuted)
            return;

        args.Cancelled = true;
        args.FailReason = Loc.GetString("punishment-paper-write-blocked");
    }
}
