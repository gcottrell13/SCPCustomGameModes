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

        CoroutineHandle roundHandlerCO;

        int CountdownTime = 65;
        int RoundTime = 20 * 60;
        // int RoundTime = 60;

        int EndOfRoundTime = 20;

        public void OnRoundStart()
        {
            // -------------------------------------------------------------
            // Event Handlers
            // -------------------------------------------------------------
            PlayerEvent.InteractingDoor += OnInteractDoor;
            MapEvent.SpawningTeamVehicle += OnSpawnTeam;
            PlayerEvent.Hurting += OnHurting;
            PlayerEvent.SearchingPickup += DeniableEvent;
            PlayerEvent.DroppingAmmo += DeniableEvent;
            PlayerEvent.DroppingItem += DeniableEvent;

            Scp914Handler.Activating += DeniableEvent;
            Scp914Handler.ChangingKnobSetting += DeniableEvent;
            Scp914Handler.UpgradingPickup += DeniableEvent;
            Scp914Handler.UpgradingInventoryItem += DeniableEvent;
            Scp914Handler.UpgradingPlayer += DeniableEvent;
            // -------------------------------------------------------------
            // -------------------------------------------------------------

            DecontaminationController.Singleton.NetworkDecontaminationOverride = DecontaminationController.DecontaminationStatus.Disabled;

            roundHandlerCO = Timing.RunCoroutine(_roundHandle());
        }

        public void OnRoundEnd(RoundEndedEventArgs ev)
        {
            // -------------------------------------------------------------
            // Event Handlers
            // -------------------------------------------------------------
            PlayerEvent.InteractingDoor -= OnInteractDoor;
            MapEvent.SpawningTeamVehicle -= OnSpawnTeam;
            PlayerEvent.Hurting -= OnHurting;
            PlayerEvent.SearchingPickup -= DeniableEvent;
            PlayerEvent.DroppingAmmo -= DeniableEvent;
            PlayerEvent.DroppingItem -= DeniableEvent;

            Scp914Handler.Activating -= DeniableEvent;
            Scp914Handler.ChangingKnobSetting -= DeniableEvent;
            Scp914Handler.UpgradingPickup -= DeniableEvent;
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

        }

        public void OnWaitingForPlayers()
        {
            DecontaminationController.Singleton.NetworkDecontaminationOverride = DecontaminationController.DecontaminationStatus.None;

        }

        #region Event Handlers

        private void OnSpawnTeam(SpawningTeamVehicleEventArgs ev)
        {
            // should be enough to prevent MTF and Chaos from spawning?
            // effectively keeping everyone as spectator until the end of the round
            ev.IsAllowed = false;
        }

        private void OnInteractDoor(InteractingDoorEventArgs ev)
        {
            // NO DOORS
            ev.IsAllowed = false;
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
            else
            {
                if (ev.Attacker?.Role.Team == PlayerRoles.Team.ClassD)
                {
                    ev.IsAllowed = false;
                }
            }
        }

        public void DeniableEvent(IDeniableEvent ev)
        {
            ev.IsAllowed = false;
        }

        #endregion

        #region GameHandle

        private IEnumerator<float> _roundHandle()
        {

            List<Player> players = Player.List.ToList();
            players.ShuffleList();

            var iterator = 0;

            LCZDoors = Door.Get(door => door.Zone == ZoneType.LightContainment).ToList();

            beastDoor = Room.Get(RoomType.LczGlassBox).Doors.First(d => d.Rooms.Count == 1 && d.IsGate);

            foreach (var lczdoor in LCZDoors)
            {
                if (lczdoor != beastDoor)
                    lczdoor.IsOpen = true;
            }

            while (iterator < players.Count)
            {
                var player = players[iterator];

                if (iterator == 0)
                {
                    player.Role.Set(PlayerRoles.RoleTypeId.Scp939);
                    var cafe = SpawnLocationType.InsideGr18.GetPosition();
                    player.Position = cafe;
                    beast = player;
                }
                else
                {
                    player.Role.Set(PlayerRoles.RoleTypeId.ClassD);
                    var item = player.AddItem(ItemType.Flashlight);
                    player.CurrentItem = item;
                    ClassD.Add(player);
                }
                iterator++;
            }

            Map.TurnOffAllLights(RoundTime + CountdownTime);

            foreach (var dclass in ClassD)
                CountdownHelper.AddCountdown(dclass, "Hide!\nReleasing the Beast in:", TimeSpan.FromSeconds(CountdownTime));
            CountdownHelper.AddCountdown(beast, "The Class-D are hiding!\nYou will be released in:", TimeSpan.FromSeconds(CountdownTime));

            yield return Timing.WaitForSeconds(CountdownTime);

            beastReleased = true;

            Cassie.MessageTranslated("S C P 9 3 9 has escaped containment", "SCP-939 Has Escaped Containment");
            beastDoor.IsOpen = true;

            foreach (var dclass in ClassD)
                CountdownHelper.AddCountdown(dclass, "Survive!", TimeSpan.FromSeconds(RoundTime));
            CountdownHelper.AddCountdown(beast, "Kill Them ALL", TimeSpan.FromSeconds(RoundTime));

            yield return Timing.WaitForSeconds(RoundTime);

            Cassie.MessageTranslated("The Class D Are Successful", "Class-D Win!");

            foreach (var classD in Player.List.Where(x => x.Role.Type == PlayerRoles.RoleTypeId.ClassD))
            {
                classDWin = true;
                var item = classD.AddItem(ItemType.MicroHID);
                classD.CurrentItem = item;
                CountdownHelper.AddCountdown(classD, "Class-D Win!\nKill The Beast!", TimeSpan.FromSeconds(EndOfRoundTime));
            }

            CountdownHelper.AddCountdown(beast, "End Of Round", TimeSpan.FromSeconds(EndOfRoundTime));
            yield return Timing.WaitForSeconds(EndOfRoundTime);

            beast.Hurt(-1);
        }

        #endregion
    }
}
