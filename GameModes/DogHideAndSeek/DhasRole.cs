using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Features;
using MEC;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CustomGameModes.GameModes
{
    internal abstract class DhasRole
    {
        public delegate IEnumerator<float> dhasTask();

        public Player player { get; private set; }
        public DhasRoleManager Manager { get; }
        public abstract List<dhasTask> Tasks { get; }
        public abstract RoleTypeId RoleType();

        #region Running State

        public Player AlreadyAcceptedCooperativeTasks;

        private CoroutineHandle _runningCoroutine;

        public Pickup MyTargetPickup { get; private set; }

        public int CurrentTaskNum { get; private set; }
        public dhasTask CurrentTask { get
            {
                try
                {
                    return Tasks[CurrentTaskNum];
                }
                catch (ArgumentOutOfRangeException)
                {
                    DoneAllTasks = true;
                    return null;
                }
            } }

        public bool DoneAllTasks { get; private set; }

        protected bool IsRunning => _runningCoroutine.IsRunning;

        #endregion

        public DhasRole(Player player, DhasRoleManager manager)
        {
            this.player = player;
            Manager = manager;
        }

        /// <summary>
        /// Idempotent Stop
        /// </summary>
        public void Stop()
        {
            if (_runningCoroutine.IsRunning)
            {
                Timing.KillCoroutines(_runningCoroutine);
            }
            if (MyTargetPickup != null && Manager.ClaimedPickups.ContainsKey(MyTargetPickup))
            {
                Manager.ClaimedPickups.Remove(MyTargetPickup);
            }
            OnStop();
        }

        /// <summary>
        /// idempotent stop()
        /// </summary>
        public abstract void OnStop();

        public void Start()
        {
            // idempotent stop()
            Stop();

            // then start
            _runningCoroutine = Timing.RunCoroutine(_coroutine());
        }

        private IEnumerator<float> _coroutine()
        {
            while (CurrentTask != null)
            {
                // start next task
                var nextTask = CurrentTask();
                var d = true;

                while (d)
                {
                    try
                    {
                        d = nextTask.MoveNext();
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                    if (player.Role.Type == RoleTypeId.Spectator)
                    {
                        // dead =(
                        Manager.OnPlayerDied(player);
                        goto Dead;
                    }

                    if (d)
                        yield return nextTask.Current;
                }

                Manager.OnPlayerCompleteOneTask(player);

                ShowTaskCompleteMessage(3);
                yield return Timing.WaitForSeconds(3);

                RemoveTime();

                // increment task
                CurrentTaskNum++;
            }

            Dead:

            if (CurrentTaskNum >= Tasks.Count)
                Manager.OnPlayerCompleteAllTasks(player);
        }


        public void ShowTaskCompleteMessage(float duration)
        {
            var d = taskDifficulty.DifficultyTime();

            player.ShowHint($"{TaskSuccessMessage} (-{d}s)", duration);
        }

        private TaskDifficulty taskDifficulty => CurrentTask.GetMethodInfo().GetCustomAttribute<CrewmateTaskAttribute>().Difficulty;

        private void RemoveTime()
        {
            Log.Debug($"Removed time: {taskDifficulty.DifficultyTime()}s");
            Manager.OnRemoveTime(taskDifficulty.DifficultyTime());
        }

        public void FormatTask(string message, string compass)
        {
            // empty lines push it down from the middle of the screen
            var taskMessage = $"""







                <b>Task {CurrentTaskNum+1}</b>:
                {message}
                {compass}
                """;
            player.ShowHint(taskMessage);
        }

        #region Pickups

        private Pickup _claimNearestPickup(Func<Pickup, bool> predicate)
        {
            var inZone = Pickup.List.Where(p => p.Room?.Zone == player.CurrentRoom?.Zone);
            var validPickups = new List<Pickup>();

            foreach (var pickup in inZone)
            {
                if (Manager.ClaimedPickups.TryGetValue(pickup, out var owner) && owner != player) continue;
                if (!predicate(pickup)) continue;
                validPickups.Add(pickup);
            }

            var closePickup = validPickups.OrderBy(p => (p.Position - player.Position).magnitude).FirstOrDefault();
            if (closePickup != null)
            {
                if (closePickup != MyTargetPickup
                    && MyTargetPickup != null 
                    && Manager.ClaimedPickups.ContainsKey(MyTargetPickup)
                    && Manager.ClaimedPickups[MyTargetPickup] == player
                    )
                {
                     Manager.ClaimedPickups.Remove(MyTargetPickup);
                }

                Manager.ClaimedPickups[closePickup] = player;
                MyTargetPickup = closePickup;
                return closePickup;
            }
            return null;
        }

        /// <summary>
        /// returns false when done
        /// </summary>
        /// <param name="message"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public bool GoGetPickup(Func<Pickup, bool> predicate, Action onFail)
        {
            if (MyTargetPickup != null && !NotHasItem(MyTargetPickup.Type)) 
                return false;

            MyTargetPickup = _claimNearestPickup(predicate);
            if (MyTargetPickup == null)
            {
                onFail();
                return false;
            }

            return NotHasItem(MyTargetPickup.Type);
        }

        #endregion

        #region Cooperative Tasks

        /// <summary>
        /// Will add the given tasks to this role's task list after the current task + offset.
        /// If the tasks should be run immediately after the current task, offset should be zero.
        /// </summary>
        /// <param name="offsetFromCurrent"></param>
        /// <param name="tasks"></param>
        /// <returns></returns>
        public bool TryGiveCooperativeTasks(Player player, uint offsetFromCurrent, params dhasTask[] tasks)
        {
            if (DoneAllTasks) return false;
            if (AlreadyAcceptedCooperativeTasks != null && AlreadyAcceptedCooperativeTasks != player) return false;
            AlreadyAcceptedCooperativeTasks = player;

            var insertAt = Math.Min((int)offsetFromCurrent + CurrentTaskNum + 1, tasks.Length - 1);
            Tasks.InsertRange(insertAt, tasks);

            return true;
        }

        public List<Player> Teammates => Player.List.Where(p => p.Role.Team != Team.SCPs && p != player).ToList();
        public Player Beast => Player.Get(player => player.Role.Type == RoleTypeId.Scp939).FirstOrDefault();

        protected Player GetFarthestTeammate()
        {
            var farthestPlayer = Teammates
                    .OrderByDescending(p => (p.Position - player.Position).magnitude)
                    .FirstOrDefault();
            if (farthestPlayer == null || farthestPlayer == player) return null;
            return farthestPlayer;
        }

        public Player GetNearestTeammate()
        {
            var nearestTeammate = Teammates
                    .OrderBy(p => (p.Position - player.Position).magnitude)
                    .FirstOrDefault();
            if (nearestTeammate == null || nearestTeammate == player) return null;
            return nearestTeammate;
        }

        public float DistanceTo(Player p) => (p.Position - player.Position).magnitude;
        public float DistanceTo(Vector3 vec) => (player.Position - vec).magnitude;

        public bool IsNear(Player p, int distance, out string display)
        {
            if (DistanceTo(p) < distance)
            {
                display = strong($"<color=green>{distance}m</color>");
                return true;
            }
            display = $"{distance}m";
            return false;
        }

        public bool IsNear(Player p, int distance) => IsNear(p, distance, out var _);

        #endregion

        #region Compass

        public string GetCompass(Vector3 to)
        {
            var delta = player.Transform.InverseTransformPoint(to);
            var deltaDir = Math.Atan2(delta.x, delta.z) * 180 / Math.PI;

            var dashesLeft = "--------------------";
            var dashesRight = dashesLeft;
            var straight = "|";
            var target = "▮";
            var radLeft = "   ";
            var radRight = "   ";

            var combined = $"{dashesLeft}{straight}{dashesRight}";

            var compassDegWidth = 60;

            while (deltaDir > 180)
            {
                deltaDir -= 360;
            }
            while (deltaDir < -180)
            {
                deltaDir += 360;
            }

            if (Math.Abs(deltaDir) > compassDegWidth)
            {
                if (deltaDir < 0) radLeft = ((int)-deltaDir).ToString();
                if (deltaDir > 0) radRight = ((int)deltaDir).ToString();
            }
            else
            {
                var i = (int)((deltaDir + compassDegWidth) / (compassDegWidth * 2) * combined.Length);
                if (i >= combined.Length || i < 0) { }
                else
                {
                    combined = combined.Substring(0, i) + target + combined.Substring(i + 1);
                }
            }
            var compass = $"<{radLeft:03} {combined} {radRight:03}>";
            return compass;
        }

        protected string HotAndCold(Vector3 to)
        {
            var distance = (int)DistanceTo(to);
            var size = distance switch
            {
                < 1 => 700,
                < 5 => 400,
                < 10 => 100,
                < 15 => 70,
                < 20 => 50,
                < 30 => 20,
                < 40 => 30,
                < 50 => 40,
                < 60 => 100,
                < 70 => 150,
                < 80 => 200,
                < 100 => 250,
                _ => 1,
            };

            var text = distance switch
            {
                < 30 => "Hot",
                < 60 => "Cold",
                _ => "Freezing",
            };

            var color = distance switch
            {
                < 10 => "red",
                < 15 => "orange",
                < 30 => "yellow",
                < 60 => "cyan",
                _ => "blue",
            };

            return strong($"<color={color}><size={size}>{text}</size></color>");
        }

        public string HotAndColdToBeast() => Beast != null ? HotAndCold(Beast.Position) : "No beast...";


        #endregion

        #region 914

        protected void CanUse914() => Manager.CanUse914(player);
        protected void CannotUse914() => Manager.CannotUse914(player);

        #endregion

        #region Inventory

        protected void CanDropItem(ItemType type)
        {
            Manager.CanDropItem(type, player);
        }

        protected void CannotDropItem(ItemType item) => Manager.CannotDropItem(item, player);


        public bool NotHasItem(ItemType type) => NotHasItem(type, out var _);

        protected bool NotHasItem(ItemType type, out Item item)
        {
            item = player.Items.FirstOrDefault(item => item.Type == type);
            if (item == null) return true;
            return false;
        }

        protected Item EnsureItem(ItemType type)
        {
            var item = player.Items.FirstOrDefault(item => item.Type == type);
            if (item == null)
            {
                return player.AddItem(type);
            }
            return item;
        }

        #endregion

        #region Timing Task


        protected ItemType MyKeycardType;
        [CrewmateTask(TaskDifficulty.Hard)]
        protected IEnumerator<float> UpgradeKeycard()
        {
            CanUse914();
            CanDropItem(MyKeycardType);

            while (NotHasItem(ItemType.KeycardO5, out var item))
            {
                var compass = player.CurrentRoom.Type != RoomType.Lcz914 ?
                    GetCompass(Door.Get(DoorType.Scp914Gate).Position) :
                    "";

                FormatTask("Upgrade Your Keycard", compass);
                yield return Timing.WaitForSeconds(1);
            }

            CannotDropItem(MyKeycardType);
            CannotUse914();
            // Assuming we have the keycard now
            ShowTaskCompleteMessage(3);
            yield return Timing.WaitForSeconds(3);
        }

        #endregion


        protected IEnumerable<float> enumerate(IEnumerator<float> iterator)
        {
            while (iterator.MoveNext())
            {
                yield return iterator.Current;
            }
        }

        public string TaskSuccessMessage => strong("<size=40><color=green>Task Complete!</color></size>");

        public string PlayerNameFmt(Player player)
        {
            var color = player.Role.Type switch
            {
                RoleTypeId.Scientist => "yellow",
                RoleTypeId.ClassD => "orange",
                RoleTypeId.NtfCaptain => "blue",
                _ => "pink",
            };
            return strong($"<color={color}>{player.DisplayNickname}</color>");
        }

        public string strong(string s)
        {
            return $"<b>{s}</b>";
        }
    }
}
