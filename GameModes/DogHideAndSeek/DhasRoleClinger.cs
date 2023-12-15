using CustomGameModes.API;
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
    internal class DhasRoleClinger : DhasRole
    {
        public const string name = "clinger";

        public override RoleTypeId RoleType() => RoleTypeId.Scientist;

        public override void OnStop()
        {
        }

        public override List<dhasTask> Tasks => new()
        {
            BeNearWhenTaskComplete,
            FindPlayerA,
            GetAKeycard,
            FindPlayerB,
        };

        public DhasRoleClinger(Player player, DhasRoleManager manager) : base(player, manager)
        {
            player.Role.Set(RoleType(), RoleSpawnFlags.UseSpawnpoint);
        }

        [CrewmateTask(TaskDifficulty.Medium)]
        private IEnumerator<float> BeNearWhenTaskComplete()
        {
            var done = false;
            var requiredDistance = 5;
            void OnSomeoneCompleteTask(Player p)
            {
                if ((player.Position - p.Position).magnitude < requiredDistance)
                    done = true;
            }

            Manager.PlayerCompleteOneTask += OnSomeoneCompleteTask;

            while (!done)
            {
                var nearest = GetNearestCrewmate();
                IsNear(nearest, requiredDistance, out var dist);

                FormatTask($"Be Near Someone ({dist}) When\nThey Complete a Task", "");
                yield return Timing.WaitForSeconds(1);
            }


            Manager.PlayerCompleteOneTask -= OnSomeoneCompleteTask;
        }


        Player playerA;
        Player playerB;

        [CrewmateTask(TaskDifficulty.Medium)]
        private IEnumerator<float> FindPlayerA()
        {
            var requiredDistance = 5;
            playerA = OtherCrewmates.Pool(x => x != player && (player.Position - x.Position).magnitude > requiredDistance);
            if (playerA != null)
            {

                while (!IsNear(playerA, requiredDistance, out var dist))
                {
                    var compass = GetCompass(playerA.Position);
                    FormatTask($"Be Within {dist} of {PlayerNameFmt(playerA)}", compass);
                    yield return Timing.WaitForSeconds(0.5f);
                }
            }
        }

        [CrewmateTask(TaskDifficulty.Easy)]
        private IEnumerator<float> GetAKeycard()
        {
            bool predicate(Pickup pickup) => pickup.Type.IsKeycard();
            void onFail() { player.CurrentItem = player.AddItem(ItemType.KeycardScientist); }

            while (GoGetPickup(predicate, onFail) && MyTargetPickup != null)
            {
                var compass = GetCompass(MyTargetPickup.Position);
                FormatTask("Pick up a Keycard", compass);
                yield return Timing.WaitForSeconds(0.5f);
            }
        }

        [CrewmateTask(TaskDifficulty.Medium)]
        private IEnumerator<float> FindPlayerB()
        {
            var requiredDistance = 5;
            playerB = OtherCrewmates.Pool(x => x != player && x != playerA && (player.Position - x.Position).magnitude > requiredDistance);
            if (playerB != null)
            {

                while (!IsNear(playerB, requiredDistance, out var dist))
                {
                    var compass = GetCompass(playerB.Position);
                    FormatTask($"Be Within {dist} of {PlayerNameFmt(playerB)}", compass);
                    yield return Timing.WaitForSeconds(0.5f);
                }
            }
        }


    }
}
