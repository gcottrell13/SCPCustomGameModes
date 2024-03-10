using PlayerEvent = Exiled.Events.Handlers.Player;
using CustomGameModes.GameModes.Normal;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Exiled.API.Extensions;
using MEC;

namespace CustomGameModes.GameModes
{
    internal class NormalSCPSL : IGameMode
    {
        public string Name => "Normal";

        public string PreRoundInstructions => "";

        SCP5000Handler SCP5000Handler { get; set; }
        ClassDStarterInventory ClassDStarterInventory { get; set; }
        CellGuard CellGuard { get; set; }

        public NormalSCPSL()
        {
            SCP5000Handler = new SCP5000Handler();
            ClassDStarterInventory = new ClassDStarterInventory();
            CellGuard = new CellGuard();
        }


        ~NormalSCPSL()
        {
            UnsubscribeEventHandlers();
        }

        public void OnRoundEnd()
        {
            UnsubscribeEventHandlers();
        }

        public void OnRoundStart()
        {
            SubscribeEventHandlers();
        }

        public void OnWaitingForPlayers()
        {
            UnsubscribeEventHandlers();
        }

        void SubscribeEventHandlers()
        {
            SCP5000Handler.SubscribeEventHandlers();
            ClassDStarterInventory.SubscribeEventHandlers();
            CellGuard.SubscribeEventHandlers();
            new SkeletonSpawner().SubscribeEventHandlers();
            new DChildren().Setup();

            PlayerEvent.Spawned += OnSpawn;
        }
        void UnsubscribeEventHandlers()
        {
            SCP5000Handler.UnsubscribeEventHandlers();
            ClassDStarterInventory.UnsubscribeEventHandlers();
            CellGuard.UnsubscribeEventHandlers();

            PlayerEvent.Spawned -= OnSpawn;
        }

        void OnSpawn(SpawnedEventArgs ev)
        {
            if (ev.Player.Role == RoleTypeId.Scp0492)
            {
                if (UnityEngine.Random.Range(1, 101) < CustomGameModes.Singleton.Config.Scp3114ZombieChance)
                {
                    Timing.CallDelayed(0.1f, () => ev.Player.ChangeAppearance(RoleTypeId.Scp3114));
                }
            }
        }
    }
}
