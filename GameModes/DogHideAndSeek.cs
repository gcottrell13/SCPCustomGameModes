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
        Player beast;
        List<Player> ClassD = new();
        public static DhasRoleManager Manager;
        bool cassieBeastEscaped = false;

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

        public void OnRoundEnd(RoundEndedEventArgs ev)
        {
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
            if (e.Player.Role.Team == PlayerRoles.Team.SCPs && beastReleased)
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
            if (Manager.DoorsToOpen.TryGetValue(ev.Door, out var assignedPlayers) && assignedPlayers.Contains(ev.Player))
            {
                // allow it
            }
            else
            {
                var allowedToggle = ev.Door.Type switch
                {
                    DoorType.Airlock => true,
                    DoorType.LczArmory => true,
                    DoorType.LczWc => true,
                    DoorType.LczCafe => true,
                    _ => false,
                };

                if (!allowedToggle)
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
                if (ev.Attacker?.Role.Team == PlayerRoles.Team.SCPs)
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
                if (ev.Attacker?.Role.Team == PlayerRoles.Team.ClassD)
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

            beastDoor = Room.Get(RoomType.LczGlassBox).Doors.First(d => d.Rooms.Count == 1 && d.IsGate);
            var innerGR18Door = Room.Get(RoomType.LczGlassBox).Doors.First(d => d.Rooms.Count == 1 && !d.IsGate);

            var doorsDoNotOpen = new HashSet<Door>()
            {
                Room.Get(RoomType.Lcz173).Doors.First(d => d.IsGate),
                Room.Get(RoomType.LczArmory).Doors.First(d => d.Rooms.Count == 1),
                Door.Get(DoorType.CheckpointLczA),
                Door.Get(DoorType.CheckpointLczA),
                Door.Get(DoorType.CheckpointLczB),
                innerGR18Door,
            };

            foreach (var door in LCZDoors)
            {
                if (door.Rooms.Count == 1 && door.Room.RoomName == MapGeneration.RoomName.LczCheckpointA) doorsDoNotOpen.Add(door);
                if (door.Rooms.Count == 1 && door.Room.RoomName == MapGeneration.RoomName.LczCheckpointB) doorsDoNotOpen.Add(door);
            }

            foreach (var lczdoor in LCZDoors)
            {
                if (!doorsDoNotOpen.Contains(lczdoor))
                    lczdoor.IsOpen = true;
                else
                    lczdoor.IsOpen = false;
            }

            #endregion
            // ----------------------------------------------------------------------------------------------------------------
            // ----------------------------------------------------------------------------------------------------------------
            #region Set up Players

            var roles = Manager.RoleDistribution;

            while (iterator < players.Count)
            {
                var player = players[iterator];

                if (iterator == 0)
                {
                    player.Role.Set(PlayerRoles.RoleTypeId.Scp939);
                    var doorDelta = beastDoor.Position - innerGR18Door.Position;
                    Vector3 box;
                    if (Math.Abs(doorDelta.x) > Math.Abs(doorDelta.z))
                        box = new Vector3(beastDoor.Position.x - doorDelta.x, beastDoor.Position.y + 1, beastDoor.Position.z);
                    else
                        box = new Vector3(beastDoor.Position.x, beastDoor.Position.y + 1, beastDoor.Position.z - doorDelta.z);
                    player.Position = box;
                    beast = player;
                }
                else
                {
                    Manager.ApplyRoleToPlayer(player, roles[iterator-1]);
                    ClassD.Add(player);
                }
                iterator++;
            }

            #endregion
            // ----------------------------------------------------------------------------------------------------------------
            // ----------------------------------------------------------------------------------------------------------------
            #region Lights

            yield return Timing.WaitForSeconds(2);

            // turn off all the lights in each room except for those that have players in them
            {
                var playerRooms = ClassD.Select(player => player.CurrentRoom);
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

            var classDMessage = classDMessageWaiting;
            var beastMessage = beastMessageWaiting;
            void removeTime(int seconds)
            {
                var elapsed = (int)(DateTime.Now - timerStartedTime).TotalSeconds;
                timerTotalSeconds -= seconds;
                var remainingSeconds = timerTotalSeconds - elapsed;
                foreach (var dclass in ClassD)
                {
                    CountdownHelper.AddCountdown(dclass, classDMessage, TimeSpan.FromSeconds(remainingSeconds));
                }
                CountdownHelper.AddCountdown(beast, beastMessage, TimeSpan.FromSeconds(remainingSeconds));
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
            classDMessage = classDMessageActive;
            beastMessage = beastMessageActive;

            beast.ShowHint("Break Free", 5);
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
                CountdownHelper.AddCountdown(classD, "Class-D Win!\nKill The Beast!", TimeSpan.FromSeconds(EndOfRoundTime));
            }

            CountdownHelper.AddCountdown(beast, "End Of Round", TimeSpan.FromSeconds(EndOfRoundTime));
            yield return Timing.WaitForSeconds(EndOfRoundTime);

            beast.Hurt(-1);
            // ----------------------------------------------------------------------------------------------------------------
            // ----------------------------------------------------------------------------------------------------------------
        }

        #endregion

        private static string classDMessageWaiting = "Releasing the Beast in: ❤";
        private static string beastMessageWaiting = "The Class-D are hiding!\nYou will be released in:";
        private static string classDMessageActive = "Do your tasks and Survive!";
        private static string beastMessageActive = "Kill Everyone!";


        private void onPlayerRoleDied(Player ev)
        {
            var playerDeadCount = Player.List.Where(p => p.Role.Type == PlayerRoles.RoleTypeId.Spectator).Count();
            Cassie.Clear();
            Cassie.DelayedMessage($"{playerDeadCount} personnal are dead", 3f);
        }

        private void onPlayerCompleteAllTasks(Player ev)
        {
            Cassie.MessageTranslated("Personnel Has Completed All Tasks", $"{ev.DisplayNickname} has completed all tasks.", isNoisy: false);
        }
    }
}
