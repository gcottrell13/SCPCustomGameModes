using Exiled.Events.EventArgs.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlayerEvent = Exiled.Events.Handlers.Player;
using MapEvent = Exiled.Events.Handlers.Map;
using Scp914Handler = Exiled.Events.Handlers.Scp914;
using Exiled.API.Features;
using MEC;

namespace CustomGameModes.GameModes
{
    internal class PeanutRun : IGameMode
    {

        public void OnRoundStart()
        {
            PlayerEvent.TriggeringTesla += OnTriggeringTesla;
            PlayerEvent.Escaping += OnEscape;
            PlayerEvent.Dying += OnDied;
            PlayerEvent.SearchingPickup += OnPickup;

            var players = Player.List.ToList();
            players.ShuffleList();

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (i == 0)
                {
                    player.Role.Set(PlayerRoles.RoleTypeId.Scp173, PlayerRoles.RoleSpawnFlags.UseSpawnpoint);
                    SetupSCP173(player);
                }
                else
                {
                    player.Role.Set(PlayerRoles.RoleTypeId.ClassD, PlayerRoles.RoleSpawnFlags.UseSpawnpoint);
                    SetupClassD(player);
                }
            }
        }
        public void OnRoundEnd()
        {
            PlayerEvent.TriggeringTesla -= OnTriggeringTesla;
            PlayerEvent.Escaping -= OnEscape;
            PlayerEvent.Dying -= OnDied;
            PlayerEvent.SearchingPickup -= OnPickup;
        }

        public void OnWaitingForPlayers()
        {
            OnRoundEnd();
        }


        public void SetupSCP173(Player player)
        {
            Timing.CallDelayed(15, () =>
            {
                player.ShowHint("""
                    Tesla Gates are OFF!
                    If Any Class-D escape,
                    <b>You Die Immediately!</b>
                    """, 15);
            });
        }

        public void SetupClassD(Player player)
        {
            player.CurrentItem = player.AddItem(ItemType.KeycardO5);
            player.AddItem(ItemType.SCP207, 4);

            Timing.CallDelayed(15, () =>
            {
                player.ShowHint("""
                    No Picking up Items!
                    Tesla Gates are OFF!
                    <b>Just Run and Escape!</b>
                    """, 15);
            });
        }


        public void OnTriggeringTesla(TriggeringTeslaEventArgs ev)
        {
            ev.IsAllowed = false;
        }

        public void OnDied(DyingEventArgs ev)
        {
            if (ev.Player.Role.Type == PlayerRoles.RoleTypeId.ClassD)
            {
                if (ev.Attacker?.Role.Type == PlayerRoles.RoleTypeId.Scp173)
                {
                    Timing.CallDelayed(0.5f, () =>
                    {
                        ev.Player.Role.Set(PlayerRoles.RoleTypeId.Scp173);
                        ev.Player.Teleport(ev.Attacker.Position);
                        SetupSCP173(ev.Player);
                    });
                }
            }
        }

        public void OnEscape(EscapingEventArgs e)
        {
            foreach (var scp in Player.List.Where(p => p.Role.Type == PlayerRoles.RoleTypeId.Scp173))
            {
                scp.Hurt(-1, Exiled.API.Enums.DamageType.Warhead);
            }
        }

        public void OnPickup(SearchingPickupEventArgs e)
        {
            e.IsAllowed = false;
        }
    }
}
