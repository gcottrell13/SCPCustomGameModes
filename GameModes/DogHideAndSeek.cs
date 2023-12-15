using CustomGameModes.API;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.DamageHandlers;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Spawn;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using MEC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using PlayerEvent = Exiled.Events.Handlers.Player;
using MapEvent = Exiled.Events.Handlers.Map;
using Scp914Handler = Exiled.Events.Handlers.Scp914;
using PluginAPI.Events;
using Exiled.Events.EventArgs.Map;
using LightContainmentZoneDecontamination;
using PluginAPI.Roles;
using Exiled.Events.EventArgs.Interfaces;
using Exiled.Events.EventArgs.Scp914;
using Exiled.API.Features.Pickups;
using Scp914;
using PlayerRoles;

namespace CustomGameModes.GameModes
{
    internal class DogHideAndSeek : IGameMode
    {
        Door beastDoor;
        bool beastReleased;
        bool classDWin = false;
        List<Door> LCZDoors = new();
        public static DhasRoleManager Manager;
        bool cassieBeastEscaped = false;

        List<Door> DoorsLockedExceptToRole = new();
        HashSet<Door> DoorsReopenAfterClosing = new();

        CoroutineHandle roundHandlerCO;

        int CountdownTime = 65;
        int RoundTime = 10 * 60;
        // int RoundTime = 60;

        int EndOfRoundTime = 30;

        public void OnRoundStart()
        {
            // -------------------------------------------------------------
            // Event Handlers
            // -------------------------------------------------------------
            PlayerEvent.InteractingDoor += OnInteractDoor;
            MapEvent.SpawningTeamVehicle += OnSpawnTeam;
            PlayerEvent.Hurting += OnHurting;
            PlayerEvent.SearchingPickup += OnSearchingPickup;
            PlayerEvent.DroppingAmmo += DeniableEvent;
            PlayerEvent.DroppingItem += OnDropItem;
            PlayerEvent.PlayerDamageWindow += PlayerDamagingWindow;

            Scp914Handler.Activating += Activate914;
            Scp914Handler.UpgradingPickup += UpgradePickup;
            //Scp914Handler.ChangingKnobSetting += DeniableEvent;
            //Scp914Handler.UpgradingPickup += DeniableEvent;
            Scp914Handler.UpgradingInventoryItem += DeniableEvent;
            Scp914Handler.UpgradingPlayer += DeniableEvent;
            // -------------------------------------------------------------
            // -------------------------------------------------------------

            DecontaminationController.Singleton.NetworkDecontaminationOverride = DecontaminationController.DecontaminationStatus.Disabled;

            roundHandlerCO = Timing.RunCoroutine(_wrapRoundHandle());
        }

        public void OnRoundEnd()
        {
            Log.Info("Stopping Game");

            // -------------------------------------------------------------
            // Event Handlers
            // -------------------------------------------------------------
            PlayerEvent.InteractingDoor -= OnInteractDoor;
            MapEvent.SpawningTeamVehicle -= OnSpawnTeam;
            PlayerEvent.Hurting -= OnHurting;
            PlayerEvent.SearchingPickup -= OnSearchingPickup;
            PlayerEvent.DroppingAmmo -= DeniableEvent;
            PlayerEvent.DroppingItem -= OnDropItem;
            PlayerEvent.PlayerDamageWindow -= PlayerDamagingWindow;

            Scp914Handler.Activating -= Activate914;
            Scp914Handler.UpgradingPickup -= UpgradePickup;
            //Scp914Handler.ChangingKnobSetting -= DeniableEvent;
            //Scp914Handler.UpgradingPickup -= DeniableEvent;
            Scp914Handler.UpgradingInventoryItem -= DeniableEvent;
            Scp914Handler.UpgradingPlayer -= DeniableEvent;
            // -------------------------------------------------------------
            // -------------------------------------------------------------

            if (roundHandlerCO.IsRunning)
            {
                // If the SCP kills everyone, the coroutine will still be running. Kill it.
                Timing.KillCoroutines(roundHandlerCO);
            }
            else if (!Player.List.Any(p => p.Role.Team == PlayerRoles.Team.SCPs))
            {
                // the SCP got disconnected early
                CountdownHelper.Stop();
            }
            // else, the Class-D survived long enough.

            Manager.StopAll();
        }

        public void OnWaitingForPlayers()
        {
            DecontaminationController.Singleton.NetworkDecontaminationOverride = DecontaminationController.DecontaminationStatus.None;

        }

        #region Event Handlers

        private void PlayerDamagingWindow(DamagingWindowEventArgs e)
        {
            if (e.Player.Role.Team == Team.SCPs && beastReleased)
            {
                if (!cassieBeastEscaped)
                {
                    cassieBeastEscaped = true;
                    Cassie.MessageTranslated("S C P 9 3 9 has escaped containment", "SCP-939 Has Escaped Containment");
                }
            }
            else
            {
                e.IsAllowed = false;
            }
        }

        private void OnSearchingPickup(SearchingPickupEventArgs e)
        {
            if (Manager == null) { e.IsAllowed = false; return; }
            if (Manager.ClaimedPickups.TryGetValue(e.Pickup, out var assignedPlayer) && e.Player == assignedPlayer)
            {
                // allow it
            }
            else if (e.Pickup.PreviousOwner == e.Player)
            {
                // allow it
            }
            else
            {
                e.IsAllowed = false;
            }
        }

        private void OnDropItem(DroppingItemEventArgs e)
        {
            if (Manager == null) { e.IsAllowed = false; return; }
            if (Manager.ItemsToDrop.TryGetValue(e.Player, out var items) && items.Contains(e.Item.Type))
            {
                // allow it
            }
            else
            {
                e.IsAllowed = false;
            }
        }

        private void OnSpawnTeam(SpawningTeamVehicleEventArgs ev)
        {
            // should be enough to prevent MTF and Chaos from spawning?
            // effectively keeping everyone as spectator until the end of the round
            ev.IsAllowed = false;
        }

        private void OnInteractDoor(InteractingDoorEventArgs ev)
        {
            if (Manager == null) { ev.IsAllowed = false; return; }
            if (DoorsLockedExceptToRole.Contains(ev.Door))
            {
                if (Manager.DoorsToOpen.TryGetValue(ev.Door, out var assignedPlayers) && assignedPlayers.Contains(ev.Player))
                {
                    // allow it
                }
                else
                {
                    ev.IsAllowed = false;
                }
            }
            else if (ev.Door == beastDoor || ev.Door.Zone != ZoneType.LightContainment)
            {
                ev.IsAllowed = false;
            }
            else
            {
                if (DoorsReopenAfterClosing.Contains(ev.Door) && ev.Door.IsOpen)
                {
                    Timing.CallDelayed(2f, () =>
                    {
                        ev.Door.IsOpen = true;
                    });
                }
            }
        }

        private void OnHurting(HurtingEventArgs ev)
        {
            // allow the Class-D to hurt/kill the beast after they win
            // disallow the beast to hurt the Class-D after it loses
            if (classDWin)
            {
                if (ev.Attacker?.Role.Team == Team.SCPs)
                {
                    ev.IsAllowed = false;
                }
            }
            else if (ev.Attacker != null && Manager.HurtRoles.TryGetValue(ev.Attacker, out var roles) && roles.Contains(ev.Player.Role.Type))
            {
                // allow it
            }
            else
            {
                if (ev.Attacker?.Role.Team == Team.ClassD)
                {
                    ev.IsAllowed = false;
                }
            }
        }

        private void Activate914(ActivatingEventArgs ev)
        {
            if (Manager == null) { ev.IsAllowed = false; return; }
            if (!Manager.AllowedToUse914.Contains(ev.Player)) { ev.IsAllowed = false; return; };
        }

        private void UpgradePickup(UpgradingPickupEventArgs ev)
        {
            if (Manager == null) { ev.IsAllowed = false; return; }
            if (ev.Pickup.Type.IsKeycard())
            {
                ev.IsAllowed = false;
                ev.Pickup.UnSpawn();
                var newType = ev.KnobSetting switch
                {
                    Scp914KnobSetting.VeryFine => ItemType.KeycardO5,
                    Scp914KnobSetting.Fine => ItemType.KeycardResearchCoordinator,
                    Scp914KnobSetting.OneToOne => ev.Pickup.Type,
                    Scp914KnobSetting.Coarse => ItemType.KeycardJanitor,
                    Scp914KnobSetting.Rough => ItemType.KeycardJanitor,
                };
                var newPickup = Pickup.CreateAndSpawn(newType, ev.OutputPosition, ev.Pickup.Rotation, ev.Pickup.PreviousOwner);
            }
            else
            {
                ev.IsAllowed = false;
            }
        }

        public void DeniableEvent(IDeniableEvent ev)
        {
            ev.IsAllowed = false;
        }

        #endregion

        #region GameHandle

        private IEnumerator<float> _wrapRoundHandle()
        {
            var r = _roundHandle();
            var d = true;
            while (d)
            {
                try
                {
                    d = r.MoveNext();
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    break;
                }
                if (d)
                    yield return r.Current;
            }
        }

        private IEnumerator<float> _roundHandle()
        {
            List<Player> players = Player.List.ToList();
            players.ShuffleList();

            Log.Debug("Starting a new game");

            Manager = new DhasRoleManager();

            var iterator = 0;

            // ----------------------------------------------------------------------------------------------------------------
            // ----------------------------------------------------------------------------------------------------------------
            #region Set up Doors
            foreach (var outsideDoor in Door.Get(door => door.Zone != ZoneType.LightContainment))
            {
                outsideDoor.IsOpen = false;
            }

            LCZDoors = Door.Get(door => door.Zone == ZoneType.LightContainment).ToList();

            beastDoor = Room.Get(RoomType.LczGlassBox).Doors.First(d => d.Rooms.Count == 1 && !d.IsGate);

            var doorsDoNotOpen = new HashSet<Door>()
            {
                Room.Get(RoomType.Lcz173).Doors.First(d => d.IsGate),
                Room.Get(RoomType.LczArmory).Doors.First(d => d.Rooms.Count == 1),
                Door.Get(DoorType.CheckpointLczA),
                Door.Get(DoorType.CheckpointLczB),
                beastDoor,
            };

            foreach (var door in LCZDoors)
            {
                if (door.Rooms.Count == 1 && door.Room.RoomName == MapGeneration.RoomName.LczCheckpointA) doorsDoNotOpen.Add(door);
                if (door.Rooms.Count == 1 && door.Room.RoomName == MapGeneration.RoomName.LczCheckpointB) doorsDoNotOpen.Add(door);
                if (door.Rooms.Count == 2 && door.DoorLockType == DoorLockType.None) DoorsReopenAfterClosing.Add(door);
            }

            DoorsLockedExceptToRole.Add(Door.Get(DoorType.CheckpointLczA));
            DoorsLockedExceptToRole.Add(Door.Get(DoorType.CheckpointLczB));
            DoorsLockedExceptToRole.AddRange(Room.Get(RoomType.Lcz173).Doors.Where(d => d.IsGate));
            DoorsLockedExceptToRole.Add(Room.Get(RoomType.Lcz914).Doors.First(d => d.IsGate));

            foreach (var lczdoor in LCZDoors)
            {
                lczdoor.IsOpen = !doorsDoNotOpen.Contains(lczdoor);
            }

            foreach (var shouldBeClosed in doorsDoNotOpen)
            {
                shouldBeClosed.IsOpen = false;
            }

            #endregion
            // ----------------------------------------------------------------------------------------------------------------
            // ----------------------------------------------------------------------------------------------------------------
            #region Set up Players

            var roles = Manager.RoleDistribution;

            while (iterator < players.Count)
            {
                var player = players[iterator];
                Manager.ApplyRoleToPlayer(player, roles[iterator]);
                iterator++;
            }

            #endregion
            // ----------------------------------------------------------------------------------------------------------------
            // ----------------------------------------------------------------------------------------------------------------
            #region Lights

            yield return Timing.WaitForSeconds(2);

            // turn off all the lights in each room except for those that have players in them
            {
                var playerRooms = Manager.Humans().Select(role => role.player.CurrentRoom);
                foreach (var room in Room.Get(ZoneType.LightContainment))
                {
                    if (playerRooms.Contains(room)) continue;
                    room.TurnOffLights(9999f);
                }
            }

            #endregion
            // ----------------------------------------------------------------------------------------------------------------
            // ----------------------------------------------------------------------------------------------------------------

            var timerTotalSeconds = CountdownTime;
            var timerStartedTime = DateTime.Now;

            var getBroadcast = (DhasRole p) => p.CountdownBroadcast;

            void removeTime(int seconds)
            {
                var elapsed = (int)(DateTime.Now - timerStartedTime).TotalSeconds;
                timerTotalSeconds -= seconds;
                var remainingSeconds = timerTotalSeconds - elapsed;
                foreach (var playerRole in Manager.ActiveRoles)
                    CountdownHelper.AddCountdown(playerRole.player, getBroadcast(playerRole), TimeSpan.FromSeconds(remainingSeconds));
            }

            Manager.StartAll();
            removeTime(0);
            Manager.RemovingTime += removeTime;
            Manager.PlayerDied += onPlayerRoleDied;
            Manager.PlayerCompleteAllTasks += onPlayerCompleteAllTasks;
            while ((DateTime.Now - timerStartedTime).TotalSeconds < timerTotalSeconds) yield return Timing.WaitForSeconds(1);

            // ----------------------------------------------------------------------------------------------------------------
            // ----------------------------------------------------------------------------------------------------------------

            beastReleased = true;
            timerTotalSeconds = RoundTime;
            timerStartedTime = DateTime.Now;
            getBroadcast = (p) => p.MainGameBroadcast;

            foreach (var beast in Manager.Beast())
                beast.player.ShowHint("Break Free", 7);

            removeTime(0);
            while ((DateTime.Now - timerStartedTime).TotalSeconds < timerTotalSeconds) yield return Timing.WaitForSeconds(1);

            Manager.RemovingTime -= removeTime;
            Manager.PlayerDied -= onPlayerRoleDied;
            Manager.PlayerCompleteAllTasks -= onPlayerCompleteAllTasks;

            // ----------------------------------------------------------------------------------------------------------------
            // Time Ran out, humans win!
            // ----------------------------------------------------------------------------------------------------------------

            foreach (var room in Room.Get(ZoneType.LightContainment))
            {
                room.TurnOffLights(-1);
            }

            Cassie.MessageTranslated("The Class D Are Successful", "Class-D Win!");

            Manager.StopAll();

            foreach (var classD in Player.List.Where(x => x.Role.Team switch
            {
                Team.Dead => false,
                Team.SCPs => false,
                _ => true,
            }))
            {
                classDWin = true;
                var item = classD.AddItem(ItemType.MicroHID);
                classD.CurrentItem = item;
            }

            foreach (var dclass in Manager.ActiveRoles)
                CountdownHelper.AddCountdown(dclass.player, dclass.RoundEndBroadcast, TimeSpan.FromSeconds(EndOfRoundTime));

            yield return Timing.WaitForSeconds(EndOfRoundTime);

            foreach (var beast in Manager.Beast()) 
                beast.player.Hurt(-1);
            // ----------------------------------------------------------------------------------------------------------------
            // ----------------------------------------------------------------------------------------------------------------
        }

        #endregion

        private void onPlayerRoleDied(Player ev)
        {
            var playerDeadCount = Manager.Humans().Count();
            Cassie.Clear();
            Cassie.DelayedMessage($"{playerDeadCount} personnel are dead", 3f, isNoisy: false);
        }

        private void onPlayerCompleteAllTasks(Player ev)
        {
            Cassie.MessageTranslated("Personnel Has Completed All Tasks", $"{ev.DisplayNickname} has completed all tasks.", isNoisy: false);
        }
    }
}
