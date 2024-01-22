using Exiled.Events.EventArgs.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using PlayerEvent = Exiled.Events.Handlers.Player;
using ServerEvent = Exiled.Events.Handlers.Server;
using Exiled.API.Features;
using MEC;
using Exiled.Events.EventArgs.Interfaces;
using UnityEngine;
using Exiled.API.Features.Items;
using PlayerRoles;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using CustomGameModes.API;
using Exiled.API.Structs;

namespace CustomGameModes.GameModes
{
    internal class PeanutRun : IGameMode
    {
        public string Name => "Zombies";

        CoroutineHandle roundLoop;

        const RoleTypeId STARTROLE = RoleTypeId.ClassD;
        const RoleTypeId SCPROLE = RoleTypeId.Scp0492;
        static Vector3 SCPSpawnPoint = RoleTypeId.Scp939.GetRandomSpawnLocation().Position;

        public PeanutRun()
        {
            // set this so that the SCPs can properly get a win screen, even if they're all zombies
            Round.EscapedScientists = -1;
        }

        ~PeanutRun()
        {
            OnRoundEnd();
        }

        public void OnRoundStart()
        {
            PlayerEvent.TriggeringTesla += DeniableEvent;
            PlayerEvent.Escaping += OnEscape;
            PlayerEvent.Dying += OnDied;
            PlayerEvent.SearchingPickup += OnSearchingPickup;
            PlayerEvent.Hurting += OnHurting;
            PlayerEvent.InteractingDoor += OnDoor;
            ServerEvent.RespawningTeam += DeniableEvent;

            roundLoop = Timing.RunCoroutine(_roundLoop());
        }
        public void OnRoundEnd()
        {
            PlayerEvent.TriggeringTesla -= DeniableEvent;
            PlayerEvent.Escaping -= OnEscape;
            PlayerEvent.Dying -= OnDied;
            PlayerEvent.SearchingPickup -= OnSearchingPickup;
            PlayerEvent.Hurting -= OnHurting;
            PlayerEvent.InteractingDoor -= OnDoor;
            ServerEvent.RespawningTeam -= DeniableEvent;

            if (roundLoop.IsRunning)
                Timing.KillCoroutines(roundLoop);
        }

        public void OnWaitingForPlayers()
        {
            OnRoundEnd();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------------------------------------------

        private IEnumerator<float> _roundLoop()
        {
            var players = Player.List.ToList().ManyRandom();

            for (var i = 0; i < players.Count; i++)
            {
                Action<Player> setup = i switch
                {
                    0 => SetupSCP,
                    1 => SetupClassD,
                    2 => SetupSCP,
                    _ => SetupClassD,
                };

                setup(players[i]);
            }

            foreach (var hczRoom in Room.Get(ZoneType.HeavyContainment))
            {
                hczRoom.TurnOffLights(9999f);
            }
            foreach (var ezRoom in Room.Get(ZoneType.Entrance))
            {
                ezRoom.TurnOffLights(9999f);
            }

            while (Round.InProgress)
            {
                try
                {

                SCPHUD:
                    {
                        var humans = Player.Get(p => p.IsHuman).ToList();
                        var inLcz = humans.Where(p => p.Zone == ZoneType.LightContainment).Count();
                        var inHcz = humans.Where(p => p.Zone == ZoneType.HeavyContainment).Count();
                        var inEz = humans.Where(p => p.Zone == ZoneType.Entrance || p.CurrentRoom.Type == RoomType.HczEzCheckpointA || p.CurrentRoom.Type == RoomType.HczEzCheckpointB).Count();
                        var inSurface = humans.Where(p => p.Zone == ZoneType.Surface).Count();

                        IEnumerable<string> lines()
                        {
                            if (inLcz > 0) yield return $"<color=orange>LCZ: {inLcz}</color>";
                            if (inHcz > 0) yield return $"<color=yellow>HCZ: {inHcz}</color>";
                            if (inEz > 0) yield return $"<color=green>Entrance: {inEz}</color>";
                            if (inSurface > 0) yield return $"<color=blue>Surface: {inSurface}</color>";
                        }

                        var message = string.Join(" | ", lines());

                        foreach (var scp in Player.Get(p => p.Role.Team == Team.SCPs))
                        {
                            scp.Broadcast(2, $"""
                            Where to Find the Class-Ds:
                            {message}
                            """, shouldClearPrevious: true);
                        }
                    }

                ChaosHUD:
                    {
                        var chaosPlayers = Player.Get(p => p.Role.Team == Team.ChaosInsurgency).ToList();

                        if (chaosPlayers.Count > 0)
                        {
                            var scpCount = Player.Get(p => p.IsScp).Count();
                            var cdCount = Player.Get(p => p.Role == STARTROLE).Count();
                            var scpMsg = $"<color=red>SCPs</color>: {scpCount}";
                            var cdMsg = $"<color=orange>Class-D</color>: {cdCount}";
                            foreach (var ci in chaosPlayers)
                            {
                                ci.Broadcast(2, $"{scpMsg} - {cdMsg}", shouldClearPrevious: true);
                            }
                        }
                    }
                }
                catch
                {

                }

                yield return Timing.WaitForSeconds(1);
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------------------------------------------

        void SetupSCP(Player scp) => SetupSCP(scp, SCPROLE);

        public void SetupSCP(Player scp, RoleTypeId role)
        {
            scp.Role.Set(role, RoleSpawnFlags.None);
            scp.Teleport(SCPSpawnPoint + Vector3.up);

            if (Round.EscapedDClasses > 0)
            {
                Timing.CallDelayed(15, () => ShowEscapedMessage(scp));
            }
            else
            {
                Timing.CallDelayed(15, () => ShowSCPStartupMessage(scp));
            }
        }

        public void SetupClassD(Player player)
        {
            player.Role.Set(STARTROLE, RoleSpawnFlags.UseSpawnpoint);
            player.ClearInventory();
            player.CurrentItem = player.AddItem(ItemType.KeycardO5);
            player.AddItem(ItemType.Lantern);
            player.AddItem(ItemType.SCP207, 3);
            player.AddItem(ItemType.SCP500, 1);

            Timing.CallDelayed(15, () => ShowClassDStartupMessage(player));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------------------------------------------

        public void ShowEscapedMessage(Player scp)
        {
            scp.ShowHint("""
                A Class-D has Escaped and returned as Chaos Insurgency!
                Beware!
                """, 20);
        }

        public void ShowClassDStartupMessage(Player classd)
        {
            classd.ShowHint("""
                No Picking up Items!
                Tesla Gates are OFF!
                <b>Just Run and Escape!</b>
                """, 15);
        }

        public void ShowSCPStartupMessage(Player scp)
        {
            scp.ShowHint("""
                Tesla Gates are OFF!
                Kill the <b><color=orange>Class-D</color></b> Before They Escape!
                """, 15);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------------------------------------------

        public void DeniableEvent(IDeniableEvent ev)
        {
            ev.IsAllowed = false;
        }

        public void OnDoor(InteractingDoorEventArgs ev)
        {
            if (ev.Door.IsOpen && !ev.Door.IsElevator && ev.Door.Type != DoorType.Airlock)
            {
                // cannot close doors
                DeniableEvent(ev);
            }
        }

        public void OnHurting(HurtingEventArgs ev)
        {
            if (Round.EscapedDClasses > 0 && ev.Player.IsScp)
            {
                ev.DamageHandler.Damage *= 5;
            }
        }

        public void OnSearchingPickup(SearchingPickupEventArgs ev)
        {
            if (ev.Player.Role == STARTROLE) DeniableEvent(ev);
        }

        public IEnumerator<float> OnDied(DyingEventArgs ev)
        {
            if (ev.Player.IsHuman)
            {
                ev.Player.ClearInventory();

                if (ev.Attacker?.Role.Team == Team.SCPs)
                {
                    yield return Timing.WaitForSeconds(0.5f);
                    SetupSCP(ev.Player, ev.Attacker.Role);
                    ev.Player.Teleport(ev.Attacker.Position);
                }
            }
        }

        public IEnumerator<float> OnEscape(EscapingEventArgs e)
        {
            foreach (var scp in Player.List.Where(p => p.Role == SCPROLE))
            {
                ShowEscapedMessage(scp);
            }

            yield return Timing.WaitForSeconds(2);
            AddFlashlightToCiGun(e.Player);
            yield return Timing.WaitForSeconds(13);
            e.Player.ShowHint("""
                Go Kill all the Zombies!
                Beware, you can still become a Zombie!






                """, 15);
        }

        public void AddFlashlightToCiGun(Player player)
        {
            foreach (Item item in player.Items)
            {
                if (item is not Firearm weapon) continue;

                if (AttachmentIdentifier.Get(weapon.FirearmType, InventorySystem.Items.Firearms.Attachments.AttachmentName.Flashlight) is AttachmentIdentifier att && att.Code != 0)
                    weapon.AddAttachment(att);
            }
        }
    }
}
