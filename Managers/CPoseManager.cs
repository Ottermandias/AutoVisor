using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AutoVisor.SeFunctions;
using Dalamud.Hooking;
using Dalamud.Plugin;

namespace AutoVisor.Managers
{
    public class CPoseManager
    {
        public const int NumStandingPoses    = 7;
        public const int NumWeaponDrawnPoses = 2;
        public const int NumSitPoses         = 3;
        public const int NumGroundSitPoses   = 4;
        public const int NumDozePoses        = 3;

        public static readonly int[] NumPoses =
        {
            NumStandingPoses,
            NumWeaponDrawnPoses,
            NumSitPoses,
            NumGroundSitPoses,
            NumDozePoses,
        };

        public static readonly string[] PoseNames =
        {
            "Standing Pose",
            "Weapon Drawn Pose",
            "Sitting Pose",
            "Sitting on Ground Pose",
            "Dozing Pose",
        };

        private static readonly Dictionary<byte, byte> StandPoses = new()
        {
            { 0, 0 },
            { 1, 91 },
            { 2, 92 },
            { 3, 107 },
            { 4, 108 },
            { 5, 218 },
            { 6, 219 },
        };

        private static readonly Dictionary<byte, byte> WeaponPoses = new()
        {
            { 0, 0 },
            { 1, 93 },
        };

        private static readonly Dictionary<byte, byte> DozePoses = new()
        {
            { 0, 88 },
            { 1, 99 },
            { 2, 100 },
        };

        private static readonly Dictionary<byte, byte> SitPoses = new()
        {
            { 0, 50 },
            { 1, 95 },
            { 2, 96 },
        };

        private static readonly Dictionary<byte, byte> GroundSitPoses = new()
        {
            { 0, 52 },
            { 1, 97 },
            { 2, 98 },
            { 3, 117 },
        };

        private static int StateFromPose(int pose, bool weaponDrawn)
        {
            return pose switch
            {
                0   => weaponDrawn ? 1 : 0,
                50  => 2,
                52  => 3,
                88  => 4,
                91  => 0,
                92  => 0,
                93  => 1,
                95  => 2,
                96  => 2,
                97  => 3,
                98  => 3,
                99  => 4,
                100 => 4,
                107 => 0,
                108 => 0,
                117 => 3,
                218 => 0,
                219 => 0,
                _   => 0,
            };
        }


        public const byte DefaultPose   = byte.MaxValue;
        public const byte UnchangedPose = byte.MaxValue - 1;

        private readonly DalamudPluginInterface _pi;
        private readonly CPoseSettings          _cposeSettings;
        private readonly CommandManager         _commandManager;

        private readonly byte[] _defaultPoses = new byte[5];

        public byte DefaultStandingPose
            => _defaultPoses[0];

        public byte DefaultWeaponDrawnPose
            => _defaultPoses[1];

        public byte DefaultSitPose
            => _defaultPoses[2];

        public byte DefaultGroundSitPose
            => _defaultPoses[3];

        public byte DefaultDozePose
            => _defaultPoses[4];

        public IntPtr PlayerPointer { get; set; } = IntPtr.Zero;
        public bool   WeaponDrawn   { get; set; } = false;

        private unsafe int GetPersistentEmote()
        {
            const int persistentEmoteOffset = 0xE94;
            var       ptr                   = (byte*) PlayerPointer.ToPointer();
            return *(int*) (ptr + persistentEmoteOffset);
        }

        private unsafe int GetCPoseActorState()
        {
            const int cPoseOffset = 0xEA1;
            var       ptr         = (byte*) PlayerPointer.ToPointer();
            return *(ptr + cPoseOffset);
        }

        private unsafe byte GetPose(int which)
        {
            var ptr = (byte*) _cposeSettings.Address.ToPointer();
            return ptr[which];
        }

        private unsafe void WritePose(int which, byte pose)
        {
            var ptr = (byte*)_cposeSettings.Address.ToPointer();
            ptr[which] = pose;
        }

        public byte StandingPose
            => GetPose(0);

        public byte WeaponDrawnPose
            => GetPose(1);

        public byte SitPose
            => GetPose(2);

        public byte GroundSitPose
            => GetPose(3);

        public byte DozePose
            => GetPose(4);

        public void SetPose(int which, byte toWhat)
        {
            if (toWhat == UnchangedPose)
                return;

            if (toWhat == DefaultPose)
            {
                toWhat = _defaultPoses[which];
            }
            else if (toWhat >= NumPoses[which])
            {
                PluginLog.Error($"Higher pose requested than possible for {PoseNames[which]}: {toWhat} / {NumPoses[which]}.");
                return;
            }

            if (PlayerPointer == IntPtr.Zero)
                return;

            var currentEmote = GetPersistentEmote();
            var currentState = StateFromPose(currentEmote, WeaponDrawn);
            var pose         = GetPose(which);
            if (currentState == which)
            {
                if (toWhat == GetCPoseActorState())
                {
                    if (pose != toWhat)
                    {
                        WritePose(which, toWhat);
                        PluginLog.Debug("Overwrote {OldPose} with {NewPose} for {WhichPose:l}, currently in {CurrentState:l}.", pose, toWhat, PoseNames[which], PoseNames[currentState]);
                    }
                }
                else
                {
                    Task.Run(() =>
                    {
                        var i = 0;
                        do
                        {
                            PluginLog.Debug("Execute /cpose to get from {OldPose} to {NewPose} of {CurrentState:l}.", pose, toWhat, PoseNames[currentState]);
                            _commandManager.Execute("/cpose");
                            Task.Delay(50);
                        } while (toWhat != GetCPoseActorState() && i++ < 8);
                        if (i > 8)
                            PluginLog.Error("Could not change pose of {CurrentState:l}.", PoseNames[GetCPoseActorState()]);
                    });
                }
            }
            else if (pose != toWhat)
            {
                WritePose(which, toWhat);
                PluginLog.Debug("Overwrote {OldPose} with {NewPose} for {WhichPose:l}, currently in {CurrentState:l}.", pose, toWhat, PoseNames[which], PoseNames[currentState]);
            }
        }


        public void SetStandingPose(byte pose)
            => SetPose(0, pose);

        public void SetWeaponDrawnPose(byte pose)
            => SetPose(1, pose);

        public void SetSitPose(byte pose)
            => SetPose(2, pose);

        public void SetGroundSitPose(byte pose)
            => SetPose(3, pose);

        public void SetDozePose(byte pose)
            => SetPose(4, pose);


        public void SetPoses(byte standing, byte weaponDrawn, byte sitting, byte groundSitting, byte dozing)
        {
            SetPose(0, standing);
            SetPose(1, weaponDrawn);
            SetPose(2, sitting);
            SetPose(3, groundSitting);
            SetPose(4, dozing);
        }

        public void ResetDefaultPoses()
        {
            _defaultPoses[0] = GetPose(0);
            _defaultPoses[1] = GetPose(1);
            _defaultPoses[2] = GetPose(2);
            _defaultPoses[3] = GetPose(3);
            _defaultPoses[4] = GetPose(4);
        }

        private readonly Hook<CurrentPoseStateDelegate>? _cposeStateHook;

        public CPoseManager(DalamudPluginInterface pi, CommandManager commandManager)
        {
            _pi             = pi;
            _commandManager = commandManager;
            _cposeSettings  = new CPoseSettings(_pi.TargetModuleScanner);

            ResetDefaultPoses();
        }
    }
}
