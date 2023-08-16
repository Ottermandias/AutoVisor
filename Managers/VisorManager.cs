using System;
using System.Collections.Generic;
using AutoVisor.Classes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace AutoVisor.Managers;

public class VisorManager : IDisposable
{
    public static string VisorCommand      = "/visor";
    public static string HideHatCommand    = "/displayhead";
    public static string HideWeaponCommand = "/displayarms";
    public static string OnString          = "on";
    public static string OffString         = "off";


    public static readonly Dictionary<VisorChangeStates, bool> ValidStatesForWeapon = new()
    {
        { VisorChangeStates.Normal, true },
        { VisorChangeStates.Mounted, true },
        { VisorChangeStates.Flying, true },
        { VisorChangeStates.Swimming, true },
        { VisorChangeStates.Diving, true },
        { VisorChangeStates.Fashion, false },
        { VisorChangeStates.Crafting, false },
        { VisorChangeStates.Gathering, false },
        { VisorChangeStates.Fishing, false },
        { VisorChangeStates.Combat, true },
        { VisorChangeStates.Casting, false },
        { VisorChangeStates.Duty, true },
        { VisorChangeStates.Drawn, false },
    };

    public bool IsActive { get; private set; }

    private const    int     NumStateLongs = 12;
    private readonly ulong[] _currentState = new ulong[NumStateLongs];
    private          bool    _currentWeaponDrawn;

    private readonly IntPtr _conditionPtr;
    private          ushort _currentHatModelId;
    private          Job    _currentJob;
    private          string _currentName = string.Empty;
    private          bool   _hatIsShown;
    private          bool   _weaponIsShown;
    private          bool   _visorEnabled;
    private          bool?  _visorToggleEntered;
    private          int    _visorToggleTimer;
    private          int    _waitTimer;

    private bool _visorIsToggled;

    private readonly CommandManager _commandManager;
    public readonly  CPoseManager   CPoseManager;

    public VisorManager(CommandManager commandManager)
    {
        _commandManager = commandManager;
        CPoseManager    = new CPoseManager(_commandManager);
        _conditionPtr   = Dalamud.Conditions.Address;
    }

    public void Dispose()
        => Deactivate();

    public void Activate()
    {
        if (IsActive)
            return;

        IsActive                 =  true;
        Dalamud.Framework.Update += OnFrameworkUpdate;
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive                 =  false;
        Dalamud.Framework.Update -= OnFrameworkUpdate;
    }

    public void ResetState()
    {
        Array.Clear(_currentState, 0, NumStateLongs);
        _currentJob = 0;
    }

    private static readonly ulong[] RelevantConditionsBitmask =
    {
        0x0001010100000100,
        0x0000000000000000,
        0x0000000000000000,
        0x0000000001010000,
        0x0000000000010000,
        0x0000000001000000,
        0x0000000000000000,
        0x0000000000000000,
        0x0000000000000000,
        0x0100000000000000,
        0x0000000001010000,
        0x0001000000000000,
    };

    private static readonly ulong[] WaitStateConditionsBitmask =
    {
        0x0000000000FF00FF,
        0x0000000000000000,
        0x0000000000000000,
        0xFFFF00000000FF00,
        0x00FF0000FF0000FF,
        0x0000FF0000000000,
        0x0000FFFFFF000000,
        0x000000FF00FF0000,
        0x00FF00FFFF0000FF,
        0x00FF000000000000,
        0x00FF000000000000,
        0x0000FFFF00FF0000,
    };

    private unsafe void UpdateWeaponDrawn(PlayerCharacter player)
    {
        var address     = (Character*)player.Address;
        var weaponDrawn = address->IsWeaponDrawn;
        if (weaponDrawn != _currentWeaponDrawn)
        {
            _currentWeaponDrawn      = weaponDrawn;
            CPoseManager.WeaponDrawn = weaponDrawn;
        }

        CPoseManager.Accessory = address->Ornament.OrnamentId != 0;
        var draw = address->GameObject.DrawObject;
        if (draw != null && draw->Object.GetObjectType() is ObjectType.CharacterBase)
        {
            var characterBase = (CharacterBase*)draw;
            CPoseManager.Umbrella = characterBase->HasUmbrella;
            if (CPoseManager.Umbrella)
                CPoseManager.Accessory = false;
        }
    }

    public unsafe void OnFrameworkUpdate(object framework)
    {
        for (var i = 0; i < NumStateLongs; ++i)
        {
            if ((WaitStateConditionsBitmask[i] & *(ulong*)(_conditionPtr + 8 * i).ToPointer()) != 0ul)
                return;
        }

        var player = Player();
        if (player == null)
            return;

        UpdateName(player);
        UpdateJob(player);

        if (_visorToggleEntered != null)
        {
            UpdateFlags(player);
            if (_visorIsToggled == _visorToggleEntered || _visorToggleTimer <= 0)
                _visorToggleEntered = null;
            else
                --_visorToggleTimer;
        }

        if (_waitTimer > 0)
        {
            --_waitTimer;
            return;
        }

        for (var i = 0; i < NumStateLongs; ++i)
        {
            var condition = RelevantConditionsBitmask[i] & *(ulong*)(_conditionPtr + 8 * i).ToPointer();
            if (condition != _currentState[i])
            {
                _currentState[i] = condition;
                for (; i < NumStateLongs; ++i)
                    _currentState[i] = RelevantConditionsBitmask[i] & *(ulong*)(_conditionPtr + 8 * i).ToPointer();
                break;
            }

            if (i == NumStateLongs - 1)
                UpdateWeaponDrawn(player);
        }

        if (!_visorEnabled || !AutoVisor.Config.States.TryGetValue(_currentName, out var config) || !config.Enabled)
            return;

        UpdateActor(player);
        if (!config.PerJob.TryGetValue(_currentJob, out var flags))
            flags = config.PerJob[Job.Default];

        HandleState(flags, Dalamud.Conditions);
    }

    private static readonly (ConditionFlag, VisorChangeStates)[] Conditions = new (ConditionFlag, VisorChangeStates)[]
    {
        (ConditionFlag.Fishing, VisorChangeStates.Fishing),
        (ConditionFlag.Gathering, VisorChangeStates.Gathering),
        (ConditionFlag.Crafting, VisorChangeStates.Crafting),
        (ConditionFlag.InFlight, VisorChangeStates.Flying),
        (ConditionFlag.Diving, VisorChangeStates.Diving),
        (ConditionFlag.UsingParasol, VisorChangeStates.Fashion),
        (ConditionFlag.Mounted, VisorChangeStates.Mounted),
        (ConditionFlag.Swimming, VisorChangeStates.Swimming),
        (ConditionFlag.Casting, VisorChangeStates.Casting),
        (ConditionFlag.InCombat, VisorChangeStates.Combat),
        (ConditionFlag.None, VisorChangeStates.Drawn),
        (ConditionFlag.BoundByDuty, VisorChangeStates.Duty),
        (ConditionFlag.NormalConditions, VisorChangeStates.Normal),
    };

    private void HandleState(VisorChangeGroup visor, Condition condition)
    {
        var hatSet    = visor.HideHatSet == 0;
        var visorSet  = hatSet && _visorEnabled && visor.VisorSet == 0;
        var weaponSet = visor.HideWeaponSet == 0;

        bool ApplyHatChange(VisorChangeStates flag)
        {
            if (!visor.HideHatSet.HasFlag(flag))
                return false;

            ToggleHat(visor.HideHatState.HasFlag(flag), flag);
            return true;
        }

        bool ApplyVisorChange(VisorChangeStates flag)
        {
            if (!visor.VisorSet.HasFlag(flag))
                return false;

            ToggleVisor(visor.VisorState.HasFlag(flag), flag);
            return true;
        }

        bool ApplyWeaponChange(VisorChangeStates flag)
        {
            if (!ValidStatesForWeapon[flag] || !visor.HideWeaponSet.HasFlag(flag))
                return false;

            ToggleWeapon(visor.HideWeaponState.HasFlag(flag), flag);
            return true;
        }

        foreach (var (flag, state) in Conditions)
        {
            if (visorSet && hatSet && weaponSet)
                return;

            var doStuff = state switch
            {
                VisorChangeStates.Drawn => _currentWeaponDrawn,
                _                       => condition[flag],
            };
            if (!doStuff)
                continue;

            hatSet    = hatSet || ApplyHatChange(state);
            visorSet  = visorSet || ApplyVisorChange(state);
            weaponSet = weaponSet || ApplyWeaponChange(state);
        }
    }

    private void ToggleWeapon(bool on, VisorChangeStates flag)
    {
        if (on == _weaponIsShown)
            return;

        PluginLog.Debug("{What} Weapon Slot for {Name} on {Job} due to {Flag}.", on ? "Enabled" : "Disabled", _currentName, _currentJob,
            flag);
        _commandManager.Execute($"{HideWeaponCommand} {(on ? OnString : OffString)}");
        _weaponIsShown = on;
        _waitTimer     = (AutoVisor.Config.WaitFrames + 1) / 2;
    }

    private void ToggleHat(bool on, VisorChangeStates flag)
    {
        if (on == _hatIsShown)
            return;

        if (on)
        {
            PluginLog.Debug("Enabled Hat Slot for {Name} on {Job} due to {Flag}.", _currentName, _currentJob, flag);
            _commandManager.Execute($"{HideHatCommand} {OnString}");
            _hatIsShown = true;
        }
        else
        {
            PluginLog.Debug("Disabled Hat Slot for {Name} on {Job} due to {Flag}.", _currentName, _currentJob, flag);
            _commandManager.Execute($"{HideHatCommand} {OffString}");
            _hatIsShown   = false;
            _visorEnabled = false;
        }

        _waitTimer = (AutoVisor.Config.WaitFrames + 1) / 2;
    }

    private void ToggleVisor(bool on, VisorChangeStates flag)
    {
        if (!_visorEnabled || on == _visorIsToggled || on == _visorToggleEntered)
            return;

        PluginLog.Debug("Toggled Visor for {Name} on {Job} due to {Flag}.", _currentName, _currentJob, flag);
        _commandManager.Execute(VisorCommand);
        _visorIsToggled     = on;
        _visorToggleEntered = on;
        _visorToggleTimer   = AutoVisor.Config.WaitFrames;
        _waitTimer          = (AutoVisor.Config.WaitFrames + 1) / 2;
    }

    private unsafe PlayerCharacter? Player()
    {
        var player = Dalamud.ClientState.LocalPlayer;
        _visorEnabled              = player != null;
        CPoseManager.PlayerPointer = (Character*)(player?.Address ?? IntPtr.Zero);
        return player;
    }

    private void UpdatePoses(PlayerCharacter player)
    {
        if (!AutoVisor.Config.States.TryGetValue(_currentName, out var config))
            return;

        if (!config.PerJob.TryGetValue(_currentJob, out var settings))
            settings = config.PerJob[Job.Default];

        UpdateWeaponDrawn(player);
        foreach (var pose in Enum.GetValues<PoseType>())
            CPoseManager.SetPose(pose, settings.Pose(pose));
    }

    private void UpdateName(PlayerCharacter player)
    {
        var name = player.Name.TextValue;
        if (name == _currentName)
            return;

        ResetState();
        CPoseManager.ResetDefaultPoses();
        _currentName = name;
    }

    private void UpdateActor(PlayerCharacter player)
    {
        _visorEnabled &= UpdateFlags(player);
        _visorEnabled &= UpdateHat(player);
    }

    private void UpdateJob(PlayerCharacter actor)
    {
        var job = (Job)actor.ClassJob.Id;
        if (job == _currentJob)
            return;

        ResetState();
        _currentJob = (Job)actor.ClassJob.Id;
        UpdatePoses(actor);
        _waitTimer = AutoVisor.Config.WaitFrames;
    }

    private unsafe bool UpdateHat(PlayerCharacter actor)
    {
        _currentHatModelId = ((Character*)actor.Address)->DrawData.Head.Id;
        return _currentHatModelId != 0;
    }

    private unsafe bool UpdateFlags(PlayerCharacter actor)
    {
        var address = (Character*)actor.Address;
        _hatIsShown     = !address->DrawData.IsHatHidden;
        _visorIsToggled = address->DrawData.IsVisorToggled;
        _weaponIsShown  = !address->DrawData.IsWeaponHidden;
        return _hatIsShown;
    }
}
