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

        static int gamesPlayed = 0;

        Door beastDoor;
        bool beastReleased;
        bool DidTimeRunOut = false;
        List<Door> LCZDoors = new();
        public static DhasRoleManager Manager;
        bool cassieBeastEscaped = false;

        HashSet<Door> DoorsReopenAfterClosing = new();

        CoroutineHandle roundHandlerCO;

        int CountdownTime = 65;
        int RoundTime = 10 * 60;

        bool FinalCountdown = false;
        bool FiveMinuteWarning = false;
        bool ReleaseOneMinuteWarning = false;
        bool ReleaseCountdown = false;

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

            ServerEvent.EndingRound += OnEndingRound;
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

            setupGame();
            roundHandlerCO = Timing.RunCoroutine(_wrapRoundHandle());

            DidTimeRunOut = false;
            FinalCountdown = false;
            FiveMinuteWarning = false;
            ReleaseOneMinuteWarning = false;
            ReleaseCountdown = false;
            cassieBeastEscaped = false;
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

            ServerEvent.EndingRound -= OnEndingRound;
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

            if (Manager != null)
            {
                Manager.PlayerDied -= onPlayerRoleDied;
                Manager.PlayerCompleteAllTasks -= onPlayerCompleteAllTasks;
                Manager.StopAll();
            }

            CountdownHelper.Stop();
        }

        public void OnWaitingForPlayers()
        {
            DecontaminationController.Singleton.NetworkDecontaminationOverride = DecontaminationController.DecontaminationStatus.None;
        }

        #region Event Handlers

        private void OnEndingRound(EndingRoundEventArgs ev)
        {
            if (DidTimeRunOut || Manager?.Beast().Count == 0)
            {
                ev.LeadingTeam = LeadingTeam.FacilityForces;
                ev.IsAllowed = true;
            }
        }

        private void PlayerDamagingWindow(DamagingWindowEventArgs ev)
        {
            if (ev.Window.Room.Type == RoomType.LczGlassBox)
            {
                if (!beastReleased)
                {
                    DeniableEvent(ev);
                }
                else if (!cassieBeastEscaped)
                {
                    cassieBeastEscaped = true;
                    Cassie.MessageTranslated("S C P 9 3 9 has escaped containment", "SCP-939 Has Escaped Containment");
                }
            }
        }

        private void OnSearchingPickup(SearchingPickupEventArgs ev)
        {
            if (Manager == null) { ev.IsAllowed = false; return; }
            if (Manager.ClaimedPickups.TryGetValue(ev.Pickup, out var assignedPlayer) && ev.Player == assignedPlayer)
            {
                // allow it
            }
            else if (ev.Pickup.PreviousOwner == ev.Player)
            {
                // allow it
            }
            else if (ev.Pickup.Type.IsAmmo() || ev.Pickup.Type.IsWeapon())
            {
                // allow it
            }
            else if (Manager.ClaimedPickups.ContainsKey(ev.Pickup) == false)
            {
                // allow it
            }
            else
            {
                DeniableEvent(ev);
            }
        }

        private void OnInteractDoor(InteractingDoorEventArgs ev)
        {
            if (Manager == null) { ev.IsAllowed = false; return; }

            if (ev.Door == beastDoor || ev.Door.Zone != ZoneType.LightContainment)
            {
                DeniableEvent(ev);
            }
            else if (Room.Get(RoomType.LczArmory).Doors.Contains(ev.Door) && ev.Door.IsOpen)
            {
                DeniableEvent(ev);
            }
            else
            {
                if (Manager.BeastSickoModeActivate)
                {
                    // everyone can open all the LCZ doors once time is almost out
                    ev.Door.IsOpen = true;
                }

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
                DeniableEvent(ev);

            if (DidTimeRunOut && ev.Attacker?.Role.Team == Team.SCPs)
                DeniableEvent(ev);
        }

        private void Activate914(ActivatingEventArgs ev)
        {
            if (Manager == null)
                DeniableEvent(ev);
            if (!Manager.AllowedToUse914.Contains(ev.Player))
                DeniableEvent(ev);
        }

        private void UpgradePickup(UpgradingPickupEventArgs ev)
        {
            if (Manager == null) { ev.IsAllowed = false; return; }

            if (ev.Pickup.Type.IsKeycard() && ev.KnobSetting >= Scp914KnobSetting.Fine)
            {
                Pickup.CreateAndSpawn(ev.Pickup.Type switch
                {
                    ItemType.KeycardFacilityManager => ItemType.KeycardO5,
                    _ => ItemType.KeycardFacilityManager,
                }, ev.OutputPosition, ev.Pickup.Rotation, previousOwner: ev.Pickup.PreviousOwner);
                ev.Pickup.Destroy();
                DeniableEvent(ev);
            }
            else if (InventoryItemLoader.AvailableItems.TryGetValue(ev.Pickup.Type, out var value) && value.TryGetComponent<Scp914ItemProcessor>(out var processor))
            {
                var newPickupBase = processor.OnPickupUpgraded(ev.KnobSetting, ev.Pickup.Base, ev.OutputPosition);

                if (newPickupBase == null)
                {
                    ev.Pickup.Position = ev.OutputPosition;
                    DeniableEvent(ev);
                    return;
                }

                var newPickup = Pickup.Get(newPickupBase);
                Manager.ClaimedPickups[newPickup] = ev.Pickup.PreviousOwner;
                if (newPickup != ev.Pickup)
                {
                    Manager.ClaimedPickups.Remove(ev.Pickup);
                    ev.Pickup.Destroy();
                }

                Log.Info($"914 created {newPickup.Type}");
            }

            DeniableEvent(ev);
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

        private void setupGame()
        {
            Log.Debug("DHAS - Starting a new game");
            gamesPlayed++;
            List<Player> players = Player.List.ToList();
            Manager = new DhasRoleManager();

            // ----------------------------------------------------------------------------------------------------------------
            // ----------------------------------------------------------------------------------------------------------------
            #region Set up Doors
            Log.Debug("DHAS - set up doors");
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
                if (door.Room.Type == RoomType.Lcz330 && door.Type == DoorType.Scp330Chamber) doorsDoNotOpen.Add(door);
            }

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
            Log.Debug("DHAS - set up players");

            var roles = Manager.RoleDistribution;

            var iterator = 0;
            while (iterator < players.Count)
            {
                var index = (iterator + gamesPlayed) % players.Count;
                var player = players[index];
                Manager.ApplyRoleToPlayer(player, roles[iterator]);
                iterator++;
            }

            #endregion
            // ----------------------------------------------------------------------------------------------------------------
            // ----------------------------------------------------------------------------------------------------------------
            #region Lights
            Log.Debug("DHAS - set up lights");

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
        }

        private IEnumerator<float> _roundHandle()
        {
            Log.Debug("DHAS - round coroutine started");

            var timerTotalSeconds = CountdownTime;
            var timerStartedTime = DateTime.Now;

            var getBroadcast = (DhasRole p) => p.CountdownBroadcast;

            int elapsedTime() => (int)(DateTime.Now - timerStartedTime).TotalSeconds;
            void setTimer(int seconds)
            {
                foreach (var playerRole in Manager.ActiveRoles)
                    CountdownHelper.AddCountdown(playerRole.player, getBroadcast(playerRole), TimeSpan.FromSeconds(seconds));
            }
            void removeTime(int seconds)
            {
                timerTotalSeconds -= seconds;
                var remainingSeconds = Math.Max(0, timerTotalSeconds - elapsedTime());
                setTimer(remainingSeconds);
            }

            Manager.StartAll();
            removeTime(0);

            Log.Debug("DHAS - starting escape timer");

            Manager.RemovingTime += removeTime;
            Manager.PlayerDied += onPlayerRoleDied;
            Manager.PlayerCompleteAllTasks += onPlayerCompleteAllTasks;
            while (elapsedTime() < timerTotalSeconds)
            {
                var t = timerTotalSeconds - elapsedTime();
                if (!ReleaseOneMinuteWarning && t <= 60)
                {
                    ReleaseOneMinuteWarning = true;
                    CassieCountdownHelper.SayTimeReminder(t, "until s c p 9 3 9 escapes");
                }
                if (!ReleaseCountdown && t <= 10)
                {
                    ReleaseCountdown = true;
                    CassieCountdownHelper.SayCountdown(t, t);
                    setTimer(t);
                }
                yield return Timing.WaitForSeconds(1);
            }

            // ----------------------------------------------------------------------------------------------------------------
            // ----------------------------------------------------------------------------------------------------------------

            Cassie.Clear();

            Log.Debug("DHAS - starting main timer");
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
                    room.TurnOffLights(0);
                    room.Color = Color.red;
                }
                getBroadcast = p => p.SickoModeBroadcast;
                removeTime(0);
            }

            while (elapsedTime() < timerTotalSeconds)
            {
                var t = timerTotalSeconds - elapsedTime();

                if (!FiveMinuteWarning && t <= 300 && t % 30 == 0)
                {
                    FiveMinuteWarning = true;
                    CassieCountdownHelper.SayTimeReminder(t, "left in the game");
                }

                if (Manager.BeastSickoModeActivate == false && t <= 70)
                {
                    CassieCountdownHelper.SayTimeReminder(t, "left in the game");
                    ActivateBeastSickoMode();
                }

                if (!FinalCountdown && t <= 10)
                {
                    FinalCountdown = true;
                    CassieCountdownHelper.SayCountdown(t, t);
                    setTimer(t);
                }

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
                room.TurnOffLights(0);
                room.Color = Color.white;
            }

            Cassie.GlitchyMessage("Game Over", 0.5f, 0f);

            var stillAlive = Manager.Humans().Count;
            if (Player.Get(player => player.Role.Side == Side.Mtf).Count() > 0)
                Round.EscapedScientists = stillAlive;
            else
                Round.EscapedDClasses = stillAlive * 2; // *2 so that it doesn't end in a draw

            DidTimeRunOut = true;

            foreach (var classD in Manager.Humans())
            {
                var item = classD.player.AddItem(ItemType.MicroHID);
                classD.player.CurrentItem = item;
            }

            Manager.StopAll();
        }

        #endregion

        private void onPlayerRoleDied(Player ev)
        {
            if (Manager.PlayerRoles[ev] is BeastRole) { return; }

            var specRole = Manager.ApplyRoleToPlayer(ev, SpectatorRole.name);
            specRole.Start();

            Timing.CallDelayed(UnityEngine.Random.Range(0, 2), () =>
            {
                if (Manager.Humans().Count == 0) return;

                Cassie.Clear();
                var spectators = Manager.Spectators().Count;
                var verb = spectators == 1 ? "is" : "are";
                Cassie.DelayedMessage($"{spectators} personnel {verb} dead", 1f, isNoisy: false);
            });
        }

        private void onPlayerCompleteAllTasks(Player ev)
        {
            Cassie.MessageTranslated("Personnel Has Completed All Tasks", $"{ev.DisplayNickname} has completed all tasks.", isNoisy: false);
        }
    }
}
