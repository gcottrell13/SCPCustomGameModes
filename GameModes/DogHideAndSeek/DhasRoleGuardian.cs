using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using HarmonyLib;
using MEC;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlayerEvent = Exiled.Events.Handlers.Player;

namespace CustomGameModes.GameModes
{
    internal class DhasRoleGuardian : DhasRole
    {
        public const string name = "guardian";

        public override RoleTypeId RoleType() => RoleTypeId.Scientist;
        private RoleTypeId _escapedRole = RoleTypeId.NtfCaptain;

        public int RemainingLives;

        public override List<dhasTask> Tasks => new()
        {
            GetAKeycard,
            UpgradeKeycard,
            EscapeToHcz,
            ProtectTeammates,
        };

        public DhasRoleGuardian(Player player, DhasRoleManager manager) : base(player, manager)
        {
            player.Role.Set(RoleType(), RoleSpawnFlags.UseSpawnpoint);
        }

        /// <summary>
        /// idempotent stop()
        /// </summary>
        public override void OnStop()
        {
            if (CurrentTask == ProtectTeammates)
            {
                // unbind
                PlayerEvent.Hurting -= Hurting;
                PlayerEvent.Died -= OnDied;
            }
        }


        [CrewmateTask(TaskDifficulty.Easy)]
        private IEnumerator<float> GetAKeycard()
        {
            bool predicate(Pickup pickup) => pickup.Type == ItemType.KeycardScientist;
            void onFail() { player.CurrentItem = player.AddItem(ItemType.KeycardScientist); }

            while(GoGetPickup(predicate, onFail) && MyTargetPickup != null) {
                var compass = GetCompass(MyTargetPickup.Position);
                FormatTask("Pick up Your Scientist Keycard", compass);
                yield return Timing.WaitForSeconds(0.5f);
            }
            MyKeycardType = ItemType.KeycardScientist;

            // Assuming we have the keycard now
        }


        [CrewmateTask(TaskDifficulty.Medium)]
        private IEnumerator<float> EscapeToHcz()
        {
            var allowedDoors = new List<Door>()
            {
                Door.Get(DoorType.CheckpointLczA),
                Door.Get(DoorType.CheckpointLczB),
                Door.Get(DoorType.ElevatorLczA),
                Door.Get(DoorType.ElevatorLczB),
            };

            allowedDoors.AddRange(Door.Get(door => door.Rooms.Count == 1 && door.Room.Type switch
            {
                RoomType.LczCheckpointA => true,
                RoomType.LczCheckpointB => true,
                _ => false,
            }));

            Manager.PlayerCanUseDoors(allowedDoors, player);
            while (player.CurrentRoom.Zone != ZoneType.HeavyContainment)
            {
                FormatTask("Escape To Heavy Containment", "");
                yield return Timing.WaitForSeconds(1);
            }

            foreach (var door in player.CurrentRoom.Doors)
            {
                door.IsOpen = false;
            }

            player.Role.Set(_escapedRole, RoleSpawnFlags.None);
            player.AddAmmo(AmmoType.Nato556, 50);
            EnsureItem(ItemType.GunE11SR);
            EnsureItem(ItemType.ArmorCombat);
        }

        [CrewmateTask(TaskDifficulty.Hard)]
        private IEnumerator<float> ProtectTeammates()
        {
            // since this is a never-ending task, we can't accept any cooperative tasks.
            AlreadyAcceptedCooperativeTasks = player;

            // bind event listeners
            PlayerEvent.Hurting += Hurting;
            PlayerEvent.Died += OnDied;

            RemainingLives = 5;

            while (IsRunning)
            {
                var a = new List<string>();
                for (int i = 0; i < RemainingLives; i++) a.Add("❤");

                FormatTask($"""
                    Protect your Teammates
                    Remaining Lives: {string.Join("", a)}
                    """, "");
                yield return Timing.WaitForSeconds(1);
            }
            Manager.ClearPlayerAllowedDoors(player);
        }


        #region Event Handlers

        private void Hurting(HurtingEventArgs ev)
        {
            if (ev.Player.Role.Team == Team.SCPs && ev.Attacker == player)
            {
                ev.Player.EnableEffect(EffectType.SinkHole, duration: 2f);
            }
        }

        private void OnDied(DiedEventArgs ev)
        {
            RemainingLives--;

            if (RemainingLives <= 0) return;

            ev.Player.Role.Set(_escapedRole, RoleSpawnFlags.None);
            player.AddAmmo(AmmoType.Nato556, 50);
            player.CurrentItem = EnsureFirearm(FirearmType.E11SR);
            EnsureItem(ItemType.Flashlight);
            EnsureItem(ItemType.ArmorCombat);
            player.Position = GetFarthestCrewmate()?.Position 
                ?? ev.Attacker?.Position 
                ?? SpawnLocationType.Inside173Bottom.GetPosition() + UnityEngine.Vector3.up;
        }

        #endregion
    }
}
