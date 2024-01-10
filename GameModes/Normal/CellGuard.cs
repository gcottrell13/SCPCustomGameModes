using Exiled.Events.EventArgs.Interfaces;
using Exiled.Events.EventArgs.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlayerEvent = Exiled.Events.Handlers.Player;
using Exiled.API.Features;
using PlayerRoles;
using MEC;
using Exiled.API.Enums;
using UnityEngine;

namespace CustomGameModes.GameModes.Normal
{
    internal class CellGuard
    {
        public CellGuard()
        {
        }

        ~CellGuard()
        {
            UnsubscribeEventHandlers();
        }

        public void SubscribeEventHandlers()
        {
            Timing.CallDelayed(0.5f, () =>
            {
                var guards = Player.Get(RoleTypeId.FacilityGuard).ToList();
                var numCellGuards = guards.Count switch
                {
                    0 => 0,
                    <= 3 => 1,
                    >= 4 => 2,
                };
                for (var i = 0; i < numCellGuards; i++)
                {
                    var classDDoors = Room.Get(RoomType.LczClassDSpawn).Doors.Where(door => door.Rooms.Count == 2).First();
                    guards[i].Position = Vector3.up + classDDoors.Position - classDDoors.Transform.forward * i * 3;
                }
            });
        }

        public void UnsubscribeEventHandlers()
        {
        }

    }
}
