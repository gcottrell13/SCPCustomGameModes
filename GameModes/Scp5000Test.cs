using CustomGameModes.GameModes.Normal;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using PlayerRoles;
using System.Collections.Generic;
using System.Linq;
using ServerEvent = Exiled.Events.Handlers.Server;
using PlayerEvent = Exiled.Events.Handlers.Player;
using PlayerRoles.RoleAssign;
using HarmonyLib;
using System.Security.Cryptography;

namespace CustomGameModes.GameModes
{
    internal class Scp5000Test : IGameMode
    {
        public string Name => "SCP 5000 Test";

        public string PreRoundInstructions => "Press <b><color=blue>~</color></b> and type <b><color=blue>.volunteer scp5000</color></b> to receive SCP 5000 when the game starts";

        public HashSet<Player> Volunteers = new();

        public Player SCP5000Owner;

        public bool Volunteer(Player player)
        {
            if (Volunteers.Contains(player))
            {
                Volunteers.Remove(player);
                return false;
            }
            Volunteers.Add(player);
            return true;
        }

        public void OnRoundEnd()
        {
            PlayerEvent.Hurting -= OnHurting;
        }

        public void OnRoundStart()
        {
            PlayerEvent.Hurting += OnHurting;
            DoSpawnQueue();
            EnsureVolunteersGetChosen();
            GiveSCP5000ToHuman();
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

            ScpSpawner.SpawnScps(Player.List.Count - 1);
            HumanSpawner.SpawnHumans(new[] {Team.FoundationForces}, 1);
        }

        private void EnsureVolunteersGetChosen()
        {
            if (Volunteers.Count > 0)
            {
                var human = Player.Get(Team.FoundationForces).First();
                if (Volunteers.Contains(human)) 
                    return; // we're good

                // all the volunteers are SCPs
                var volunteer = Volunteers.GetRandomValue();

                Log.Debug($"SCP5000Test - Swapping {human.DisplayNickname} and {volunteer.DisplayNickname}");

                var volunteerRole = volunteer.Role.Type;
                var humanRole = human.Role.Type;

                // switch the non-volunteer's role with the volunteer's
                human.ClearInventory();

                human.Role.Set(volunteerRole);
                volunteer.Role.Set(humanRole);
            }
        }

        private void GiveSCP5000ToHuman()
        {
            var human = Player.Get(Team.FoundationForces).First();
            var scp5000 = new SCP5000Handler();
            scp5000.SetupScp5000(human);
            SCP5000Owner = human;
            human.AddAmmo(Exiled.API.Enums.AmmoType.Nato9, 255);
        }

        private void OnHurting(HurtingEventArgs ev)
        {
            if (ev.Attacker == SCP5000Owner && ev.Player.IsScp)
            {
                var scpsMinusZombies = Player.Get(Team.SCPs).Where(s => s.Role != RoleTypeId.Scp0492).ToList();
                ev.DamageHandler.Damage *= 1 + (scpsMinusZombies.Count / 2);
            }
        }
    }
}
