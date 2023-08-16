using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using AutoVisor.Classes;
using AutoVisor.Managers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AutoVisor;

public struct VisorChangeGroup
{
    public VisorChangeStates VisorSet;
    public VisorChangeStates VisorState;
    public VisorChangeStates HideHatSet;
    public VisorChangeStates HideHatState;
    public VisorChangeStates HideWeaponSet;
    public VisorChangeStates HideWeaponState;
    public byte              StandingPose      = CPoseManager.UnchangedPose;
    public byte              WeaponDrawnPose   = CPoseManager.UnchangedPose;
    public byte              SittingPose       = CPoseManager.UnchangedPose;
    public byte              GroundSittingPose = CPoseManager.UnchangedPose;
    public byte              DozingPose        = CPoseManager.UnchangedPose;
    public byte              UmbrellaPose      = CPoseManager.UnchangedPose;
    public byte              AccessoryPose     = CPoseManager.UnchangedPose;

    public VisorChangeGroup()
    {}

    public byte Pose(PoseType type)
        => type switch
        {
            PoseType.Idle        => StandingPose,
            PoseType.WeaponDrawn => WeaponDrawnPose,
            PoseType.Sit         => SittingPose,
            PoseType.GroundSit   => GroundSittingPose,
            PoseType.Doze        => DozingPose,
            PoseType.Umbrella    => UmbrellaPose,
            PoseType.Accessory   => AccessoryPose,
            _                    => CPoseManager.UnchangedPose,
        };

    public byte SetPose(PoseType type, byte value)
        => type switch
        {
            PoseType.Idle        => StandingPose = value,
            PoseType.WeaponDrawn => WeaponDrawnPose = value,
            PoseType.Sit         => SittingPose = value,
            PoseType.GroundSit   => GroundSittingPose = value,
            PoseType.Doze        => DozingPose = value,
            PoseType.Umbrella    => UmbrellaPose = value,
            PoseType.Accessory   => AccessoryPose = value,
            _                    => value,
        };

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
        UmbrellaPose      = CPoseManager.UnchangedPose,
        AccessoryPose     = CPoseManager.UnchangedPose,
    };

    public VisorChangeGroup ResetPoses()
    {
        StandingPose      = CPoseManager.UnchangedPose;
        WeaponDrawnPose   = CPoseManager.UnchangedPose;
        SittingPose       = CPoseManager.UnchangedPose;
        GroundSittingPose = CPoseManager.UnchangedPose;
        DozingPose        = CPoseManager.UnchangedPose;
        UmbrellaPose      = CPoseManager.UnchangedPose;
        AccessoryPose     = CPoseManager.UnchangedPose;
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
         && StandingPose >= CPoseManager.Num(PoseType.Idle))
        {
            changes      = true;
            StandingPose = CPoseManager.DefaultPose;
        }

        if (WeaponDrawnPose != CPoseManager.DefaultPose
         && WeaponDrawnPose != CPoseManager.UnchangedPose
         && WeaponDrawnPose >= CPoseManager.Num(PoseType.WeaponDrawn))
        {
            changes         = true;
            WeaponDrawnPose = CPoseManager.DefaultPose;
        }

        if (SittingPose != CPoseManager.DefaultPose
         && SittingPose != CPoseManager.UnchangedPose
         && SittingPose >= CPoseManager.Num(PoseType.Sit))
        {
            changes     = true;
            SittingPose = CPoseManager.DefaultPose;
        }

        if (GroundSittingPose != CPoseManager.DefaultPose
         && GroundSittingPose != CPoseManager.UnchangedPose
         && GroundSittingPose >= CPoseManager.Num(PoseType.GroundSit))
        {
            changes           = true;
            GroundSittingPose = CPoseManager.DefaultPose;
        }

        if (DozingPose != CPoseManager.DefaultPose
         && DozingPose != CPoseManager.UnchangedPose
         && DozingPose >= CPoseManager.Num(PoseType.Doze))
        {
            changes    = true;
            DozingPose = CPoseManager.DefaultPose;
        }

        if (UmbrellaPose != CPoseManager.DefaultPose
         && UmbrellaPose != CPoseManager.UnchangedPose
         && UmbrellaPose >= CPoseManager.Num(PoseType.Umbrella))
        {
            changes      = true;
            UmbrellaPose = CPoseManager.DefaultPose;
        }

        if (AccessoryPose != CPoseManager.DefaultPose
         && AccessoryPose != CPoseManager.UnchangedPose
         && AccessoryPose >= CPoseManager.Num(PoseType.Accessory))
        {
            changes       = true;
            AccessoryPose = CPoseManager.DefaultPose;
        }

        return changes;
    }
}

public class PlayerConfig
{
    public const VisorChangeStates Mask = (VisorChangeStates)((1 << 13) - 1);

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
        => (PlayerConfig)MemberwiseClone();
}

[Serializable]
public class AutoVisorConfiguration : IPluginConfiguration
{
    public const int WaitFramesMin = 1;
    public const int WaitFramesMax = 3000;
    private      int _waitFrames   = 30;

    public int  Version { get; set; } = 2;
    public bool Enabled { get; set; } = true;

    public int WaitFrames
    {
        get => _waitFrames;
        set => _waitFrames = Math.Clamp(value, WaitFramesMin, WaitFramesMax);
    }

    public Dictionary<string, PlayerConfig> States { get; set; } = new();

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
