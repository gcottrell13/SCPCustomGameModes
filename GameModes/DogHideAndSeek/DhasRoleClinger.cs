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
            if (CurrentTask == BeNearWhenTaskComplete)
            {
                Manager.PlayerCompleteOneTask -= OnSomeoneCompleteTask;
            }
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



        Player playerA;
        Player playerB;

        [CrewmateTask(TaskDifficulty.Medium)]
        private IEnumerator<float> FindPlayerA()
        {
            var requiredDistance = 5;
            playerA = OtherCrewmates.Pool(x => !IsNear(x, requiredFriendDistance));
            if (playerA != null)
            {
                if (playerA.IsDead) goto Condolences;

                while (!IsNear(playerA, requiredDistance, out var dist))
                {
                    var compass = GetCompass(playerA.Position);
                    FormatTask($"Be Within {dist} of {PlayerNameFmt(playerA)}", compass);
                    yield return Timing.WaitForSeconds(0.5f);
                }
            }

        Condolences:
            if (playerA?.IsDead == true)
            {
                player.ShowHint($"{PlayerNameFmt(playerA)} has died. Condolences.", 5);
                yield return Timing.WaitForSeconds(5);
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
            playerB = OtherCrewmates.Pool(x => x != playerA && !IsNear(x, requiredFriendDistance));
            if (playerB != null)
            {
                if (playerB.IsDead) goto Condolences;

                while (!IsNear(playerB, requiredFriendDistance, out var dist))
                {
                    var compass = GetCompass(playerB.Position);
                    FormatTask($"Be Within {dist} of {PlayerNameFmt(playerB)}", compass);
                    yield return Timing.WaitForSeconds(0.5f);
                }
            }

        Condolences:
            if (playerB?.IsDead == true)
            {
                player.ShowHint($"{PlayerNameFmt(playerB)} has died. Condolences.", 5);
                yield return Timing.WaitForSeconds(5);
            }
        }


    }
}
