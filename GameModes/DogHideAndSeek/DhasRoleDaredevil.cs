using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
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
    internal class DhasRoleDaredevil : DhasRole
    {
        public const string name = "daredevil";

        private List<Door> ClassDDoorsTouched = new();

        public override RoleTypeId RoleType() => RoleTypeId.ClassD;

        public override List<dhasTask> Tasks => new()
        {
            TouchAllClassDDoors,
            NoFlashlight,
            Run,
            BeNearBeast,
        };

        public DhasRoleDaredevil(Player player, DhasRoleManager manager) : base(player, manager)
        {
            player.Role.Set(RoleType(), RoleSpawnFlags.UseSpawnpoint);
        }

        /// <summary>
        /// idempotent stop()
        /// </summary>
        public override void OnStop()
        {
            if (CurrentTask == TouchAllClassDDoors)
            {
                PlayerEvent.InteractingDoor -= doorTouch;
            }
        }

        private void doorTouch(InteractingDoorEventArgs e)
        {
            if (e.Door.Room.Type == RoomType.LczClassDSpawn 
                && ClassDDoorsTouched.Contains(e.Door) == false
                )
            {
                ClassDDoorsTouched.Add(e.Door);
            }
        }

        [CrewmateTask(TaskDifficulty.Medium)]
        private IEnumerator<float> TouchAllClassDDoors()
        {
            PlayerEvent.InteractingDoor += doorTouch;

            var allClassDDoors = Door.Get(door => door.Room.Type == RoomType.LczClassDSpawn).ToList();
            var diff = allClassDDoors.Count - ClassDDoorsTouched.Count;
            while (diff > 0)
            {
                diff = allClassDDoors.Count - ClassDDoorsTouched.Count;

                if (ClassDDoorsTouched.Count == 0)
                    FormatTask($"Touch all {allClassDDoors.Count} Class-D Doors.", "");
                else
                    FormatTask($"Touch the Remaining {diff} Class-D Doors.", "");
                yield return Timing.WaitForSeconds(0.5f);
            }
        }

        [CrewmateTask(TaskDifficulty.Hard)]
        private IEnumerator<float> NoFlashlight()
        {
            var mustRunSeconds = 30f;
            var timeElapsed = 0f;

            void hint()
            {
                if (timeElapsed == 0f)
                    FormatTask($"Flashlight off for\n{mustRunSeconds} seconds", "");
                else
                    FormatTask($"Flashlight off for an additional\n{mustRunSeconds - timeElapsed} seconds", "");
            }

            bool predicate()
            {
                // if they aren't holding a flashlight (null), or it's turned off (false) then that's what we want
                return (player.CurrentItem as Flashlight)?.IsEmittingLight != true;
            }

            while (timeElapsed < mustRunSeconds)
            {
                if (predicate()) timeElapsed += 0.5f;
                hint();
                yield return Timing.WaitForSeconds(0.5f);
            }
        }

        [CrewmateTask(TaskDifficulty.Easy)]
        private IEnumerator<float> Run()
        {
            var mustRunSeconds = 30f;
            var timeElapsed = 0f;

            void hint()
            {
                if (timeElapsed == 0f)
                    FormatTask($"Run for {mustRunSeconds} seconds", "");
                else
                    FormatTask($"Run for an additional {mustRunSeconds - timeElapsed} seconds", "");
            }
            bool predicate() => player.IsUsingStamina;

            while (timeElapsed < mustRunSeconds)
            {
                if (predicate()) timeElapsed += 0.5f;
                hint();
                yield return Timing.WaitForSeconds(0.5f);
            }
        }

        [CrewmateTask(TaskDifficulty.Hard)]
        private IEnumerator<float> BeNearBeast()
        {
            var mustRunSeconds = 2f;
            var timeElapsed = 0f;

            void hint()
            {
                if (timeElapsed == 0f)
                    FormatTask($"Be near the Beast for\n{mustRunSeconds} seconds", HotAndColdToBeast());
                else
                    FormatTask($"Be near for an additional\n{mustRunSeconds - timeElapsed} seconds", HotAndColdToBeast());
            }

            bool predicate() => (player.Position - Beast.Position).sqrMagnitude < 100;

            while (timeElapsed < mustRunSeconds)
            {
                if (predicate()) timeElapsed += 0.5f;
                hint();
                yield return Timing.WaitForSeconds(0.5f);
            }
        }

    }
}
