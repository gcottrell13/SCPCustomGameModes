using CustomGameModes.GameModes.Normal;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using System.Collections.Generic;
using System.Linq;
using ServerEvent = Exiled.Events.Handlers.Server;
using PlayerEvent = Exiled.Events.Handlers.Player;
using PlayerRoles.RoleAssign;
using Exiled.Events.EventArgs.Server;
using MEC;
using Exiled.API.Features.Doors;
using CustomGameModes.API;

namespace CustomGameModes.GameModes
{
    internal class Scp5000Test : IGameMode
    {
        public string Name => "SCP 5000 Test";

        public string PreRoundInstructions => "Press <b><color=blue>~</color></b> and type <b><color=blue>.volunteer scp5000</color></b> to receive SCP 5000+ when the game starts";

        public static HashSet<Player> Volunteers = new();

        public HashSet<Player> DoomSlayers = new();

        public bool Volunteer(Player player)
        {
            if (Volunteers.Contains(player))
            {
                Volunteers.Remove(player);
                Log.Info($"{player.DisplayNickname} has UN-volunteered for SCP 5000+");
                return false;
            }
            Log.Info($"{player.DisplayNickname} has volunteered for SCP 5000+");
            Volunteers.Add(player);
            return true;
        }

        public void OnRoundEnd()
        {
            PlayerEvent.Hurting -= OnHurting;
            PlayerEvent.Shooting -= Shooting;
            ServerEvent.RespawningTeam -= RespawningTeam;
            ServerEvent.SelectingRespawnTeam -= SelectingRespawnTeam;
        }

        public void OnRoundStart()
        {
            PlayerEvent.Hurting += OnHurting;
            PlayerEvent.Shooting += Shooting;

            ServerEvent.RespawningTeam += RespawningTeam;
            ServerEvent.SelectingRespawnTeam += SelectingRespawnTeam;
            DoSpawnQueue();
            GiveSCP5000ToHuman();

            foreach (Door door in Door.List)
            {
                if (door.Rooms.Count > 1)
                {
                    door.IsOpen = true;
                }
            }
        }

        public void OnWaitingForPlayers()
        {
        }


        private void DoSpawnQueue()
        {
            foreach (var player in Player.List)
            {
                player.ClearInventory();
                player.Role.Set(RoleTypeId.Spectator);
            }

            Volunteers = Volunteers.Where(p => p.IsConnected).ToHashSet();

            var volunteerPool = ItemPool.ToPool(Volunteers);
            var count = (Player.List.Count / 10) + 1;

            for (int i = 0; i <= Player.List.Count / 10; i++)
            {
                if (volunteerPool.Count > 0)
                {
                    var volunteer = volunteerPool.GetNext(p => !DoomSlayers.Contains(p));
                    volunteer.Role.Set(RoleTypeId.NtfCaptain);
                    DoomSlayers.Add(volunteer);
                    volunteerPool.Remove(volunteer);
                }
                else
                {
                    HumanSpawner.SpawnHumans(new[] { Team.FoundationForces }, 1);
                }
            }

            ScpSpawner.SpawnScps(Player.List.Count - count);

            foreach (var human in Player.Get(Team.FoundationForces))
            {
                human.AddItem(ItemType.KeycardO5);
                human.EnableEffect(Exiled.API.Enums.EffectType.MovementBoost, 25, 9999f);
            }
        }

        private void GiveSCP5000ToHuman()
        {
            foreach (var human in Player.Get(Team.FoundationForces))
            {
                var scp5000 = new SCP5000Handler();
                scp5000.SetupScp5000(human);

                new SCP1392Handler().SetupPlayer(human);

                DoomSlayers.Add(human);
            }
        }

        private void OnHurting(HurtingEventArgs ev)
        {
            if (!DoomSlayers.Contains(ev.Attacker)) return;

            if (ev.Player.IsScp)
            {
                var scpsMinusZombies = Player.Get(Team.SCPs).Where(s => s.Role != RoleTypeId.Scp0492).ToList();
                ev.DamageHandler.Damage *= 1 + scpsMinusZombies.Count;
            }
        }

        private void Shooting(ShootingEventArgs ev)
        {
            if (!DoomSlayers.Contains(ev.Player)) return;

            ev.Firearm.Ammo = ev.Firearm.MaxAmmo;
        }

        private IEnumerator<float> RespawningTeam(RespawningTeamEventArgs ev)
        {
            yield return Timing.WaitForSeconds(3);
            foreach (Player player in ev.Players)
            {
                new SCP5000Handler().SetupScp5000(player);
            }
        }

        private void SelectingRespawnTeam(SelectingRespawnTeamEventArgs ev)
        {
            ev.Team = Respawning.SpawnableTeamType.NineTailedFox;
        }
    }
}
