using CommandSystem;
using CustomGameModes.GameModes;
using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomGameModes.Commands;

[CommandHandler(typeof(ClientCommandHandler))]
internal class TTTBuyItemCommand : ICommand
{
    public const string CommandName = "ttt-buy";
    public string Command => CommandName;

    public string[] Aliases => Array.Empty<string>();

    public string Description => "Buy an item in TTT";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (EventHandlers.CurrentGame is not TroubleInLC ttt || !Round.IsStarted)
        {
            response = "We are not currently playing this game.";
            return false;
        }

        var player = Player.Get(sender);
        var store = CustomGameModes.Singleton.Config.TttStore;
        var choices = string.Join("\n", store.Select(kvp => $"{kvp.Key}: {kvp.Value} credits"));
        var balance = ttt.Credits.TryGetValue(player, out var b) ? b : 0;

        var didntBuyMessage = $"""
            Your balance: {balance} credits
            Type: <color=blue>.{CommandName} [item name]</color> to buy an item.
            The store:
            {choices}
            """;
        
        if (arguments.Count != 1)
        {
            response = didntBuyMessage;
            return true;
        }

        var enteredString = arguments.ElementAt(0).ToLower();
        ItemType enteredItem = ItemType.None;

        foreach (var storeItem in store.Keys)
        {
            if (storeItem.ToString().ToLower().StartsWith(enteredString))
            {
                enteredItem = storeItem;
                break;
            }
        }

        if (enteredItem == ItemType.None)
        {
            response = $"Invalid item.\n\n{didntBuyMessage}";
            return false;
        }
        if (store.TryGetValue(enteredItem, out var cost) && cost <= balance)
        {
            ttt.Credits[player] -= cost;
            player.AddItem(enteredItem);
            response = $"Bought {enteredItem} for {cost} credits";
            return true;
        }

        response = $"Could not buy item {enteredItem}.\n\n{didntBuyMessage}";
        return false;
    }
}
