using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Dalamud.Configuration;
using AutoVisor.Classes;
using Newtonsoft.Json;

namespace AutoVisor
{
    public struct VisorChangeGroup
    {
        public VisorChangeStates VisorSet;
        public VisorChangeStates VisorState;
        public VisorChangeStates HideHatSet;
        public VisorChangeStates HideHatState;
        public VisorChangeStates HideWeaponSet;
        public VisorChangeStates HideWeaponState;

        public static VisorChangeGroup Empty = new()
        {
            VisorSet        = 0,
            VisorState      = 0,
            HideHatSet      = 0,
            HideHatState    = 0,
            HideWeaponSet   = 0,
            HideWeaponState = 0,
        };
    }

    public class PlayerConfig
    {
        public Dictionary< Job, VisorChangeGroup > PerJob { get; internal set; } = new()
        {
            { Job.Default, VisorChangeGroup.Empty }
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
