using CustomGameModes.GameModes;
using Exiled.API.Interfaces;
using PlayerRoles;
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

        [Description("In Normal, % chance of resurrecting a SCP-049-2 to look like SCP-3114 instead of the normal zombie")]
        public int Scp3114ZombieChance { get; set; } = 25;

        [Description("In Normal, % chance of upgrading player with SCP 5000")]
        public int Scp5000Chance { get; set; } = 5;

        [Description("If set, play this CASSIE message to the recipient of SCP 5000")]
        public string Scp5000CassieIntro { get; set; } = "pitch_1.50 welcome new user to s c p 5 thousand . others cannot see you until you .G7";

        [Description("In DogHideAndSeek, chances of the SCP getting a particular role. Does not have to add up to 100%")]
        public Dictionary<string, float> DhasScpChance { get; set; } = new()
        {
            { nameof(RoleTypeId.Scp939), 100f },
        };


        [Description("In Zombies, chances of the Survivors getting a particular role. Does not have to add up to 100%")]
        public Dictionary<string, float> SurvivorScpChance { get; set; } = new()
        {
            { nameof(RoleTypeId.ClassD), 100f },
        };
        [Description("In Zombies, chances of the Infected getting a particular role. Does not have to add up to 100%")]
        public Dictionary<string, float> InfectedScpChance { get; set; } = new()
        {
            { nameof(RoleTypeId.Scp0492), 100f },
        };

        [Description("In Zombies, chances of the Escapees getting a particular role. Does not have to add up to 100%")]
        public Dictionary<string, float> EscapeeScpChance { get; set; } = new()
        {
            { nameof(RoleTypeId.ChaosConscript), 100f },
        };

        [Description("pregame round instruction size")]
        public int PregameRoundInstructionSize { get; set; } = 15;

        [Description("In Normal, % chance of D-Children")]
        public int DChildrenChance { get; set; } = 10;

        [Description("TTT Max Karma")]
        public int TttMaxKarma { get; set; } = 1500;

        [Description("TTT Credit Store")]
        public Dictionary<ItemType, int> TttStore { get; set; } = new()
        {
            { ItemType.SCP207, 2 },
            { ItemType.GrenadeFlash, 1 },
            { ItemType.Medkit, 1 },
        };

        [Description("TTT Credit for killing Innocent")]
        public int TttKillInnocentReward { get; set; } = 1;

        [Description("TTT Credit to detective for CI dying")]
        public int TttCiDyingReward { get; set; } = 1;

        [Description("TTT Traitor starting credits")]
        public int TttTraitorStartCredits { get; set; } = 1;

        [Description("TTT Detective starting credits")]
        public int TttDetectiveStartCredits { get; set; } = 1;
    }
}
