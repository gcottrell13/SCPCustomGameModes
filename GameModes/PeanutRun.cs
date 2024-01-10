using Exiled.Events.EventArgs.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlayerEvent = Exiled.Events.Handlers.Player;
using ServerEvent = Exiled.Events.Handlers.Server;
using Exiled.API.Features;
using MEC;
using Exiled.Events.EventArgs.Interfaces;
using UnityEngine;
using InventorySystem.Items.Usables.Scp330;
using Exiled.API.Features.Items;
using MapGeneration;
using PlayerRoles;
using Exiled.API.Enums;
using Exiled.API.Features.Roles;
using Exiled.API.Extensions;

namespace CustomGameModes.GameModes
{
    internal class PeanutRun : IGameMode
    {
        public string Name => "Zombies";

        CoroutineHandle roundLoop;

        const RoleTypeId STARTROLE = RoleTypeId.ClassD;
        const RoleTypeId SCPROLE = RoleTypeId.Scp0492;
        static Vector3 SCPSpawnPoint = RoleTypeId.Scp049.GetRandomSpawnLocation().Position;

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
            ServerEvent.RespawningTeam += DeniableEvent;

            roundLoop = Timing.RunCoroutine(_roundLoop());
        }
        public void OnRoundEnd()
        {
            PlayerEvent.TriggeringTesla -= DeniableEvent;
            PlayerEvent.Escaping -= OnEscape;
            PlayerEvent.Dying -= OnDied;
            PlayerEvent.SearchingPickup -= OnSearchingPickup;
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
            var players = Player.List.ToList();
            players.ShuffleList();

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

            while (true)
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

                yield return Timing.WaitForSeconds(1);
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------------------------------------------

        void SetupSCP(Player scp) => SetupSCP(scp, SCPROLE);

        public void SetupSCP(Player scp, RoleTypeId role)
        {
            scp.Role.Set(role, RoleSpawnFlags.None);
            scp.Teleport(SCPSpawnPoint);

            if (Round.EscapedDClasses > 0)
            {
                PrepareSCPAfterEscape(scp);
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

        public void PrepareSCPAfterEscape(Player scp)
        {
            if (scp.Role == RoleTypeId.Scp0492)
            {
                scp.MaxHealth = 100;
                scp.Health = 100;
                scp.MaxArtificialHealth = 0;
            }
            else
            {
                scp.MaxHealth = 1;
                scp.Health = 1;
                scp.MaxArtificialHealth = 0;
            }
        }

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

        public void OnSearchingPickup(SearchingPickupEventArgs ev)
        {
            if (ev.Player.Role == STARTROLE) DeniableEvent(ev);
        }

        public void OnDied(DyingEventArgs ev)
        {
            if (ev.Player.IsHuman)
            {
                if (ev.Attacker?.Role.Team == Team.SCPs)
                {
                    Timing.CallDelayed(0.5f, () =>
                    {
                        SetupSCP(ev.Player, ev.Attacker.Role);
                        ev.Player.Teleport(ev.Attacker.Position);
                    });
                }
            }
        }

        public void OnEscape(EscapingEventArgs e)
        {
            foreach (var scp in Player.List.Where(p => p.Role == SCPROLE))
            {
                PrepareSCPAfterEscape(scp);
                ShowEscapedMessage(scp);
            }
            Timing.CallDelayed(15, () =>
            {
                e.Player.ShowHint("""
                    Go Kill all the Zombies!
                    Beware, you can still become a Zombie!






                    """, 15);
            });
        }
    }
}
