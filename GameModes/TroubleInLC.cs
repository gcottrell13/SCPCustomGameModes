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
using PluginAPI.Roles;
using Scp914;
using UnityEngine;

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

    public List<Player> scientists = new List<Player>();
    public List<Player> troublemakers = new List<Player>();

    private TimeSpan TraitorDetectorCooldown = TimeSpan.FromMinutes(2);
    DateTime LastDetection = DateTime.MinValue;

    private Exiled.API.Features.Toys.Light IntakeLight;
    private Exiled.API.Features.Toys.Light OutputLight;
    private bool DetectedTraitor;

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
        LastDetection = DateTime.MinValue;
        DetectedTraitor = false;
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
        Credits.Clear();
        SubscribeEventHandlers();

        IntakeLight = Exiled.API.Features.Toys.Light.Create(Scp914Controller.Singleton.IntakeChamber.position);
        OutputLight = Exiled.API.Features.Toys.Light.Create(Scp914Controller.Singleton.OutputChamber.position);
        IntakeLight.Intensity = 5f;
        OutputLight.Intensity = 5f;

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
            UpdateLights();

            foreach (Player player in Player.List)
            {
                if (player.Zone != ZoneType.LightContainment)
                {
                    player.Position = RoleTypeId.Scientist.GetRandomSpawnLocation().Position;
                }
            }

            try
            {

                foreach (Player detective in Detectives)
                {
                    if (!detective.IsAlive)
                        continue;

                    foreach (Ragdoll ragdoll in Ragdoll.List)
                    {
                        if (RevealedBodies.Contains(ragdoll))
                            continue;
                        if ((detective.Position - ragdoll.Position).magnitude > 2)
                            continue;
                        RevealedBodies.Add(ragdoll);
                        var killerName = Killers.TryGetValue(ragdoll.Owner, out var killer) ? killer?.DisplayNickname : "Unknown Killer";
                        ragdoll.Nickname = $"Killed By {killerName} - {ragdoll.Owner.DisplayNickname}"; // 's body - they were Scientist
                        ragdoll.Owner.CustomName = $"{ragdoll.Owner.DisplayNickname} - Killed by {killerName}";
                        Log.Info($"Setting ragdoll nickname: {ragdoll.Nickname}");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

    }

    public void SetupPlayers()
    {
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
                        break;
                    }
                default:
                    {
                        scientists.Add(player);
                        if (player.Role == RoleTypeId.Scientist)
                        {
                            Detectives.Add(player);
                        }
                        break;
                    }
            }
        }

        Timing.CallDelayed(0.3f, () =>
        {
            foreach (Player ci in troublemakers)
            {
                try
                {
                    SetupTraitor(ci);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }

            foreach (Player scientinst in scientists)
            {
                try
                {
                    SetupScientist(scientinst);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }

            Round.IsLocked = false;
        });
    }

    public void SetupTraitor(Player ci)
    {
        Credits[ci] = config.TttTraitorStartCredits;
        ci.Position = RoleTypeId.ClassD.GetRandomSpawnLocation().Position;
        ci.AddItem(FirearmType.Com15, new AttachmentIdentifier[] { });
        ci.AddItem(ItemType.KeycardScientist);
        ci.AddItem(ItemType.Medkit);
        ci.AddItem(ItemType.Flashlight);
        ci.ShowHint($"""
                    You are a Traitor! Kill everyone who isn't Chaos Insurgency.
                    You can earn shop credits by killing Innocents (starting {GetCredits(ci)} credits).
                    {OpenStoreInstructions}
                    """, 30);
        ShowBaseKarmaOnPlayer(ci);
        ci.ChangeAppearance(RoleTypeId.Scientist, scientists);
    }

    public void SetupScientist(Player scientist)
    {
        scientist.Role.Set(RoleTypeId.Scientist);
        scientist.AddItem(FirearmType.Com15, new AttachmentIdentifier[] { });
        scientist.Position = RoleTypeId.ClassD.GetRandomSpawnLocation().Position;
        scientist.AddItem(ItemType.Flashlight);

        if (Detectives.Contains(scientist))
        {
            Credits[scientist] = config.TttTraitorStartCredits;
            scientist.ShowHint($"""
                You are a Detective! You can discover a body's killer.
                You can earn shop credits when a Traitor dies (starting {GetCredits(scientist)} credits).
                {OpenStoreInstructions}
                """, 30);
        }
        else
        {
            scientist.ShowHint($"""
                You are an Innocent! Avoid dying, and shoot Traitors!
                """, 30);
        }
        ShowBaseKarmaOnPlayer(scientist);
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
        return DefaultKarma;
    }

    public void ShowBaseKarmaOnPlayer(Player player)
    {
        player.CustomInfo = KarmaRank(player);
        if (Detectives.Contains(player))
        {
            player.CustomInfo = $"{player.CustomInfo} - Detective";
        }
    }

    public void AddCredits(Player player, int amount)
    {
        if (!Credits.ContainsKey(player))
            Credits[player] = 0;
        Credits[player] += amount;
    }

    public int GetCredits(Player player)
    {
        return Credits.TryGetValue(player, out var credits) ? credits : 0;
    }

    public void OnElevator(InteractingElevatorEventArgs ev)
    {
        ev.IsAllowed = false;
    }

    public void OnUpgradingPlayer(UpgradingPlayerEventArgs ev)
    {
        if (ev.KnobSetting == Scp914KnobSetting.Rough)
        {
            if (DateTime.Now - LastDetection > TraitorDetectorCooldown)
            {
                ev.Player.Health /= 2;
                if (ev.Player.IsCHI)
                {
                    DetectedTraitor = true;
                    foreach (Player player in Player.List)
                    {
                        player.ShowHint("There is at least one <color=green>Traitor</color> in SCP 914");
                    }
                }
                LastDetection = DateTime.Now;
                UpdateLights();
            }
        }
    }

    public void OnDying(DyingEventArgs ev)
    {
        if (ev.Player == null || ev.Attacker == null)
            return;

        Killers[ev.Player] = ev.Attacker;

        if (ev.Player.Role.Team == ev.Attacker.Role.Team)
        {
            AdjustKarma(ev.Attacker, -GetKarma(ev.Player) / 10);
        }
        else if (ev.Attacker.Role.Team == Team.ChaosInsurgency)
        {
            var reward = Detectives.Contains(ev.Player) ? config.TttKillInnocentReward * 2 : config.TttKillInnocentReward;

            AddCredits(ev.Attacker, reward);
            ev.Attacker.ShowHint($"""
                You got {reward} credits for killing {ev.Player.DisplayNickname}.
                {OpenStoreInstructions}
                """, 30);

            ev.DamageHandler.Attacker = null;
            AdjustKarma(ev.Attacker, 5);
        }
        else if (ev.Player.Role.Team == Team.ChaosInsurgency)
        {
            foreach (Player detective in Detectives)
            {
                AddCredits(detective, config.TttCiDyingReward);
                detective.ShowHint($"""
                    You got {config.TttCiDyingReward} credits because a Traitor died.
                    {OpenStoreInstructions}
                    """, 30);
            }
            AdjustKarma(ev.Attacker, 10);
        }
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
        {
            if (ev.Player.Role.Team == Team.SCPs)
            {
                ev.Player.Health = 1;
            }

            return;
        }

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

    public void UpdateLights()
    {
        var detectorCd = (DateTime.Now - LastDetection).TotalSeconds;
        var lightColor = Color.white;
        if (detectorCd >= TraitorDetectorCooldown.TotalSeconds)
        {
            lightColor = Color.green;
        }
        else if (detectorCd < 15)
        {
            lightColor = DetectedTraitor ? Color.red : Color.blue;
        }

        if (lightColor != IntakeLight.Color)
        {
            IntakeLight.Color = lightColor;
            OutputLight.Color = lightColor;
        }
    }
}
