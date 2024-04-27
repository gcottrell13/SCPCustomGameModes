using CommandSystem;
using CustomGameModes.GameModes;
using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomGameModes.Commands;

[CommandHandler(typeof(ClientCommandHandler))]
internal class TTTBuyItemCommand : ICommand
{
    public const string CommandName = "ttt-buy";
    public string Command => CommandName;

    public string[] Aliases => new[] { "ttt" };

    public string Description => "Buy an item in TTT";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (EventHandlers.CurrentGame is not TroubleInLC ttt)
        {
            response = "We are not currently playing this game.";
            return false;
        }

        var player = Player.Get(sender);
        var store = CustomGameModes.Singleton.Config.TttStore;
        var choices = string.Join("\n", store.Select(kvp => $"{kvp.Key}: {kvp.Value} credits"));
        var balance = ttt.GetCredits(player);

        var didntBuyMessage = $"""
            Your balance: {balance} credits
            Use <color=blue>.{CommandName} [partial item name]</color> to buy an item.
            The store:
            {choices}
            """;
        var errmsg = "";
        
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
                goto FoundItem;
            }
        }
        foreach (var storeItem in store.Keys)
        {
            if (storeItem.ToString().ToLower().Contains(enteredString))
            {
                enteredItem = storeItem;
                goto FoundItem;
            }
        }
        errmsg = "Could not find item";
        goto Error;

    FoundItem:

        if (store.TryGetValue(enteredItem, out var cost) && cost > balance)
        {
            errmsg = $"Insufficient funds for {enteredItem}";
            goto Error;
        }

        ttt.AddCredits(player, -cost);
        player.AddItem(enteredItem);
        response = $"Bought {enteredItem} for {cost} credits";
        return true;

    Error:
        response = $"\n<color=red>{errmsg}</color>\n\n{didntBuyMessage}";
        return false;
    }
}
