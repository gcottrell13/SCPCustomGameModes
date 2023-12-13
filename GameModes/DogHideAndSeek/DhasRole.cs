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
using System.Numerics;
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

        public bool AlreadyAcceptedCooperativeTasks = false;

        private CoroutineHandle _runningCoroutine;

        protected Pickup MyTargetPickup;

        public int CurrentTaskNum { get; private set; }
        public dhasTask CurrentTask { get
            {
                try
                {
                    return Tasks[CurrentTaskNum];
                }
                catch (IndexOutOfRangeException)
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
                        break;
                    }

                    if (d)
                        yield return nextTask.Current;
                }

                RemoveTime();

                // increment task
                CurrentTaskNum++;
            }

            Manager.OnPlayerCompleteAllTasks(player);
        }


        protected void ShowTaskCompleteMessage(float duration)
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

        protected void FormatTask(string message, string compass)
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
            var inZone = Pickup.List.Where(p => p.Room.Zone == player.CurrentRoom.Zone);
            var validPickups = new List<Pickup>();

            foreach (var pickup in inZone)
            {
                if (Manager.ClaimedPickups.ContainsKey(pickup)) continue;
                if (!predicate(pickup)) continue;
                validPickups.Add(pickup);
            }

            var closePickup = validPickups.OrderBy(p => (p.Position - player.Position).magnitude).FirstOrDefault();
            if (closePickup != null)
            {
                Manager.ClaimedPickups[closePickup] = player;
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
            if (MyTargetPickup == null)
            {
                MyTargetPickup = _claimNearestPickup(predicate);
                if (MyTargetPickup == null)
                {
                    onFail();
                    return false;
                }
            }
            else if (MyTargetPickup.InUse)
            {
                if (MyTargetPickup.PreviousOwner == player) return false;
                MyTargetPickup = null;
            }
            return true;
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
        public bool TryGiveCooperativeTasks(uint offsetFromCurrent, params dhasTask[] tasks)
        {
            if (AlreadyAcceptedCooperativeTasks || DoneAllTasks) return false;
            AlreadyAcceptedCooperativeTasks = true;

            var insertAt = Math.Min((int)offsetFromCurrent + CurrentTaskNum + 1, tasks.Length - 1);
            Tasks.InsertRange(insertAt, tasks);

            return true;
        }

        protected Player ChooseFarthestPlayer()
        {
            var farthestPlayer = Player.List.Where(p => p.Role.Team != Team.SCPs)
                    .OrderByDescending(p => (p.Position - player.Position).magnitude)
                    .FirstOrDefault();
            if (farthestPlayer == null || farthestPlayer == player) return null;
            return farthestPlayer;
        }

        #endregion

        #region Compass

        protected string GetCompass(UnityEngine.Vector3 to)
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
            var compass = $"{radLeft:03} {combined} {radRight:03}";
            return compass;
        }

        protected string HotAndCold(UnityEngine.Vector3 to)
        {
            var distance = (player.Position - to).magnitude;
            return distance switch
            {
                < 10 => "So hot",
                < 20 => "Hot",
                < 30 => "Cold",
                < 40 => "Colder",
                < 50 => "Really Cold",
                < 60 => "Freezing",
                _ => "Arctic Cold",
            };
        }


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

        protected void CannotDropItem(ItemType item) => Manager.CannotDropItem(item);

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

        protected IEnumerable<float> enumerate(IEnumerator<float> iterator)
        {
            while (iterator.MoveNext())
            {
                yield return iterator.Current;
            }
        }

        public string TaskSuccessMessage => "<size=40><color=green>Task Complete!</color></size>";
    }
}
