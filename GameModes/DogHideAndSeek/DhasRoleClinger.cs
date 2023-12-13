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
            void OnSomeoneCompleteTask(Player p)
            {
                if ((player.Position - p.Position).magnitude < 5)
                    done = true;
            }

            Manager.PlayerCompleteOneTask += OnSomeoneCompleteTask;

            while (!done)
            {
                FormatTask("Be Near Someone (5m) When\nThey Complete a Task", "");
                yield return Timing.WaitForSeconds(1);
            }


            Manager.PlayerCompleteOneTask -= OnSomeoneCompleteTask;
            ShowTaskCompleteMessage(3);
            yield return Timing.WaitForSeconds(3);
        }


        Player playerA;
        Player playerB;

        [CrewmateTask(TaskDifficulty.Medium)]
        private IEnumerator<float> FindPlayerA()
        {
            playerA = Teammates.Pool(x => x != player && (player.Position - x.Position).magnitude > 10);
            if (playerA != null)
            {

                while ((player.Position - playerA.Position).magnitude > 10)
                {
                    var compass = GetCompass(playerA.Position);
                    FormatTask($"Be Within 10m of {PlayerNameFmt(playerA)}", compass);
                    yield return Timing.WaitForSeconds(1);
                }

                ShowTaskCompleteMessage(3);
                yield return Timing.WaitForSeconds(3);
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
                FormatTask("Pick up Your Scientist Keycard", compass);
                yield return Timing.WaitForSeconds(0.5f);
            }

            // Assuming we have the keycard now
            ShowTaskCompleteMessage(3);
            yield return Timing.WaitForSeconds(3);
        }

        [CrewmateTask(TaskDifficulty.Medium)]
        private IEnumerator<float> FindPlayerB()
        {
            playerB = Teammates.Pool(x => x != player && x != playerA && (player.Position - x.Position).magnitude > 10);
            if (playerB != null)
            {

                while ((player.Position - playerB.Position).magnitude > 10)
                {
                    var compass = GetCompass(playerB.Position);
                    FormatTask($"Be Within 10m of {PlayerNameFmt(playerB)}", compass);
                    yield return Timing.WaitForSeconds(1);
                }

                ShowTaskCompleteMessage(3);
                yield return Timing.WaitForSeconds(3);
            }
        }


    }
}
