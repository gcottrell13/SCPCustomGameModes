using CustomGameModes.GameModes;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using MEC;
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
        }


        private static IGameMode CurrentGame;

        private static void WaitingForPlayers()
        {
            CurrentGame?.OnWaitingForPlayers();
        }

        private static void OnRoundStarted()
        {
            var pool = CustomGameModes.Singleton.Config.GameModes;

            GetGame:
            var game = pool[UnityEngine.Random.Range(0, pool.Count - 1)];

            var games = new Dictionary<string, IGameMode>()
            {
                { nameof(DogHideAndSeek), new DogHideAndSeek() },
                { "Normal", new NormalSCPSL() },
            };

            if (!games.TryGetValue(game, out CurrentGame))
            {
                if (games.Count == 0)
                {
                    Log.Error("No game modes left in config! Running normal SCP: SL...");
                    return;
                }

                Log.Error($"Could not find game mode: {game}.\nTrying Again");
                var c = pool.RemoveAll(x => x == game);
                Log.Debug($"Removed {c} invalid entries of '{game}'");
                goto GetGame;
            }

            Timing.CallDelayed(
                0.1f,
                () =>
                {
                    CurrentGame.OnRoundStart();
                }
            );

        }

        private static void OnRoundEnded(RoundEndedEventArgs @event)
        {
            CurrentGame?.OnRoundEnd(@event);
        }
    }
}
