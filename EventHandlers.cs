using CustomGameModes.API;
using CustomGameModes.GameModes;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using MEC;
using System;
using System.Collections.Generic;
using UnityEngine;
using ServerEvent = Exiled.Events.Handlers.Server;
using PlayerEvent = Exiled.Events.Handlers.Player;
using Exiled.Events.EventArgs.Player;
using CustomGameModes.GameModes.Normal;

namespace CustomGameModes
{
    internal class EventHandlers
    {
        public static IGameMode CurrentGame;
        public static bool IsLobby => !Round.IsStarted && !Round.IsEnded;

        CoroutineHandle DisplayCurrentGame;

        ~EventHandlers()
        {
            UnregisterEvents();
        }

        public static Dictionary<string, Func<IGameMode>> GameList = new()
        {
            { "dhas", () => new DogHideAndSeek() },
            { "z", () => new Infection() },
            { "n", () => new NormalSCPSL() },
            { "t", () => new Tutorial() },
            { "5k", () => new Scp5000Test() },
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
            if (DisplayCurrentGame.IsRunning) Timing.KillCoroutines(DisplayCurrentGame);
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

            if (DisplayCurrentGame.IsRunning) Timing.KillCoroutines(DisplayCurrentGame);

            DisplayCurrentGame = Timing.RunCoroutine(DisplayCurrentGameCo());
        }

        public static void SetNextGame(Func<IGameMode> constructor)
        {
            CurrentGame = constructor();
        }

        public IEnumerator<float> DisplayCurrentGameCo()
        {
            while (true)
            {
                foreach (var player in Player.List)
                {
                    player.Broadcast(new($"""
                        Next game is {CurrentGame.Name}
                        <size={CustomGameModes.Singleton.Config.PregameRoundInstructionSize}>{CurrentGame.PreRoundInstructions}</size>
                        """, 11), true);
                }
                yield return Timing.WaitForSeconds(10f);
            }
        }
    }
}
