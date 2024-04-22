using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Structs;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp914;
using MEC;
using PlayerRoles;
using PluginAPI.Roles;
using System.Collections.Generic;
using PlayerEvent = Exiled.Events.Handlers.Player;
using Scp914Event = Exiled.Events.Handlers.Scp914;

namespace CustomGameModes.GameModes;

internal class TroubleInLC : IGameMode
{
    public bool? FFStateBefore = null;

    public string Name => "Trouble In Light Containment";

    public string PreRoundInstructions => "Hidden <color=green>Chaos</color>, No Elevators, 914 on Rough hurts but reveals <color=green>Chaos</color>";

    public HashSet<Player> TrackedPlayers;

    public TroubleInLC()
    {
        TrackedPlayers = new HashSet<Player>();
    }

    ~TroubleInLC()
    {
        UnsubscribeEventHandlers();
    }

    public void OnRoundEnd()
    {
        UnsubscribeEventHandlers();
    }

    public void OnRoundStart()
    {
        SubscribeEventHandlers();
    }

    public void OnWaitingForPlayers()
    {
        UnsubscribeEventHandlers();
    }

    void SubscribeEventHandlers()
    {
        PlayerEvent.InteractingElevator += OnElevator;
        PlayerEvent.Joined += OnJoin;
        PlayerEvent.Spawned += OnSpawned;
        Scp914Event.UpgradingPlayer += OnUpgradingPlayer;

        FFStateBefore = Server.FriendlyFire;
        Server.FriendlyFire = true;

        SetupPlayers();
    }
    void UnsubscribeEventHandlers()
    {
        PlayerEvent.InteractingElevator -= OnElevator;
        PlayerEvent.Joined -= OnJoin;
        PlayerEvent.Spawned -= OnSpawned;
        Scp914Event.UpgradingPlayer -= OnUpgradingPlayer;

        if (FFStateBefore.HasValue)
            Server.FriendlyFire = FFStateBefore.Value;
    }

    public void SetupPlayers()
    {
        var scientists = new List<Player>();
        var troublemakers = new List<Player>();

        foreach (Player player in Player.List)
        {
            TrackedPlayers.Add(player);
            switch (player.Role.Team)
            {
                case Team.SCPs:
                    {
                        player.Role.Set(RoleTypeId.ChaosRepressor, RoleSpawnFlags.None);
                        troublemakers.Add(player);
                        break;
                    }
                default:
                    {
                        scientists.Add(player);
                        break;
                    }
            }
        }

        Timing.CallDelayed(0.1f, () =>
        {
            foreach (Player ci in troublemakers)
            {
                ci.ChangeAppearance(RoleTypeId.Scientist, scientists);
                ci.Position = RoleTypeId.Scientist.GetRandomSpawnLocation().Position;
                ci.AddItem(FirearmType.Com15, new AttachmentIdentifier[] { });
                ci.AddItem(ItemType.KeycardScientist);
                ci.AddItem(ItemType.Medkit);
                ci.AddItem(ItemType.Flashlight);
            }

            foreach (Player scientinst in scientists)
            {
                SetupScientist(scientinst);
            }
        });
    }

    public void SetupScientist(Player scientinst)
    {
        scientinst.Role.Set(RoleTypeId.Scientist);
        scientinst.AddItem(FirearmType.Com15, new AttachmentIdentifier[] { });
        scientinst.AddItem(ItemType.Flashlight);
    }

    public void OnElevator(InteractingElevatorEventArgs ev)
    {
        ev.IsAllowed = false;
    }

    public void OnUpgradingPlayer(UpgradingPlayerEventArgs ev)
    {
        if (ev.KnobSetting == Scp914.Scp914KnobSetting.Rough)
        {
            ev.Player.ChangeAppearance(ev.Player.Role, Player.List);
            ev.Player.Health /= 2;
        }
    }

    public void OnJoin(JoinedEventArgs ev)
    {
        foreach (Player player in Player.Get(Team.ChaosInsurgency))
        {
            player.ChangeAppearance(RoleTypeId.Scientist, new[] { ev.Player });
        }
    }

    public void OnSpawned(SpawnedEventArgs ev)
    {
        if (TrackedPlayers.Contains(ev.Player))
            return;

        TrackedPlayers.Add(ev.Player);
        SetupScientist(ev.Player);
    }
}
