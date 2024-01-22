using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Pickups;
using Exiled.API.Structs;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PlayerEvent = Exiled.Events.Handlers.Player;

namespace CustomGameModes.GameModes
{
    internal class DhasRoleGuardian : DhasRole
    {
        public const string name = "guardian";

        public override RoleTypeId RoleType => RoleTypeId.Scientist;
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
            player.Role.Set(RoleType, RoleSpawnFlags.UseSpawnpoint);

            // since we have a never ending task, we can't accept any cooperative tasks.
            AlreadyAcceptedCooperativeTasks = player;
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
            Room armory = Room.Get(RoomType.LczArmory);
            Door insideDoor = armory.Doors.FirstOrDefault(d => d.Rooms.Count == 1);
            Vector3 insideDirection = insideDoor.Position - armory.Doors.FirstOrDefault(d => d.Rooms.Count == 2).Position;
            insideDirection.Normalize();

            while (player.CurrentRoom != armory || Vector3.Dot(insideDirection, player.Position - insideDoor.Position) < 0 || DistanceTo(insideDoor.Position) < 1)
            {
                FormatTask("Go Inside the Armory", CompassToRoom(armory));
                yield return Timing.WaitForSeconds(1);
            }
        }

        [CrewmateTask(TaskDifficulty.Hard)]
        private IEnumerator<float> ProtectTeammates()
        {
            // bind event listeners
            PlayerEvent.Hurting += Hurting;
            PlayerEvent.Died += OnDied;

            equipGuardian();
            yield return Timing.WaitForSeconds(15f);

            yield return WaitHint("Go Back and Protect your Teammates!", 10f);

            var message = "Protect your Teammates";
            bool didResetToOneLife = false;
            RemainingLives = 5;

            while (IsRunning)
            {
                if (!didResetToOneLife)
                {
                    if (OtherCrewmates.Count == 0)
                    {
                        didResetToOneLife = true;
                        message = "Your Teammates are all Dead!";
                        RemainingLives = 1;
                    }
                }

                var a = new List<string>();
                for (int i = 0; i < RemainingLives; i++) a.Add("❤");

                FormatTask($"""
                    {message}
                    Remaining Lives: {string.Join("", a)}
                    """, "");
                yield return Timing.WaitForSeconds(1);
            }
        }


        #region Event Handlers

        private void Hurting(HurtingEventArgs ev)
        {
            if (ev.Player.Role.Team == Team.SCPs && ev.Attacker == player)
            {
                ev.Player.EnableEffect(EffectType.SinkHole, duration: 2f);
            }
        }

        private FirearmType gun = FirearmType.E11SR;
        private AttachmentIdentifier flashlightAttachment => AttachmentIdentifier.Get(gun, InventorySystem.Items.Firearms.Attachments.AttachmentName.Flashlight);

        private void equipGuardian()
        {
            player.Role.Set(_escapedRole, RoleSpawnFlags.None);
            player.AddAmmo(AmmoType.Nato556, 50);
            player.CurrentItem = EnsureFirearm(gun, flashlightAttachment);
            EnsureItem(ItemType.Flashlight);
            EnsureItem(ItemType.ArmorCombat);
            EnsureItem(ItemType.KeycardO5);
        }

        private void OnDied(DiedEventArgs ev)
        {
            RemainingLives--;

            if (RemainingLives <= 0) return;

            equipGuardian();
            ev.Player.Role.Set(_escapedRole, RoleSpawnFlags.None);
            player.Position = GetFarthestCrewmate()?.Position 
                ?? SpawnLocationType.Inside173Bottom.GetPosition() + UnityEngine.Vector3.up;
        }

        #endregion
    }
}
