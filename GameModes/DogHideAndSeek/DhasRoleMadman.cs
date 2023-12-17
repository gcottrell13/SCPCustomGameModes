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
        GetFriend:

            var searchedForFriendTimes = 0;

            Friend = OtherCrewmates.Pool(teammate =>
            {
                var friendrole = Manager.PlayerRoles[teammate];
                if (!friendrole.TryGiveCooperativeTasks(player, 1, GetRandomKeycard))
                    return false;
                friendrole.TryGiveCooperativeTasks(player, 4, FindMadman);
                Log.Debug($"Gave 2 tasks to friend {teammate.DisplayNickname}");
                return true;
            });

            if (Friend == null)
            {
                if (searchedForFriendTimes > 10)
                {
                    player.ShowHint($"You don't have any friends. =( \nA consolation keycard has been added to your inventory.", 10);
                    var item = EnsureItem(ItemType.KeycardMTFPrivate);
                    yield return Timing.WaitForSeconds(10);
                    goto End;
                }

                player.ShowHint("Searching for a friend...");
                yield return Timing.WaitForSeconds(1);
                searchedForFriendTimes++;
                goto GetFriend;
            }

            FriendRole = Manager.PlayerRoles[Friend];

            while (keycardFriendPickup == null)
            {
                if (Friend.IsDead) goto FriendDead;

                var compass = HotAndCold(Friend.Position);
                FormatTask($"Go get a keycard from {PlayerNameFmt(Friend)}.", compass);
                yield return Timing.WaitForSeconds(0.5f);
            }

            while (NotHasItem(keycardFriendPickup.Type, out var item))
            {
                var compass = GetCompass(keycardFriendPickup.Position);
                FormatTask($"Get the keycard that {PlayerNameFmt(Friend)} dropped for you.", compass);
                yield return Timing.WaitForSeconds(0.5f);
            }

            goto End;
        FriendDead: 
            {
                player.ShowHint($"{PlayerNameFmt(Friend)} died. \nA consolation keycard has been added to your inventory.", 10);
                var item = EnsureItem(ItemType.KeycardMTFPrivate);
                yield return Timing.WaitForSeconds(10);
            }
        End: 
            { }
        }


        [CrewmateTask(TaskDifficulty.Easy)]
        private IEnumerator<float> GoToCube()
        {
            var (CUBE, step) = Lcz173Room.MakeCube();
            Step = step;
            Cube = CUBE;

            PlayerEvent.InteractingDoor += InteractDoor;

            while (!doorOpened && DistanceTo(Cube.Position) > 7)
            {
                var compass = GetCompass(CUBE.Position);
                FormatTask("Go to THE CUBE in PT-01", compass);
                yield return Timing.WaitForSeconds(0.5f);
            }

            PlayerEvent.InteractingDoor -= InteractDoor;
        }


        [CrewmateTask(TaskDifficulty.Easy)]
        private IEnumerator<float> StandOnCube()
        {
            var timesPerSecond = 2f;
            var mustRunSeconds = 20f * timesPerSecond;
            var timeElapsed = 0f;

            if (Lcz173Room.gate != null) Lcz173Room.gate.IsOpen = true;

            void hint()
            {
                var d = (int)((mustRunSeconds - timeElapsed) / timesPerSecond);
                if (timeElapsed == 0f)
                    FormatTask($"Stand on CUBE for\n{d} seconds", "");
                else
                    FormatTask($"Stand on CUBE for an additional\n{d} seconds", "");
            }

            float heightThreshold = 13.4f;

            bool onCube()
            {
                var in173 = player.CurrentRoom.RoomName == MapGeneration.RoomName.Lcz173;
                var aboveCube = player.Position.y >= heightThreshold;
                return in173 && aboveCube;
            }

            Cube.Position = new(Cube.Position.x, 12f, Cube.Position.z);

            while (timeElapsed < mustRunSeconds)
            {
                if (onCube())
                {
                    timeElapsed += 1;
                    Cube.Rotation = Quaternion.AngleAxis(timeElapsed / timesPerSecond * 90, Vector3.up);
                }

                if (player.Position.y < heightThreshold)
                    Step.Position = new(Step.Position.x, 11.75f, Step.Position.z);
                else
                    Step.Position = new(Step.Position.x, 0f, Step.Position.z);

                if (Beast != null && IsNear(Beast, 10))
                {
                    closeEncounterNearCube = true;
                }
                hint();

                yield return Timing.WaitForSeconds(1 / timesPerSecond);
            }
        }

        [CrewmateTask(TaskDifficulty.Medium)]
        private IEnumerator<float> GetMauled()
        {
            var thingToDieFor = ThingsToDieFor.RandomChoice();

            PlayerEvent.Dying += killed;

            if (closeEncounterNearCube)
            {
                player.ShowHint("That was a close one, right?", 7);
                yield return Timing.WaitForSeconds(7);

                if (!IsNear(Beast, 20))
                {
                    player.ShowHint("Well, absence does make the heart grow fonder.", 7);
                    yield return Timing.WaitForSeconds(7);
                }
            }
            else
            {
                player.ShowHint("You know what I was thinking?\nThat Beast looks pretty friendly!", 7);
                yield return Timing.WaitForSeconds(7);
            }

            while (!worthIt)
            {
                FormatTask($"{strong(thingToDieFor)}\n(get killed by the beast)", HotAndColdToBeast());
                yield return Timing.WaitForSeconds(1);
            }

            PlayerEvent.Dying -= killed;
            DoneAllTasks = true;

            ShowTaskCompleteMessage(30);
            yield return Timing.WaitForSeconds(30);
        }

        #region Event Handlers

        void killed(DyingEventArgs ev)
        {
            if (ev.Attacker?.Role.Team == Team.SCPs)
            {
                worthIt = true;
            }
        }

        void InteractDoor(InteractingDoorEventArgs ev)
        {
            if (ev.Door == Lcz173Room.gate
                && ev.Player == player
                && ev.Player.CurrentItem?.Type == keycardFriendPickup.Type)
            {
                doorOpened = true;
                ev.Door.IsOpen = true;
            }
        }

        Pickup keycardFriendPickup;
        void OnDroppedItem(DroppedItemEventArgs ev)
        {
            if (keycardFriendPickup == null && ev.Player == Friend && ev.Pickup.Type == friendPickup.Type)
            {
                keycardFriendPickup = ev.Pickup;
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

            while (!FriendRole.IsNear(player, 5, out var dm))
            {
                var compass = FriendRole.GetCompass(player.Position);
                FriendRole.FormatTask($"Be Within {dm} of {myName}", compass);
                yield return Timing.WaitForSeconds(0.5f);
            }

            Friend.ShowHint($"Tell {myName}:\nI can tell you're going through a hard time right now.\nIt's OK, I'm here for you.", 15f);
            yield return Timing.WaitForSeconds(15f);

            Manager.CanDropItem(friendPickup.Type, Friend);

            PlayerEvent.DroppedItem += OnDroppedItem;

            while (keycardFriendPickup == null)
            {
                FriendRole.FormatTask($"Drop the {friendPickup.Type} for {myName}", "");
                yield return Timing.WaitForSeconds(1);
            }

            Manager.ClaimedPickups[keycardFriendPickup] = player;

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
