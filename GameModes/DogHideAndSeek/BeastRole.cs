﻿using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using UnityEngine;
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
    internal class BeastRole : DhasRole
    {
        public const string name = "beast";

        public override RoleTypeId RoleType() => RoleTypeId.Scp939;

        public override List<dhasTask> Tasks => new()
        {
            KillEveryone,
        };

        public override void OnStop()
        {
        }

        public override string CountdownBroadcast => "The Class-D are hiding!\nYou will be released in:";
        public override string SickoModeBroadcast => MainGameBroadcast;
        public override string MainGameBroadcast => "Kill Everyone!";
        public override string RoundEndBroadcast => "You Couldn't Kill Everyone =(";

        public override void GetPlayerReadyAndEquipped()
        {
        }
        public override void OnCompleteAllTasks() { }

        public override void ShowTaskCompleteMessage(float duration) { }

        public BeastRole(Player player, DhasRoleManager manager) : base(player, manager)
        {
            player.Role.Set(RoleType(), RoleSpawnFlags.None);

            var beastDoor = Room.Get(RoomType.LczGlassBox).Doors.First(d => d.Rooms.Count == 1 && d.IsGate);
            var innerGR18Door = Room.Get(RoomType.LczGlassBox).Doors.First(d => d.Rooms.Count == 1 && !d.IsGate);

            var doorDelta = beastDoor.Position - innerGR18Door.Position;
            Vector3 box;
            if (Math.Abs(doorDelta.x) > Math.Abs(doorDelta.z))
                box = new Vector3(beastDoor.Position.x - doorDelta.x, beastDoor.Position.y + 1, beastDoor.Position.z);
            else
                box = new Vector3(beastDoor.Position.x, beastDoor.Position.y + 1, beastDoor.Position.z - doorDelta.z);
            player.Position = box;
        }

        [CrewmateTask(TaskDifficulty.None)]
        public IEnumerator<float> KillEveryone()
        {
        Normal:
            while(!Manager.BeastSickoModeActivate)
            {
                yield return Timing.WaitForSeconds(1);
            }

        SickoMode:
            while (Manager.BeastSickoModeActivate)
            {
                if (Manager.Humans().Count > 0)
                {
                    var c = GetNearestCrewmate();
                    FormatTask(
                        $"Kill {PlayerNameFmt(c)}", 
                        $"<b><color=orange><size=50>{GetCompass(c.Position)}</size></color></b>"
                        );
                }

                yield return Timing.WaitForSeconds(0.5f);
            }
            goto Normal;

        }
    }
}
