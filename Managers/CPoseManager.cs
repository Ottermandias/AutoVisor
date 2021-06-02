using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AutoVisor.SeFunctions;
using Dalamud.Hooking;
using Dalamud.Plugin;

namespace AutoVisor.Managers
{
    public class CPoseManager : IDisposable
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

        private unsafe byte GetPose(int which)
        {
            var ptr = (byte*) _cposeSettings.Address.ToPointer();
            return ptr[which];
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


        private int  _poseTypeBeingSet = -1;

        private void SetNextPoseTarget(int which)
        {
            _poseTypeBeingSet = which;
        }

        public void SetPose(int which, byte toWhat)
        {
            if (_cposeStateHook == null || _cposeSettings.Address == IntPtr.Zero)
            {
                PluginLog.Error("Game hooks missing.");
                return;
            }

            if (which < 0 || which > NumPoses.Length)
            {
                PluginLog.Error($"Invalid pose type {which} requested.");
                return;
            }

            if (toWhat == UnchangedPose)
                return;

            if (toWhat == DefaultPose)
                toWhat = _defaultPoses[which];
            else if (toWhat >= NumPoses[which])
            {
                PluginLog.Error($"Higher pose requested than possible for {which}: {toWhat} / {NumPoses[which]}.");
                return;
            }

            if (toWhat == GetPose(which))
                return;

            Task.Run(() =>
            {
                do
                {
                    _poseTypeBeingSet = which;
                    _commandManager.Execute("/cpose");
                    Task.Delay(24);
                } while (toWhat != GetPose(which));
            });
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

            _cposeStateHook = new GetCurrentPoseState(_pi.TargetModuleScanner).CreateHook(CposeStateDetour, this);
        }

        private ulong CposeStateDetour(ulong unk)
        {
            var ret = _cposeStateHook!.Original(unk);
            PluginLog.Verbose("Current cpose state, parameter {Param}, Value {Ret}, setting to {Target}.", unk, ret, _poseTypeBeingSet);
            if (_poseTypeBeingSet != -1)
            {
                ret               = (ulong) _poseTypeBeingSet;
                _poseTypeBeingSet = -1;
            }

            return ret;
        }

        public void Dispose()
        {
            _cposeStateHook?.Disable();
            _cposeStateHook?.Dispose();
        }
    }
}
