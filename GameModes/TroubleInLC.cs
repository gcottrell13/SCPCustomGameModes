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
using MapEvent = Exiled.Events.Handlers.Map;
using System;
using System.Linq;
using Exiled.Events.EventArgs.Map;
using Exiled.API.Features.Items;
using CustomGameModes.Configs;
using CustomGameModes.Commands;
using CustomGameModes.API;
using Exiled.API.Features.DamageHandlers;
using PlayerStatsSystem;

namespace CustomGameModes.GameModes;

internal class TroubleInLC : IGameMode
{
    public bool? FFStateBefore = null;

    public const int DefaultKarma = 1000;
    public static Dictionary<Player, int> BaseKarma = new Dictionary<Player, int>();
    public Dictionary<Player, int> LiveKarma;

    public string Name => "Trouble In Light Containment";

    public string PreRoundInstructions => "Hidden <color=green>Traitors</color>, No Elevators, 914 on Rough hurts but reveals <color=green>Traitors</color>";

    public const string OpenStoreInstructions = $"Use <color=blue>~</color> and type <color=blue>.{TTTBuyItemCommand.CommandName}</color> to buy items.";

    public HashSet<Player> TrackedPlayers;
    public HashSet<Player> DamagedATeammate;
    public Dictionary<Player, int> Credits;
    public HashSet<Player> Detectives;
    public Dictionary<Player, Player> Killers; // [dead person] => killer
    public HashSet<Ragdoll> RevealedBodies;

    private CoroutineHandle coroutineHandle;

    private Config config;

    public TroubleInLC()
    {
        LiveKarma = BaseKarma.ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // copy the karma as it was at the beginning of the round
        TrackedPlayers = new HashSet<Player>();
        DamagedATeammate = new HashSet<Player>();
        Credits = new Dictionary<Player, int>();
        Detectives = new HashSet<Player>();
        config = CustomGameModes.Singleton.Config;
        Killers = new Dictionary<Player, Player>();
        RevealedBodies = new HashSet<Ragdoll>();
    }

    ~TroubleInLC()
    {
        UnsubscribeEventHandlers();
    }

    public void OnRoundEnd()
    {
        UnsubscribeEventHandlers();

        if (coroutineHandle.IsRunning)
            Timing.KillCoroutines(coroutineHandle);

        foreach (Player player in Player.List)
        {
            AdjustKarma(player, DamagedATeammate.Contains(player) ? 50 : 150);
            player.CustomName = null;
        }

        BaseKarma = LiveKarma.ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // copy the karma as it was at the end of the round
    }

    public void OnRoundStart()
    {
        SubscribeEventHandlers();

        coroutineHandle = Timing.RunCoroutine(roundCoroutine());
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
        PlayerEvent.Dying += OnDying;
        PlayerEvent.Hurting += OnHurting;

        Scp914Event.UpgradingPlayer += OnUpgradingPlayer;

        ServerEvent.RespawningTeam += DeniableEvent;

        MapEvent.Decontaminating += OnDecontaminating;

        FFStateBefore = Server.FriendlyFire;
        Server.FriendlyFire = true;

        SetupPlayers();
    }
    void UnsubscribeEventHandlers()
    {
        PlayerEvent.InteractingElevator -= OnElevator;
        PlayerEvent.Joined -= OnJoin;
        PlayerEvent.Spawned -= OnSpawned;
        PlayerEvent.Dying -= OnDying;
        PlayerEvent.Hurting -= OnHurting;

        Scp914Event.UpgradingPlayer -= OnUpgradingPlayer;

        ServerEvent.RespawningTeam -= DeniableEvent;

        MapEvent.Decontaminating -= OnDecontaminating;

        if (FFStateBefore.HasValue)
            Server.FriendlyFire = FFStateBefore.Value;
    }

    private IEnumerator<float> roundCoroutine()
    {
        yield return Timing.WaitForOneFrame;

        while (Round.InProgress)
        {
            yield return Timing.WaitForSeconds(1f);

            foreach (Player player in Player.List)
            {
                if (player.Zone != ZoneType.LightContainment)
                {
                    player.Position = RoleTypeId.Scientist.GetRandomSpawnLocation().Position;
                }
            }

            foreach (Player detective in Detectives)
            {
                if (!detective.IsAlive)
                    continue;

                foreach (Ragdoll ragdoll in Ragdoll.List)
                {
                    if (RevealedBodies.Contains(ragdoll) || !Killers.ContainsKey(ragdoll.Owner))
                        continue;
                    if ((detective.Position - ragdoll.Position).magnitude > 1)
                        continue;
                    RevealedBodies.Add(ragdoll);
                    var killerName = Killers[ragdoll.Owner].DisplayNickname;
                    ragdoll.Nickname = $"Killed By {killerName} - {ragdoll.Owner.DisplayNickname}"; // 's body - they were Scientist
                    ragdoll.Owner.CustomName = $"{ragdoll.Owner.DisplayNickname} - Killed by {killerName}";
                    Log.Debug($"Setting ragdoll nickname: {ragdoll.Nickname}");
                    //if ((detective.Position - ragdoll.Position).magnitude < 1)
                    //{
                    //    switch (ragdoll.DamageHandler)
                    //    {
                    //        case PlayerStatsSystem.FirearmDamageHandler handler:
                    //            {
                    //                handler.Attacker = Killers[ragdoll.Owner].Footprint;
                    //                break;
                    //            }
                    //        case PlayerStatsSystem.ExplosionDamageHandler handler:
                    //            {
                    //                handler.Attacker = Killers[ragdoll.Owner].Footprint;
                    //                break;
                    //            }
                    //        case PlayerStatsSystem.AttackerDamageHandler handler:
                    //            {
                    //                handler.Attacker = Killers[ragdoll.Owner].Footprint;
                    //                break;
                    //            }
                    //    }
                    //}
                }
            }
        }

    }

    public void SetupPlayers()
    {
        var scientists = new List<Player>();
        var troublemakers = new List<Player>();

        Round.IsLocked = true;

        foreach (Player player in Player.List)
        {
            TrackedPlayers.Add(player);
            switch (player.Role.Team)
            {
                case Team.SCPs:
                    {
                        player.Role.Set(RoleTypeId.ChaosRepressor, RoleSpawnFlags.None);
                        troublemakers.Add(player);
                        Credits[player] = config.TttTraitorStartCredits;
                        break;
                    }
                default:
                    {
                        scientists.Add(player);
                        if (player.Role == RoleTypeId.Scientist)
                        {
                            Detectives.Add(player);
                            Credits[player] = config.TttTraitorStartCredits;
                        }
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
                ci.ShowHint($"""
                    You are a Traitor! Kill everyone who isn't Chaos Insurgency.
                    You can earn shop credits by killing Innocents (starting {Credits[ci]} credits).
                    """, 30);
            }

            foreach (Player scientinst in scientists)
            {
                SetupScientist(scientinst);
                ShowBaseKarmaOnPlayer(scientinst);
            }

            Round.IsLocked = false;
        });
    }

    public void SetupScientist(Player scientinst)
    {
        scientinst.Role.Set(RoleTypeId.Scientist);
        scientinst.AddItem(FirearmType.Com15, new AttachmentIdentifier[] { });
        scientinst.AddItem(ItemType.Flashlight);

        if (Detectives.Contains(scientinst))
        {
            scientinst.ShowHint($"""
                You are a Detective! You can discover a body's killer.
                You can earn shop credits when a Traitor dies (starting {Credits[scientinst]} credits).
                """, 30);
        }
    }

    public void AdjustKarma(Player player, int amount)
    {
        var max = config.TttMaxKarma;
        var current = GetKarma(player);
        LiveKarma[player] = Math.Min(Math.Max(0, current + amount), max);
    }

    public int GetKarma(Player player)
    {
        if (LiveKarma.TryGetValue(player, out var karma))
            return karma;
        LiveKarma[player] = DefaultKarma;
        return LiveKarma[player];
    }

    public void ShowBaseKarmaOnPlayer(Player player)
    {
        player.CustomInfo = KarmaRank(player);
    }

    public void AddCredits(Player player, int amount)
    {
        if (!Credits.ContainsKey(player))
            Credits[player] = 0;
        Credits[player] += amount;
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

    public void OnDying(DyingEventArgs ev)
    {
        if (ev.Player == null || ev.Attacker == null)
            return;

        if (ev.Player.Role.Team == ev.Attacker.Role.Team)
        {
            AdjustKarma(ev.Attacker, -GetKarma(ev.Player) / 10);
            return;
        }
        
        if (ev.Attacker.Role.Team == Team.ChaosInsurgency)
        {
            var reward = config.TttKillInnocentReward;
            var title = "";
            if (Detectives.Contains(ev.Player))
            {
                title = "Detective ";
                reward *= 2;
            }
            AddCredits(ev.Attacker, reward);
            ev.Attacker.ShowHint($"""
                You got {reward} credits for killing {title}{ev.Player.DisplayNickname}.
                {OpenStoreInstructions}
                """, 15);

            ev.DamageHandler.Attacker = null;
            AdjustKarma(ev.Attacker, 20);
        }

        if (ev.Player.Role.Team == Team.ChaosInsurgency)
        {
            foreach (Player detective in Detectives)
            {
                AddCredits(detective, config.TttCiDyingReward);
                detective.ShowHint($"""
                    You got {config.TttCiDyingReward} credits because a Traitor died.
                    {OpenStoreInstructions}
                    """, 15);
            }
            AdjustKarma(ev.Attacker, 40);
        }

        Killers[ev.Player] = ev.Attacker;
    }

    public void OnHurting(HurtingEventArgs ev)
    {
        if (ev.Player == null || ev.Attacker == null)
            return;

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

    public void OnDecontaminating(DecontaminatingEventArgs ev)
    {
        ev.IsAllowed = false;
        foreach (Player player in Player.Get(Team.ChaosInsurgency))
        {
            player.Role.Set(RoleTypeId.Spectator);
        }
    }

    public void DeniableEvent(IDeniableEvent ev)
    {
        ev.IsAllowed = false;
    }

    public string KarmaRank(Player player) => GetKarma(player) switch
    {
        < 700 => ColorHelper.Color(Misc.PlayerInfoColorTypes.Red, "Liability"),
        < 900 => ColorHelper.Color(Misc.PlayerInfoColorTypes.Yellow, "Disreputable"),
        _ => ColorHelper.Color(Misc.PlayerInfoColorTypes.Green, "Reputable"),
    };

    public float DamageMultiplierFromKarma(Player player) => Math.Min(1f, (float)GetKarma(player) / DefaultKarma);
}
