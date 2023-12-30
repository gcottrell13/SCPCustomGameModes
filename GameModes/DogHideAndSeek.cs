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
using ServerEvent = Exiled.Events.Handlers.Server;
using PluginAPI.Events;
using Exiled.Events.EventArgs.Map;
using LightContainmentZoneDecontamination;
using PluginAPI.Roles;
using Exiled.Events.EventArgs.Interfaces;
using Exiled.Events.EventArgs.Scp914;
using Exiled.API.Features.Pickups;
using Scp914;
using PlayerRoles;
using Scp914.Processors;
using CommandSystem.Commands.RemoteAdmin;
using CommandSystem.Commands.RemoteAdmin.MutingAndIntercom;
using PlayerRoles.Voice;
using System.Reflection;
using InventorySystem;

namespace CustomGameModes.GameModes
{
    internal class DogHideAndSeek : IGameMode
    {
        public string Name => "DogHideAndSeek";

        Door beastDoor;
        bool beastReleased;
        bool DidTimeRunOut = false;
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

        public DogHideAndSeek()
        {
        }

        ~DogHideAndSeek()
        {
            OnRoundEnd();
        }

        public void OnRoundStart()
        {
            // -------------------------------------------------------------
            // Event Handlers
            // -------------------------------------------------------------
            PlayerEvent.InteractingDoor += OnInteractDoor;
            PlayerEvent.Hurting += OnHurting;
            PlayerEvent.SearchingPickup += OnSearchingPickup;
            PlayerEvent.DroppingAmmo += DeniableEvent;
            //PlayerEvent.DroppingItem += OnDropItem;
            PlayerEvent.PlayerDamageWindow += PlayerDamagingWindow;

            ServerEvent.RespawningTeam += DeniableEvent;

            Scp914Handler.Activating += Activate914;
            Scp914Handler.UpgradingPickup += UpgradePickup;
            //Scp914Handler.ChangingKnobSetting += DeniableEvent;
            //Scp914Handler.UpgradingPickup += DeniableEvent;
            Scp914Handler.UpgradingInventoryItem += DeniableEvent;
            //Scp914Handler.UpgradingPlayer += DeniableEvent;
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
            PlayerEvent.Hurting -= OnHurting;
            PlayerEvent.SearchingPickup -= OnSearchingPickup;
            PlayerEvent.DroppingAmmo -= DeniableEvent;
            //PlayerEvent.DroppingItem -= OnDropItem;
            PlayerEvent.PlayerDamageWindow -= PlayerDamagingWindow;

            ServerEvent.RespawningTeam -= DeniableEvent;

            Scp914Handler.Activating -= Activate914;
            Scp914Handler.UpgradingPickup -= UpgradePickup;
            //Scp914Handler.ChangingKnobSetting -= DeniableEvent;
            //Scp914Handler.UpgradingPickup -= DeniableEvent;
            Scp914Handler.UpgradingInventoryItem -= DeniableEvent;
            //Scp914Handler.UpgradingPlayer -= DeniableEvent;
            // -------------------------------------------------------------
            // -------------------------------------------------------------

            if (roundHandlerCO.IsRunning)
            {
                // If the SCP kills everyone, the coroutine will still be running. Kill it.
                Timing.KillCoroutines(roundHandlerCO);
            }
            // else, the Class-D survived long enough.

            Manager.PlayerDied -= onPlayerRoleDied;
            Manager.PlayerCompleteAllTasks -= onPlayerCompleteAllTasks;

            CountdownHelper.Stop();
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
                Manager.ClaimedPickups.Remove(e.Pickup);
            }
            else if (e.Pickup.PreviousOwner == e.Player)
            {
                // allow it
            }
            else if (e.Pickup.Type.IsAmmo() || e.Pickup.Type.IsWeapon())
            {
                // allow it
            }
            else
            {
                e.IsAllowed = false;
            }
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
            else if (Room.Get(RoomType.LczArmory).Doors.Contains(ev.Door) && ev.Door.IsOpen)
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

            if (ev.Player.IsHuman == ev.Attacker?.IsHuman)
                ev.IsAllowed = false;

            if (DidTimeRunOut && ev.Attacker?.Role.Team == Team.SCPs)
                ev.IsAllowed = false;
        }

        private void Activate914(ActivatingEventArgs ev)
        {
            if (Manager == null) { ev.IsAllowed = false; return; }
            if (!Manager.AllowedToUse914.Contains(ev.Player)) { ev.IsAllowed = false; return; };
        }

        private void UpgradePickup(UpgradingPickupEventArgs ev)
        {
            if (Manager == null) { ev.IsAllowed = false; return; }

            if (InventoryItemLoader.AvailableItems.TryGetValue(ev.Pickup.Type, out var value) && value.TryGetComponent<Scp914ItemProcessor>(out var processor))
            {
                var newPickupBase = processor.OnPickupUpgraded(ev.KnobSetting, ev.Pickup.Base, ev.OutputPosition);
                var newPickup = Pickup.Get(newPickupBase);
                Manager.ClaimedPickups.Remove(ev.Pickup);
                Manager.ClaimedPickups[newPickup] = ev.Pickup.PreviousOwner;
                if (newPickup != ev.Pickup)
                    ev.Pickup.Destroy();
                Log.Info($"914 created {newPickup.Type}");
            }
            ev.IsAllowed = false;
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

            foreach (var vtWindow in Room.Get(RoomType.LczPlants).Windows)
            {
                vtWindow.BreakWindow();
            }

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

            DoorsLockedExceptToRole.AddRange(Room.Get(RoomType.Lcz173).Doors.Where(d => d.IsGate));
            DoorsLockedExceptToRole.Add(Room.Get(RoomType.Lcz914).Doors.First(d => d.IsGate));
            DoorsLockedExceptToRole.AddRange(Room.Get(RoomType.Lcz330).Doors.Where(d => d.Rooms.Count == 1));

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

            double elapsedTime() => (DateTime.Now - timerStartedTime).TotalSeconds;
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
            while (elapsedTime() < timerTotalSeconds) yield return Timing.WaitForSeconds(1);

            // ----------------------------------------------------------------------------------------------------------------
            // ----------------------------------------------------------------------------------------------------------------

            beastReleased = true;
            timerTotalSeconds = RoundTime;
            timerStartedTime = DateTime.Now;
            getBroadcast = (p) => p.MainGameBroadcast;

            foreach (var beast in Manager.Beast())
                beast.player.ShowHint("Break Free", 7);

            removeTime(0);

            void ActivateBeastSickoMode()
            {
                Manager.BeastSickoModeActivate = true;
                foreach (var room in Room.Get(ZoneType.LightContainment))
                {
                    room.TurnOffLights(-1);
                    room.Color = Color.red;
                }
                getBroadcast = p => p.SickoModeBroadcast;
                removeTime(0);
            }


            while (elapsedTime() < timerTotalSeconds)
            {
                if (Manager.BeastSickoModeActivate == false && timerTotalSeconds - elapsedTime() < 70)
                    ActivateBeastSickoMode();
                yield return Timing.WaitForSeconds(1);
            }

            Manager.RemovingTime -= removeTime;
            Manager.PlayerDied -= onPlayerRoleDied;
            Manager.PlayerCompleteAllTasks -= onPlayerCompleteAllTasks;

            // ----------------------------------------------------------------------------------------------------------------
            // Time Ran out, humans win!
            // ----------------------------------------------------------------------------------------------------------------

            foreach (var room in Room.Get(ZoneType.LightContainment))
            {
                room.TurnOffLights(-1);
                room.Color = Color.white;
            }

            Cassie.MessageTranslated("The Class D Are Successful", "Class-D Win!");
            DidTimeRunOut = true;

            foreach (var classD in Player.List.Where(x => x.IsHuman))
            {
                var item = classD.AddItem(ItemType.MicroHID);
                classD.CurrentItem = item;
            }

            foreach (var player in Manager.ActiveRoles)
            {
                CountdownHelper.AddCountdown(player.player, player.RoundEndBroadcast, TimeSpan.FromSeconds(EndOfRoundTime));
                player.player.ShowHint(player.RoundEndBroadcast, EndOfRoundTime);
            }

            Manager.StopAll();

            yield return Timing.WaitForSeconds(EndOfRoundTime);

            //foreach (var beast in Manager.Beast())
            //{
            //    if (beast.player.IsAlive)
            //        beast.player.Hurt(-1);
            //}

            // ----------------------------------------------------------------------------------------------------------------
            // ----------------------------------------------------------------------------------------------------------------

            Round.Restart(false);

            while (true)
            {
                Log.Debug("Round STILL going");
                yield return Timing.WaitForSeconds(2);
            }
        }

        #endregion

        private void onPlayerRoleDied(Player ev)
        {
            Cassie.Clear();
            Cassie.DelayedMessage($"1 personnel is dead", 3f, isNoisy: false);

            var specRole = Manager.ApplyRoleToPlayer(ev, SpectatorRole.name);
            specRole.Start();
        }

        private void onPlayerCompleteAllTasks(Player ev)
        {
            Cassie.MessageTranslated("Personnel Has Completed All Tasks", $"{ev.DisplayNickname} has completed all tasks.", isNoisy: false);
        }
    }
}
