using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Structs;
using Exiled.Events.EventArgs.Interfaces;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp914;
using MEC;
using PlayerRoles;
using System.Collections.Generic;
using PlayerEvent = Exiled.Events.Handlers.Player;
using ServerEvent = Exiled.Events.Handlers.Server;
using Scp914Event = Exiled.Events.Handlers.Scp914;
using System;
using System.Linq;

namespace CustomGameModes.GameModes;

internal class TroubleInLC : IGameMode
{
    public bool? FFStateBefore = null;

    public const int DefaultKarma = 1000;
    public static Dictionary<Player, int> Karma = new();
    public Dictionary<Player, int> BaseKarma;

    public string Name => "Trouble In Light Containment";

    public string PreRoundInstructions => "Hidden <color=green>Chaos</color>, No Elevators, 914 on Rough hurts but reveals <color=green>Chaos</color>";

    public HashSet<Player> TrackedPlayers;
    public HashSet<Player> DamagedATeammate;

    public TroubleInLC()
    {
        BaseKarma = Karma.ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // copy the karma as it was at the beginning of the round
        TrackedPlayers = new HashSet<Player>();
        DamagedATeammate = new HashSet<Player>();
    }

    ~TroubleInLC()
    {
        UnsubscribeEventHandlers();
    }

    public void OnRoundEnd()
    {
        UnsubscribeEventHandlers();

        foreach (Player player in Player.List)
        {
            AdjustKarma(player, DamagedATeammate.Contains(player) ? 50 : 150);
        }
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
        PlayerEvent.Died += OnDied;
        PlayerEvent.Hurting += OnHurting;

        Scp914Event.UpgradingPlayer += OnUpgradingPlayer;

        ServerEvent.RespawningTeam += DeniableEvent;

        FFStateBefore = Server.FriendlyFire;
        Server.FriendlyFire = true;

        SetupPlayers();
    }
    void UnsubscribeEventHandlers()
    {
        PlayerEvent.InteractingElevator -= OnElevator;
        PlayerEvent.Joined -= OnJoin;
        PlayerEvent.Spawned -= OnSpawned;
        PlayerEvent.Died -= OnDied;
        PlayerEvent.Hurting -= OnHurting;

        Scp914Event.UpgradingPlayer -= OnUpgradingPlayer;

        ServerEvent.RespawningTeam -= DeniableEvent;

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
                ShowBaseKarmaOnPlayer(ci);
            }

            foreach (Player scientinst in scientists)
            {
                SetupScientist(scientinst);
                ShowBaseKarmaOnPlayer(scientinst);
            }
        });
    }

    public void SetupScientist(Player scientinst)
    {
        scientinst.Role.Set(RoleTypeId.Scientist);
        scientinst.AddItem(FirearmType.Com15, new AttachmentIdentifier[] { });
        scientinst.AddItem(ItemType.Flashlight);
    }

    public void AdjustKarma(Player player, int amount)
    {
        var max = CustomGameModes.Singleton.Config.TttMaxKarma;
        Karma[player] = Math.Min(Math.Max(0, GetKarma(player) + amount), max);
    }

    public int GetKarma(Player player)
    {
        if (Karma.TryGetValue(player, out var karma))
            return karma;
        Karma[player] = DefaultKarma;
        return Karma[player];
    }

    public void ShowBaseKarmaOnPlayer(Player player)
    {
        player.CustomInfo = KarmaRank(player);
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

    public void OnDied(DiedEventArgs ev)
    {
        if (PlayerRolesUtils.GetTeam(ev.TargetOldRole) == ev.Attacker.Role.Team)
        {
            AdjustKarma(ev.Attacker, -GetKarma(ev.Player) / 10);
        }
    }

    public void OnHurting(HurtingEventArgs ev)
    {
        ev.Amount *= DamageMultiplierFromKarma(ev.Attacker);
        if (ev.Player.Role.Team == ev.Attacker.Role.Team)
        {
            DamagedATeammate.Add(ev.Attacker);
            AdjustKarma(ev.Attacker, -20);
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

    public void DeniableEvent(IDeniableEvent ev)
    {
        ev.IsAllowed = false;
    }

    public string KarmaRank(Player player) => GetKarma(player) switch
    {
        < 300 => "<color=red>Liability</color>",
        < 700 => "<color=yellow>Disreputable</color>",
        _ => "<color=green>Reputable</color>",
    };

    public float DamageMultiplierFromKarma(Player player) => Math.Min(1f, GetKarma(player) / DefaultKarma);
}
