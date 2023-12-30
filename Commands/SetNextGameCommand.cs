using CommandSystem;
using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomGameModes.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    internal class SetNextGameCommand : ICommand
    {
        public string Command => "set-next-game";

        public string[] Aliases => new[] { "sng" };

        public string Description => "Sets the next Game mode";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            response = $"""
                    Usage: {Command} <gamemode>
                    Games Include:
                    {string.Join("\n", EventHandlers.GameList.Keys)}
                    """;

            if (arguments.Count != 1)
            {
                return false;
            }

            var name = arguments.ElementAt(0);
            if (!EventHandlers.GameList.TryGetValue(name, out var cons))
            {
                return false;
            }

            EventHandlers.CurrentGame = cons();

            foreach (var player in Player.List)
            {
                player.Broadcast(new($"Next game is {EventHandlers.CurrentGame.Name}", 5), shouldClearPrevious: true);
            }

            response = $"Set current game: {name}";
            return true;
        }
    }
}
