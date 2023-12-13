﻿using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomGameModes.GameModes
{
    internal class DhasRoleManager
    {

        public Dictionary<Pickup, Player> ClaimedPickups = new();
        public Dictionary<ItemType, Player> ItemsToDrop = new();
        public Dictionary<Door, List<Player>> DoorsToOpen = new();
        public Dictionary<Player, DhasRole> PlayerRoles = new();
        public List<Player> AllowedToUse914 = new();

        public List<Func<Player, DhasRole>> RoleList { get; private set; }

        public List<DhasRole> ActiveRoles = new();

        public delegate void RemovingTimeHandlerEvent(int seconds);
        public event RemovingTimeHandlerEvent RemovingTime;

        public delegate void PlayerDiedHandlerEvent(Player player);
        public event PlayerDiedHandlerEvent PlayerDied;

        public delegate void PlayerCompleteAllTasksEvent(Player player);
        public event PlayerCompleteAllTasksEvent PlayerCompleteAllTasks;

        private int roleIndex = 0;

        public DhasRoleManager()
        {
            RoleList = new()
            {
                (player) => new DhasRoleGuardian(player, this),
                (player) => new DhasRoleGuardian(player, this),
            };
        }

        public DhasRole ApplyRoleToPlayer(Player player)
        {
            if (PlayerRoles.TryGetValue(player, out var existingRole))
            {
                existingRole.Stop();
            }

            var role = RoleList[roleIndex++](player);
            ActiveRoles.Add(role);
            PlayerRoles[player] = role;
            return role;
        }

        public void StopAll()
        {
            foreach(var role in ActiveRoles)
            {
                role.Stop();
            }
        }

        public void StartAll()
        {
            foreach (var role in ActiveRoles)
            {
                role.Start();
            }
        }

        public void OnRemoveTime(int seconds) => RemovingTime?.Invoke(seconds);

        public void OnPlayerDied(Player player) => PlayerDied?.Invoke(player);

        public void OnPlayerCompleteAllTasks(Player player) => PlayerCompleteAllTasks?.Invoke(player);

        #region 914

        public void CanUse914(Player player)
        {
            AllowedToUse914.Add(player);
        }
        public void CannotUse914(Player player)
        {
            AllowedToUse914.Remove(player);
        }

        #endregion

        #region Inventory

        public void CanDropItem(ItemType item, Player player)
        {
            ItemsToDrop[item] = player;
        }

        public void CannotDropItem(ItemType item)
        {
            ItemsToDrop.Remove(item);
        }

        #endregion

        #region Doors
           
        public void PlayerCanUseDoor(Door door, Player player)
        {
            if (DoorsToOpen.TryGetValue(door, out var list))
            {
                if (!list.Contains(player)) 
                    list.Add(player);
            }
            else
            {
                DoorsToOpen[door] = new() { player };
            }
        }

        public void PlayerCannotUseDoor(Door door, Player player)
        {
            if (DoorsToOpen.TryGetValue(door, out var list))
            {
                list.Remove(player);
            }
        }

        public void PlayersCanUseDoor(Door door, ICollection<Player> players)
        {
            foreach (var player in players) PlayerCanUseDoor(door, player);
        }

        public void PlayersCannotUseDoor(Door door, ICollection<Player> players)
        {
            foreach (var player in players) PlayerCannotUseDoor(door, player);
        }

        public void PlayersCanUseDoors(ICollection<Door> doors, ICollection<Player> players)
        {
            foreach (var player in players)
                foreach (var door in doors)
                    PlayerCanUseDoor(door, player);
        }

        public void PlayersCannotUseDoors(ICollection<Door> doors, ICollection<Player> players)
        {
            foreach (var player in players)
                foreach (var door in doors)
                    PlayerCannotUseDoor(door, player);
        }

        public void PlayerCanUseDoors(ICollection<Door> doors, Player player)
        {
            foreach (var door in doors) PlayerCanUseDoor(door, player);
        }

        public void PlayerCannotUseDoors(ICollection<Door> doors, Player player)
        {
            foreach (var door in doors) PlayerCannotUseDoor(door, player);
        }

        public void ClearPlayerAllowedDoors(Player player)
        {
            foreach (var door in DoorsToOpen)
            {
                door.Value.Remove(player);
            }
        }

        #endregion
    }
}
