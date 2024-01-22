using CustomGameModes.GameModes;
using Exiled.API.Interfaces;
using System.Collections.Generic;
using System.ComponentModel;

namespace CustomGameModes.Configs
{
    internal class Config : IConfig
    {
        [Description("plugin is enabled")]
        public bool IsEnabled { get; set; }

        [Description("debug messages from plugin")]
        public bool Debug { get; set; }

        [Description("game modes")]
        public List<string> GameModes { get; set; } = new()
        {
            "dhas",
            "z",
            "n",
            "n",
        };

        [Description("In Normal, % chance of upgrading player with SCP 5000")]
        public int Scp5000Chance { get; set; } = 5;

        public Dictionary<string, float> DhasScpChance { get; set; } = new()
        {
            { "Scp939", 100f },
        };
    }
}
