using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using AutoVisor.Classes;
using AutoVisor.Managers;

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
        public byte              StandingPose;
        public byte              WeaponDrawnPose;
        public byte              SittingPose;
        public byte              GroundSittingPose;
        public byte              DozingPose;

        public static VisorChangeGroup Empty = new()
        {
            VisorSet          = 0,
            VisorState        = 0,
            HideHatSet        = 0,
            HideHatState      = 0,
            HideWeaponSet     = 0,
            HideWeaponState   = 0,
            StandingPose      = CPoseManager.UnchangedPose,
            WeaponDrawnPose   = CPoseManager.UnchangedPose,
            SittingPose       = CPoseManager.UnchangedPose,
            GroundSittingPose = CPoseManager.UnchangedPose,
            DozingPose        = CPoseManager.UnchangedPose,
        };

        public VisorChangeGroup ResetPoses()
        {
            StandingPose      = CPoseManager.UnchangedPose;
            WeaponDrawnPose   = CPoseManager.UnchangedPose;
            SittingPose       = CPoseManager.UnchangedPose;
            GroundSittingPose = CPoseManager.UnchangedPose;
            DozingPose        = CPoseManager.UnchangedPose;
            return this;
        }

        public bool CheckIntegrity()
        {
            var changes = VisorSet > PlayerConfig.Mask;
            VisorSet   &= PlayerConfig.Mask;
            changes    |= (VisorSet & VisorState) != VisorState;
            VisorState &= VisorSet;

            changes      |= HideHatSet > PlayerConfig.Mask;
            HideHatSet   &= PlayerConfig.Mask;
            changes      |= (HideHatSet & HideHatState) != HideHatState;
            HideHatState &= HideHatSet;

            changes      |= (HideWeaponSet & PlayerConfig.WeaponMask) != HideWeaponSet;
            HideHatSet   &= PlayerConfig.WeaponMask;
            changes      |= (HideWeaponSet & HideWeaponState) != HideWeaponState;
            HideHatState &= HideHatSet;

            if (StandingPose != CPoseManager.DefaultPose
             && StandingPose != CPoseManager.UnchangedPose
             && StandingPose >= CPoseManager.NumStandingPoses)
            {
                changes      = true;
                StandingPose = CPoseManager.DefaultPose;
            }

            if (WeaponDrawnPose != CPoseManager.DefaultPose
             && WeaponDrawnPose != CPoseManager.UnchangedPose
             && WeaponDrawnPose >= CPoseManager.NumWeaponDrawnPoses)
            {
                changes      = true;
                WeaponDrawnPose = CPoseManager.DefaultPose;
            }

            if (SittingPose != CPoseManager.DefaultPose
             && SittingPose != CPoseManager.UnchangedPose
             && SittingPose >= CPoseManager.NumSitPoses)
            {
                changes      = true;
                SittingPose = CPoseManager.DefaultPose;
            }

            if (GroundSittingPose != CPoseManager.DefaultPose
             && GroundSittingPose != CPoseManager.UnchangedPose
             && GroundSittingPose >= CPoseManager.NumGroundSitPoses)
            {
                changes      = true;
                GroundSittingPose = CPoseManager.DefaultPose;
            }

            if (DozingPose != CPoseManager.DefaultPose
             && DozingPose != CPoseManager.UnchangedPose
             && DozingPose >= CPoseManager.NumDozePoses)
            {
                changes      = true;
                DozingPose = CPoseManager.DefaultPose;
            }

            return changes;
        }
    }

    public class PlayerConfig
    {
        public const VisorChangeStates Mask = (VisorChangeStates) ((1 << 13) - 1); 

        public const VisorChangeStates WeaponMask = 
            VisorChangeStates.Normal
          | VisorChangeStates.Mounted
          | VisorChangeStates.Flying
          | VisorChangeStates.Swimming
          | VisorChangeStates.Diving
          | VisorChangeStates.Combat
          | VisorChangeStates.Duty;

        public Dictionary<Job, VisorChangeGroup> PerJob { get; internal set; } = new()
        {
            { Job.Default, VisorChangeGroup.Empty },
        };

        public bool Enabled { get; set; } = true;

        public PlayerConfig Clone()
            => (PlayerConfig) MemberwiseClone();
    }

    [Serializable]
    public class AutoVisorConfiguration : IPluginConfiguration
    {
        public int                              Version    { get; set; } = 2;
        public bool                             Enabled    { get; set; } = true;
        public int                              WaitFrames { get; set; } = 30;
        public Dictionary<string, PlayerConfig> States     { get; set; } = new();

        public static AutoVisorConfiguration Load()
        {
            if (Dalamud.PluginInterface.GetPluginConfig() is AutoVisorConfiguration cfg)
                return cfg;

            cfg = new AutoVisorConfiguration();
            cfg.Save();
            return cfg;
        }

        public void Save()
            => Dalamud.PluginInterface.SavePluginConfig(this);
    }
}
