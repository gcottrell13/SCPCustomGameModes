using Exiled.API.Features;
using MEC;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomGameModes.GameModes
{
    internal class SpectatorRole : DhasRole
    {
        public const string name = "spectator";

        public SpectatorRole(Player player, DhasRoleManager manager) : base(player, manager)
        {
            player.Role.Set(RoleType(), RoleSpawnFlags.UseSpawnpoint);
        }

        public override List<dhasTask> Tasks => new()
        {
            Spectate,
        };

        public override void OnStop()
        {
        }

        public override string CountdownBroadcast => "The Class-D are hiding!\nYou will be released in:";
        public override string MainGameBroadcast => "You Died. Enjoy the show!";
        public override string RoundEndBroadcast => "Round End";

        public override void GetPlayerReadyAndEquipped()
        {
        }
        public override void OnCompleteAllTasks() { }

        public override void ShowTaskCompleteMessage(float duration) { }

        public override RoleTypeId RoleType() => RoleTypeId.Spectator;

        [CrewmateTask(TaskDifficulty.None)]
        public IEnumerator<float> Spectate()
        {
            while (player.IsDead)
            {
                var spectating = OtherCrewmates.FirstOrDefault(p => p.CurrentSpectatingPlayers.Contains(player));
                if (spectating == null) goto Loop;

                var theirHint = Manager.PlayerRoles[spectating].CurrentTaskHint;

                var myHint = $"""
                    Spectating: {PlayerNameFmt(spectating)}

                    {theirHint}
                    """;

                player.ShowHint(myHint, 2);

            Loop:
                yield return Timing.WaitForSeconds(1);
            }
        }
    }
}
