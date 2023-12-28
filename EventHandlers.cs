using CustomGameModes.API;
using CustomGameModes.GameModes;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using MEC;
using Mirror;
using PluginAPI.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ServerEvent = Exiled.Events.Handlers.Server;

namespace CustomGameModes
{
    internal class EventHandlers
    {
        public static IGameMode CurrentGame;

        ~EventHandlers()
        {
            UnregisterEvents();
        }

        public static Dictionary<string, Func<IGameMode>> GameList = new()
        {
            { nameof(DogHideAndSeek), () => new DogHideAndSeek() },
            { nameof(PeanutRun), () => new PeanutRun() },
            { "Normal", () =>new NormalSCPSL() },
        };

        internal static void RegisterEvents()
        {
            ServerEvent.RoundStarted += OnRoundStarted;
            ServerEvent.RoundEnded += OnRoundEnded;
            ServerEvent.WaitingForPlayers += WaitingForPlayers;
        }

        internal static void UnregisterEvents()
        {
            ServerEvent.RoundStarted -= OnRoundStarted;
            ServerEvent.RoundEnded -= OnRoundEnded;
            ServerEvent.WaitingForPlayers -= WaitingForPlayers;

            CurrentGame?.OnRoundEnd();
        }

        private static void WaitingForPlayers()
        {
            CurrentGame?.OnRoundEnd();
            CurrentGame?.OnWaitingForPlayers();

            GetNextRandomGame();
        }

        private static void OnRoundStarted()
        {
            Timing.CallDelayed(
                0.1f,
                () =>
                {
                    CurrentGame?.OnRoundStart();
                }
            );

        }

        private static void OnRoundEnded(RoundEndedEventArgs @event)
        {
            CurrentGame?.OnRoundEnd();
        }

        public static void GetNextRandomGame()
        {
            var pool = CustomGameModes.Singleton.Config.GameModes;

        GetGame:
            var game = pool.RandomChoice();

            if (!GameList.TryGetValue(game, out var gameConstructor))
            {
                if (GameList.Count == 0)
                {
                    Log.Error("No game modes left in config! Running normal SCP: SL...");
                    return;
                }

                Log.Error($"Could not find game mode: {game}.\nTrying Again");
                var c = pool.RemoveAll(x => x == game);
                Log.Debug($"Removed {c} invalid entries of '{game}'");
                goto GetGame;
            }

            CurrentGame = gameConstructor();
        }
    }
}
