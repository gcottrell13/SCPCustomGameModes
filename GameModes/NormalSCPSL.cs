using Exiled.Events.EventArgs.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlayerEvent = Exiled.Events.Handlers.Player;
using MapEvent = Exiled.Events.Handlers.Map;
using Scp914Event = Exiled.Events.Handlers.Scp914;
using ServerEvent = Exiled.Events.Handlers.Server;
using Exiled.Events.EventArgs.Player;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Exiled.Events.EventArgs.Scp914;
using Exiled.API.Extensions;
using CustomGameModes.GameModes.Normal;

namespace CustomGameModes.GameModes
{
    internal class NormalSCPSL : IGameMode
    {
        public string Name => "Normal";

        SCP5000Handler SCP5000Handler { get; set; }
        ClassDStarterInventory ClassDStarterInventory { get; set; }

        public NormalSCPSL()
        {
            SCP5000Handler = new SCP5000Handler();
            ClassDStarterInventory = new ClassDStarterInventory();
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
        }
        void UnsubscribeEventHandlers()
        {
            SCP5000Handler.UnsubscribeEventHandlers();
            ClassDStarterInventory.UnsubscribeEventHandlers();
        }
    }
}
