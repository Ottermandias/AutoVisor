using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using AutoVisor.Classes;
using Newtonsoft.Json;

namespace AutoVisor
{
    public class PlayerConfig
    {
        public Dictionary< Job, (VisorChangeStates Set, VisorChangeStates State) > PerJob { get; set; } = new()
        {
            { Job.Default, ( 0, 0 ) }
        };

        public bool Enabled { get; set; } = true;

        public PlayerConfig Clone() => ( PlayerConfig )MemberwiseClone();
    }

    [Serializable]
    public class AutoVisorConfiguration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;
        public bool Enabled { get; set; } = true;
        public Dictionary< string, PlayerConfig > States { get; set; } = new();
    }
}
