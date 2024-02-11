using CustomGameModes.API;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Interfaces;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CustomGameModes.GameModes.Normal
{
    internal class SkeletonSpawner
    {
        public void SubscribeEventHandlers()
        {
            List<Player> scientists = Player.Get(x => x.Role == RoleTypeId.Scientist).ToList();

            if (scientists.Count < 2 || UnityEngine.Random.Range(0, 101) < 75) 
                return;
            if (Player.Get(RoleTypeId.Scp3114).Any())
                return;

            Player luckyPerson = scientists.RandomChoice();
            luckyPerson.Role.Set(RoleTypeId.Scp3114);

            foreach (Ragdoll ragdoll in Ragdoll.List)
            {
                if (ragdoll.Role == RoleTypeId.ClassD && ragdoll.Room?.Type == Exiled.API.Enums.RoomType.Lcz173)
                {
                    Pickup keycard = Pickup.CreateAndSpawn(ItemType.KeycardScientist, ragdoll.Position + Vector3.up, default);
                    ragdoll.Destroy();
                }
            }
        }
    }
}
