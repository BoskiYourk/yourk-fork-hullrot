using Content.Shared._Crescent.ShipShields;
using Robust.Shared.Physics.Systems;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Content.Server.Power.Components;
using Content.Server._Crescent.UnionfallCapturePoint;
using Content.Shared.Interaction;
using Content.Shared.Preferences;
using Content.Server.Preferences.Managers;
using Robust.Shared.Network;
using Content.Server._Crescent.HullrotFaction;
using Robust.Shared.Player;
using Content.Server.Announcements.Systems;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.DoAfter;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Serialization;
using Content.Shared.DoAfter;
using Content.Shared._Crescent.UnionfallCapturePoint;
using Robust.Shared.Timing;
using Content.Shared._Crescent.UnionfallShipNode;


namespace Content.Server._Crescent.UnionfallCapturePoint;

public sealed class UnionfallShipNodeSystem : EntitySystem
{

    [Dependency] private readonly AnnouncerSystem _announcer = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<UnionfallShipNodeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<UnionfallShipNodeComponent, ActivateInWorldEvent>(OnActivatedInWorld);
        SubscribeLocalEvent<UnionfallShipNodeComponent, UnionfallShipNodeDoAfterEvent>(OnCaptureDoAfter);
        SubscribeLocalEvent<UnionfallShipNodeComponent, ComponentRemove>(OnDestruction);
        _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("audio.ambience");
    }

    private void OnComponentInit(EntityUid uid, UnionfallShipNodeComponent component, ComponentInit args)
    {
        //skibidi sigma
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<UnionfallShipNodeComponent>();
        while (query.MoveNext(out var uid, out var capturepoint))
        {
            capturepoint.GracePeriod -= frameTime; //we do it this way so we can VVedit in admin mode midgame

            if (capturepoint.GracePeriod > 0f) //point is still in grace period
                return;

            if (capturepoint.IsBeingCaptured == false) //if nobody's capping it then don't do anything
                return;
            else //someone is capping it rn
            {
                capturepoint.CurrentCaptureProgress -= frameTime; //this is how the timer decreases
            }

            if (capturepoint.CurrentCaptureProgress <= 0) //capturing complete. announce and count how many left
            {
                _announcer.SendAnnouncement(_announcer.GetAnnouncementId("Fallback"), Filter.Broadcast(),
            "A " + capturepoint.OwningFaction + " cloner database has been destroyed!");
                _gameTicker.EndRound("All of " + capturepoint.OwningFaction + "'s cloner databases have been destroyed. ROUND OVER!");
                capturepoint.CurrentCaptureProgress = 999999;
                Timer.Spawn(TimeSpan.FromMinutes(1), _gameTicker.RestartRound);
            }
        }
    }

    private void OnActivatedInWorld(EntityUid uid, UnionfallShipNodeComponent component, ActivateInWorldEvent args)
    {
        if (component.GracePeriod > 0) //grace period still active
        {
            _popup.PopupEntity(Loc.GetString("shipnode-grace-period-fail"), uid, args.User);
            return;
        }

        if (!TryComp<HullrotFactionComponent>(args.User, out var comp)) //someone with no faction interacted with this. modified client only
            return;
        string faction = comp.Faction;

        if (component.OwningFaction == faction & component.IsBeingCaptured == false)
        {
            _popup.PopupEntity(Loc.GetString("shipnode-same-faction-fail"), uid, args.User);
            return;
        }

        if (component.OwningFaction == faction) //defusing
            _popup.PopupEntity(Loc.GetString("shipnode-defusing"), uid, args.User);
        else
            _popup.PopupEntity(Loc.GetString("shipnode-sabotaging"), uid, args.User);


        DoAfterArgs doAfterArguments = new DoAfterArgs(EntityManager, args.User, component.DoAfterDelay, new UnionfallShipNodeDoAfterEvent(), uid, uid, null)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterArguments, null);
    }

    private void OnCaptureDoAfter(EntityUid uid, UnionfallShipNodeComponent component, UnionfallShipNodeDoAfterEvent args)
    {
        if (args.Cancelled)
            return;
        if (args.Target is null)
            return;

        if (!TryComp<HullrotFactionComponent>(args.User, out var comp)) //someone with no faction interacted with this. modified client only
            return;
        string faction = comp.Faction;

        if (component.OwningFaction != comp.Faction) // opposing faction rigged to blow
        {
            component.IsBeingCaptured = true;
            _announcer.SendAnnouncement(_announcer.GetAnnouncementId("unionfallPointCapture"), Filter.Broadcast(),
                "A " + component.OwningFaction + " cloner database has been rigged to explode! It will detonate in " + float.Round(component.CurrentCaptureProgress).ToString() + " seconds.");
        }
        else if (component.OwningFaction == faction) // same faction interacted to defuse
        {
            component.IsBeingCaptured = false;
            component.CurrentCaptureProgress = component.TimeToCapture;

            _announcer.SendAnnouncement(_announcer.GetAnnouncementId("unionfallPointCapture"), Filter.Broadcast(),
                "The " + component.OwningFaction + " cloner database has been defused.");
        }
    }

    private void OnDestruction(EntityUid uid, UnionfallShipNodeComponent capturepoint, ComponentRemove args)
    {
        _announcer.SendAnnouncement(_announcer.GetAnnouncementId("Fallback"), Filter.Broadcast(),
            "A " + capturepoint.OwningFaction + " cloner database has been destroyed!");
        _gameTicker.EndRound("All of " + capturepoint.OwningFaction + "'s cloner databases have been destroyed. ROUND OVER");
        capturepoint.CurrentCaptureProgress = 999999;
        Timer.Spawn(TimeSpan.FromMinutes(1), _gameTicker.RestartRound);
    }


}
