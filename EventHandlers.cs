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
using System.Xml.Linq;
using UnityEngine;
using ServerEvent = Exiled.Events.Handlers.Server;
using PlayerEvent = Exiled.Events.Handlers.Player;
using Exiled.Events.EventArgs.Player;
using Exiled.API.Features.Doors;
using CustomGameModes.GameModes.Normal;

namespace CustomGameModes
{
    internal class EventHandlers
    {
        public static IGameMode CurrentGame;
        public static bool IsLobby => !Round.IsStarted && !Round.IsEnded;

        ~EventHandlers()
        {
            UnregisterEvents();
        }

        public static Dictionary<string, Func<IGameMode>> GameList = new()
        {
            { "dhas", () => new DogHideAndSeek() },
            { "z", () => new PeanutRun() },
            { "n", () => new NormalSCPSL() },
        };

        internal void RegisterEvents()
        {
            PlayerEvent.Spawned += OnSpawned;

            ServerEvent.RoundStarted += OnRoundStarted;
            ServerEvent.RoundEnded += OnRoundEnded;
            ServerEvent.WaitingForPlayers += WaitingForPlayers;
        }

        internal void UnregisterEvents()
        {
            PlayerEvent.Spawned -= OnSpawned;

            ServerEvent.RoundStarted -= OnRoundStarted;
            ServerEvent.RoundEnded -= OnRoundEnded;
            ServerEvent.WaitingForPlayers -= WaitingForPlayers;

            CurrentGame?.OnRoundEnd();
        }

        private void WaitingForPlayers()
        {
            CurrentGame?.OnRoundEnd();
            CurrentGame?.OnWaitingForPlayers();

            GetNextRandomGame();
        }

        private void OnRoundStarted()
        {
            Timing.CallDelayed(
                0.1f,
                () =>
                {
                    foreach (var player in Player.List)
                    {
                        player.ClearBroadcasts();
                        player.Scale = Vector3.one; // just in case you were having some fun in the lobby
                    }
                    CurrentGame?.OnRoundStart();
                }
            );

        }

        private void OnSpawned(SpawnedEventArgs ev)
        {
            if (!IsLobby) return;

            ev.Player.Broadcast(new($"Next game is {CurrentGame?.Name}", 100), true);
        }

        private void OnRoundEnded(RoundEndedEventArgs @event)
        {
            SCP5000Handler.UnsubscribeAll();
            CurrentGame?.OnRoundEnd();
            foreach (var player in Player.List)
            {
                player.Scale = Vector3.one; // just in case you were having some fun in the game
            }
        }

        public void GetNextRandomGame()
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
            SetNextGame(gameConstructor);
        }

        public static void SetNextGame(Func<IGameMode> constructor)
        {
            CurrentGame = constructor();

            foreach (var player in Player.List)
            {
                player.Broadcast(new($"Next game is {CurrentGame.Name}", 100), true);
            }
        }
    }
}
