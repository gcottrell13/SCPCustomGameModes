using CustomGameModes.API;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Toys;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp939.Ripples;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using PlayerEvent = Exiled.Events.Handlers.Player;

namespace CustomGameModes.GameModes
{
    internal class DhasRoleMadman : DhasRole
    {
        public const string name = "madman";

        public override RoleTypeId RoleType() => RoleTypeId.ClassD;

        private Player Friend;
        private DhasRole FriendRole;
        private Primitive Step;
        private Primitive Cube;
        private bool closeEncounterNearCube = false;
        bool worthIt = false;
        bool doorOpened = false;

        public override List<dhasTask> Tasks => new()
        {
            AskForKeycard,
            GoToCube,
            StandOnCube,
            GetMauled,
        };

        public DhasRoleMadman(Player player, DhasRoleManager manager) : base(player, manager)
        {
            player.Role.Set(RoleType(), RoleSpawnFlags.UseSpawnpoint);
        }

        /// <summary>
        /// idempotent stop()
        /// </summary>
        public override void OnStop()
        {
            if (CurrentTask == GoToCube)
                PlayerEvent.InteractingDoor -= InteractDoor;
            if (CurrentTask == GetMauled)
                PlayerEvent.Dying -= killed;
        }

        [CrewmateTask(TaskDifficulty.Easy)]
        private IEnumerator<float> AskForKeycard()
        {
            Teammates.Pool(teammate =>
            {
                Friend = teammate;
                FriendRole = Manager.PlayerRoles[Friend];
                if (!FriendRole.TryGiveCooperativeTasks(player, 1, GetRandomKeycard))
                    return false;
                FriendRole.TryGiveCooperativeTasks(player, 3, FindMadman);
                return true;
            });

            while (friendPickup == null || NotHasItem(friendPickup.Type, out var item))
            {
                var compass = HotAndCold(Friend.Position);
                FormatTask($"Go get a keycard from {PlayerNameFmt(Friend)}.", compass);
                yield return Timing.WaitForSeconds(0.5f);
            }
        }


        [CrewmateTask(TaskDifficulty.Easy)]
        private IEnumerator<float> GoToCube()
        {
            var (CUBE, step) = Lcz173Room.MakeCube();
            Step = step;
            Cube = CUBE;


            PlayerEvent.InteractingDoor += InteractDoor;

            while (!doorOpened)
            {
                var compass = GetCompass(CUBE.Position);
                FormatTask("Go to THE CUBE", compass);
                yield return Timing.WaitForSeconds(0.5f);
            }

            PlayerEvent.InteractingDoor -= InteractDoor;
        }


        [CrewmateTask(TaskDifficulty.Easy)]
        private IEnumerator<float> StandOnCube()
        {
            var mustRunSeconds = 20;
            var timeElapsed = 0;

            void hint()
            {
                if (timeElapsed == 0f)
                    FormatTask($"Stand on CUBE for\n{mustRunSeconds} seconds", "");
                else
                    FormatTask($"Stand on CUBE for an additional\n{mustRunSeconds - timeElapsed} seconds", "");
            }

            bool predicate()
            {
                var in173 = player.CurrentRoom.RoomName == MapGeneration.RoomName.Lcz173;
                var onCube = player.Position.y > 13;
                return in173 && onCube;
            }

            while (timeElapsed < mustRunSeconds)
            {
                if (player.Position.y >= Cube.Position.y + 1)
                {
                    timeElapsed += 1;
                    Cube.Rotation = Quaternion.AngleAxis(timeElapsed * 180, Vector3.up);
                }

                if (player.Position.y < Cube.Position.y + 0.75)
                    Step.Position = new(Step.Position.x, Cube.Position.y, Step.Position.z);
                else
                    Step.Position = new(Step.Position.x, 10f, Step.Position.z);
                if ((Beast.Position - player.Position).magnitude < 10)
                {
                    closeEncounterNearCube = true;
                }
                hint();

                yield return Timing.WaitForSeconds(1);
            }
        }

        [CrewmateTask(TaskDifficulty.Easy)]
        private IEnumerator<float> GetMauled()
        {
            var thingToDieFor = ThingsToDieFor.RandomChoice();

            PlayerEvent.Dying += killed;

            if (closeEncounterNearCube)
            {
                player.ShowHint("That was a close one, right?");
                yield return Timing.WaitForSeconds(7);

                if ((Beast.Position - player.Position).magnitude > 20)
                {
                    player.ShowHint("Well, absence does make the heart grow fonder.");
                    yield return Timing.WaitForSeconds(7);
                }
            }

            while (!worthIt)
            {
                FormatTask($"{strong(thingToDieFor)}\n(get killed by the beast)", HotAndColdToBeast());
                yield return Timing.WaitForSeconds(1);
            }

            PlayerEvent.Dying -= killed;

            ShowTaskCompleteMessage(30);
            yield return Timing.WaitForSeconds(30);
        }

        #region Event Handlers

        void killed(DyingEventArgs ev)
        {
            if (ev.Attacker?.Role.Type == RoleTypeId.Scp939)
            {
                worthIt = true;
            }
        }

        void InteractDoor(InteractingDoorEventArgs ev)
        {
            if (ev.Door.IsGate
                && ev.Door.Room.RoomName == MapGeneration.RoomName.Lcz173
                && ev.Player == player
                && ev.Player.CurrentItem?.Type == friendPickup.Type)
            {
                doorOpened = true;
                ev.Door.IsOpen = true;
            }
        }

        #endregion

        #region Friend Tasks


        Pickup friendPickup;

        [CrewmateTask(TaskDifficulty.Easy)]
        private IEnumerator<float> GetRandomKeycard()
        {
            bool predicate(Pickup pickup) => pickup.Type.IsKeycard();
            void onFail() { Friend.CurrentItem = Friend.AddItem(ItemType.KeycardScientist); }

            while (FriendRole.GoGetPickup(predicate, onFail) && FriendRole.MyTargetPickup != null)
            {
                friendPickup = FriendRole.MyTargetPickup;
                var compass = FriendRole.GetCompass(FriendRole.MyTargetPickup.Position);
                FriendRole.FormatTask("Pick up a Keycard", compass);
                yield return Timing.WaitForSeconds(0.5f);
            }
        }

        [CrewmateTask(TaskDifficulty.Easy)]
        private IEnumerator<float> FindMadman()
        {
            var myName = PlayerNameFmt(player);

            while ((player.Position - Friend.Position).magnitude > 5)
            {
                var compass = FriendRole.GetCompass(player.Position);
                FormatTask($"Be Within 5m of {myName}", compass);
                yield return Timing.WaitForSeconds(0.5f);
            }

            FormatTask($"Tell {myName}:\nI can tell you're going through a hard time right now.\nIt's OK, I'm here for you.", "");
            yield return Timing.WaitForSeconds(15f);

            Manager.CanDropItem(friendPickup.Type, Friend);

            var dropped = false;
            void OnDroppedItem(DroppedItemEventArgs ev)
            {
                if (dropped == false && ev.Player == Friend && ev.Pickup.Type == friendPickup.Type)
                {
                    dropped = true;
                }
            }

            PlayerEvent.DroppedItem += OnDroppedItem;

            while (!dropped)
            {
                FormatTask($"Drop the {friendPickup.Type} for {myName}", "");
                yield return Timing.WaitForSeconds(1);
            }

            PlayerEvent.DroppedItem -= OnDroppedItem;

            Manager.CannotDropItem(friendPickup.Type, Friend);
        }

        #endregion

        private List<string> ThingsToDieFor = new()
        {
            "Get the Beast's Autograph",
            "Clap that Red Booty",
            "Make friends with the Beast",
            "Tame the Big Red Doggy",
        };
    }
}
