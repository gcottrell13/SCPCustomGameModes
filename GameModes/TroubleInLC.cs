using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
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
using CustomGameModes.API;
using Scp914;
using UnityEngine;
using Exiled.API.Features.Pickups;

namespace CustomGameModes.GameModes;

internal class TroubleInLC : IGameMode
{
    public bool? FFStateBefore = null;

    public const int DefaultKarma = 1000;
    public static Dictionary<Player, int> BaseKarma = new Dictionary<Player, int>();
    public Dictionary<Player, int> LiveKarma;

    public string Name => "Trouble In Light Containment";

    public string PreRoundInstructions => "Hidden <color=green>Traitors</color>, No Elevators, 914 on Rough hurts but reveals <color=green>Traitors</color>";

    public const string OpenStoreInstructions = "Use ALT to buy items.";
    public const string StoreCycleInstructions = "Press ALT to cycle menu\n";

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

    private Exiled.API.Features.Toys.Light? IntakeLight;
    private Exiled.API.Features.Toys.Light? OutputLight;
    private bool DetectedTraitor;
    private bool DidResetDetector;

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
        DidResetDetector = false;
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
        PlayerEvent.TogglingNoClip += OnToggleNoclip;
        PlayerEvent.UsedItem += OnUseItem;

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
        PlayerEvent.TogglingNoClip -= OnToggleNoclip;
        PlayerEvent.UsedItem -= OnUseItem;

        Scp914Event.UpgradingPlayer -= OnUpgradingPlayer;

        ServerEvent.RespawningTeam -= DeniableEvent;

        MapEvent.Decontaminating -= OnDecontaminating;

        if (FFStateBefore.HasValue)
            Server.FriendlyFire = FFStateBefore.Value;
    }

    private IEnumerator<float> roundCoroutine()
    {
        yield return Timing.WaitForOneFrame;
        float updateInterval = 1f;

        foreach (var player in Player.List)
        {
            Pickup.CreateAndSpawn(
                ItemType.GunCOM15, 
                RoleTypeId.Scientist.GetRandomSpawnLocation().Position + Vector3.up + Vector3.back * UnityEngine.Random.Range(-1, 1) + Vector3.left * UnityEngine.Random.Range(-1, 1), 
                default);
        }

        float lockTime = 30;
        CountdownHelper.AddCountdownForAll("Class-D Door Opens in:", TimeSpan.FromSeconds(lockTime));
        foreach (var door in Room.Get(RoomType.LczClassDSpawn).Doors)
        {
            if (door.Rooms.Count == 2)
                door.Lock(lockTime, DoorLockType.Regular079);
        }

        while (Round.InProgress)
        {
            yield return Timing.WaitForSeconds(updateInterval);
            UpdateLights();

            if (DateTime.Now - LastDetection > TraitorDetectorCooldown && !DidResetDetector)
            {
                DetectedTraitor = false;
                DidResetDetector = true;
            }

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
                        if (UnityEngine.Random.Range(0, 101) > 20)
                        {
                            player.Role.Set(RoleTypeId.ChaosRepressor, RoleSpawnFlags.None);
                        }
                        else if (player.Role.Type switch { RoleTypeId.Scp049 | RoleTypeId.Scp096 | RoleTypeId.Scp079 | RoleTypeId.Scp173 | RoleTypeId.Scp049 => true, _ => false})
                        {
                            player.Role.Set(RoleTypeId.Scp939, RoleSpawnFlags.None);
                        }
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
        ci.AddItem(ItemType.KeycardScientist);
        ci.AddItem(ItemType.Medkit);
        ci.AddItem(ItemType.Flashlight);
        ShowBaseKarmaOnPlayer(ci);
        ci.ChangeAppearance(RoleTypeId.Scientist, scientists);
        if (ci.IsScp)
        {
            ci.VoiceChannel = VoiceChat.VoiceChatChannel.Proximity;
            ci.ShowHint($"""
                    You are an SCP Traitor! Kill all the Innocents (<color=yellow>Scientists</color>).
                    You can earn shop credits by killing Innocents (starting {GetCredits(ci)} credits).

                    Be wary of using <color=red>SCP Abilities</color> where the Innocents can see you!
                    <color=red>SCP Attacks</color> won't do damage unless you have obtained a weapon.
                    Use the Store to pick up nearby items.

                    {OpenStoreInstructions}
                    """, 45);
        }
        else
        {
            ci.ShowHint($"""
                    You are a Traitor! Kill all the Innocents (<color=yellow>Scientists</color>).
                    You can earn shop credits by killing Innocents (starting {GetCredits(ci)} credits).
                    {OpenStoreInstructions}
                    """, 45);
        }
    }

    public void SetupScientist(Player scientist)
    {
        scientist.Role.Set(RoleTypeId.Scientist);
        scientist.Position = RoleTypeId.ClassD.GetRandomSpawnLocation().Position;
        scientist.AddItem(ItemType.Flashlight);

        if (Detectives.Contains(scientist))
        {
            Credits[scientist] = config.TttDetectiveStartCredits;
            scientist.ShowHint($"""
                You are a Detective! You can discover a body's killer.
                You can earn shop credits when a Traitor dies (starting {GetCredits(scientist)} credits).
                {OpenStoreInstructions}
                """, 45);
        }
        else
        {
            scientist.ShowHint($"""
                You are an Innocent! Avoid dying, and work together to shoot Traitors!
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

    public void OnUseItem(UsedItemEventArgs ev)
    {
        if (ev.Player.IsScp && ev.Item.Type == ItemType.Medkit)
        {
            ev.Player.Heal(ev.Player.MaxHealth / 2);
        }
    }

    public void OnUpgradingPlayer(UpgradingPlayerEventArgs ev)
    {
        if (ev.KnobSetting == Scp914KnobSetting.Rough)
        {
            if (DateTime.Now - LastDetection > TraitorDetectorCooldown)
            {
                DidResetDetector = false;
                ev.Player.Health /= 2;
                if (ev.Player.Role != RoleTypeId.Scientist)
                {
                    DetectedTraitor = true;
                    foreach (Player player in Player.List)
                    {
                        player.ShowHint("There is at least one <color=green>Traitor</color> in SCP 914", 10);
                    }
                }
                LastDetection = DateTime.Now - TimeSpan.FromSeconds(1);
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
        else if (ev.Attacker.Role.Team != Team.FoundationForces)
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
        else if (ev.Player.Role.Team != Team.FoundationForces)
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

        if (ev.Attacker.IsScp && !ev.Attacker.Items.Any(x => x.IsWeapon))
        {
            ev.Attacker.ShowHint("You cannot use SCP Attacks unless you have a weapon in your inventory");
            ev.IsAllowed = false;
            return;
        }

        if (ev.Player.IsScp)
        {
            ev.Amount = ev.Player.MaxHealth / 5;
        }

        ev.Amount *= DamageMultiplierFromKarma(ev.Attacker);

        if (ev.Player.Role.Team == ev.Attacker.Role.Team)
        {
            DamagedATeammate.Add(ev.Attacker);
            AdjustKarma(ev.Attacker, -20);
        }
    }

    public void OnJoin(JoinedEventArgs ev)
    {
        foreach (Player player in Player.Get(x => x.Role != RoleTypeId.Scientist))
        {
            player.ChangeAppearance(RoleTypeId.Scientist, new[] { ev.Player });
        }
    }

    public IEnumerator<float> OnSpawned(SpawnedEventArgs ev)
    {
        if (ev.Player.Role.Type == RoleTypeId.Spectator)
        {
            yield break;
        }
        else if (TrackedPlayers.Contains(ev.Player))
        {
            if (ev.Player.Role != RoleTypeId.Scientist)
            {
                Log.Debug($"{ev.Player.DisplayNickname} spawned as {ev.Player.Role.Type}, setting appearance to scientist");
                yield return Timing.WaitForSeconds(0.5f);
                ev.Player.ChangeAppearance(RoleTypeId.Scientist, scientists);
            }
        }
        else
        {
            TrackedPlayers.Add(ev.Player);
            SetupScientist(ev.Player);
        }
    }

    public void OnDecontaminating(DecontaminatingEventArgs ev)
    {
        ev.IsAllowed = false;
        foreach (Player player in Player.Get(x => x.Role != RoleTypeId.Scientist))
        {
            player.Role.Set(RoleTypeId.Spectator);
        }
    }

    public IEnumerator<float> OnToggleNoclip(TogglingNoClipEventArgs ev)
    {
        yield return Timing.WaitForOneFrame;
        if (PlayerHintMenu.ByPlayerDict.TryGetValue(ev.Player, out var menu))
        {
            var timeToWaitForSelection = 1f;
            menu.Next();
            var current = menu.GetCurrent();
            if (current != null)
                menu.CountdownToSelect($"{StoreCycleInstructions}{current.ActionName} in: ", timeToWaitForSelection);
        }
        else
        {
            List<HintMenuItem> list = new List<HintMenuItem>();

            if (ev.Player.IsScp)
            {
                list.Add(new HintMenuItem(text: () => "Your Inventory:\n-"));
                if (ev.Player.Items.Count < 8)
                {
                    if (getClosePickup(ev.Player) is Pickup closestPickup)
                    {
                        list.Add(new HintMenuItem(
                            actionName: $"Pick up item",
                            text: () =>
                            {
                                closestPickup = getClosePickup(ev.Player);
                                return ColorHelper.Color(Misc.PlayerInfoColorTypes.Emerald, $"Pick up {closestPickup?.Type}");
                            },
                            onSelect: () =>
                            {
                                if (closestPickup?.IsSpawned == true)
                                {
                                    ev.Player.AddItem(closestPickup);
                                    closestPickup.UnSpawn();
                                    return "Picked up item";
                                }
                                return "Item is gone";
                            }
                        ));
                    }
                }
                if (ev.Player.CurrentItem != null)
                {
                    list.Add(new HintMenuItem(
                        actionName: $"Stow {ev.Player.CurrentItem.Type}",
                        text: () => ColorHelper.Color(Misc.PlayerInfoColorTypes.BlueGreen, $"Stow {ev.Player.CurrentItem.Type}"),
                        onSelect: () =>
                        {
                            ev.Player.CurrentItem = null;
                            return $"Put Away Item";
                        }
                    ));
                    list.Add(new HintMenuItem(
                        actionName: $"Drop {ev.Player.CurrentItem.Type}",
                        text: () => ColorHelper.Color(Misc.PlayerInfoColorTypes.BlueGreen, $"Drop {ev.Player.CurrentItem.Type}"),
                        onSelect: () =>
                        {
                            ev.Player.DropItem(ev.Player.CurrentItem);
                            return $"Dropped Item";
                        }
                    ));
                }

                // show inventory
                foreach (Item item in ev.Player.Items)
                {
                    var thisItem = item;
                    if (item == ev.Player.CurrentItem)
                        continue;
                    list.Add(new HintMenuItem(
                        actionName: $"Select {thisItem.Type}",
                        text: () => ColorHelper.Color(Misc.PlayerInfoColorTypes.Yellow, thisItem.Type.ToString()),
                        onSelect: () =>
                        {
                            ev.Player.CurrentItem = thisItem;
                            return $"Switched to {thisItem.Type}";
                        }
                    ));
                    //list.Add(new HintMenuItem(
                    //    actionName: $"Drop {thisItem.Type}",
                    //    text: () => $"Drop {thisItem.Type}",
                    //    onSelect: () =>
                    //    {
                    //        ev.Player.DropItem(thisItem);
                    //        return $"Dropped Item";
                    //    }
                    //));
                }

                list.Last().ColumnBreakAfter = true;
            }

            list.Add(new HintMenuItem(
                text: () =>
                {
                    var store = config.TttStore;
                    var balance = GetCredits(ev.Player);
                    return $"""
                        Your balance: {balance} credits
                        -
                        """;
                }));

            var shopItemCount = 0;
            foreach (var shopItem in config.TttStore)
            {
                shopItemCount++;
                var name = shopItem.Key;
                var cost = shopItem.Value;
                list.Add(new HintMenuItem(
                    actionName: $"Buy {name}",
                    text: () =>
                    {
                        var credits = GetCredits(ev.Player);
                        var text = $"{name} - {cost} credits";
                        if (credits < cost)
                        {
                            text = $"<color=red>{text}</color>";
                        }
                        return text;
                    }, 
                    onSelect: () =>
                    {
                        var credits = GetCredits(ev.Player);
                        if (credits >= cost)
                        {
                            AddCredits(ev.Player, -cost);
                            var newItem = ev.Player.AddItem(name);
                            if (!ev.Player.IsScp)
                                return $"Bought {name}";
                            ev.Player.CurrentItem = newItem;
                            return $"Bought {name} - Access Inventory with TAB";
                        }
                        return "Insufficient funds";
                    }
                )
                {
                    ColumnBreakAfter = shopItemCount % 6 == 0,
                });
            }

            menu = new PlayerHintMenu(ev.Player, list);
            menu.CountdownToSelect($"{StoreCycleInstructions}Closing Menu In: ", 10);
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
        if (IntakeLight == null || OutputLight == null)
            return;
        var detectorCd = (DateTime.Now - LastDetection).TotalSeconds;
        var lightColor = Color.white;
        if (detectorCd >= TraitorDetectorCooldown.TotalSeconds)
        {
            lightColor = Color.green;
        }
        else if (detectorCd < TraitorDetectorCooldown.TotalSeconds / 2)
        {
            lightColor = DetectedTraitor ? Color.red : Color.blue;
        }

        if (lightColor != IntakeLight.Color)
        {
            IntakeLight.Color = lightColor;
            OutputLight.Color = lightColor;
        }
    }

    private Pickup? getClosePickup(Player player)
    {
        var closestPickup = player.CurrentRoom.Pickups
            .Where(x => x.IsSpawned)
            .OrderBy(x => (x.Position - player.Position).sqrMagnitude)
            .FirstOrDefault();
        if (closestPickup == null)
            return null;
        return (closestPickup.Position - player.Position).sqrMagnitude < 10 ? closestPickup : null;
    }
}
