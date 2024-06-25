using System;
using System.Linq;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AutoVisor.Managers;

public unsafe class CPoseManager
{
    private static readonly int[] NumPoses = Enum.GetValues<PoseType>().Select(p => PlayerState.AvailablePoses(p) + 1).ToArray();

    public static readonly string[] PoseNames = Enum.GetValues<PoseType>().Select(p => p switch
    {
        PoseType.Idle        => "Standing Pose",
        PoseType.WeaponDrawn => "Weapon Drawn Pose",
        PoseType.Sit         => "Sitting Pose",
        PoseType.GroundSit   => "Sitting on Ground Pose",
        PoseType.Doze        => "Dozing Pose",
        PoseType.Umbrella    => "Umbrella Pose",
        PoseType.Accessory   => "Accessory Pose",
        _                    => $"{p} Pose",
    }).ToArray();

    public static int Num(PoseType type)
        => NumPoses[(int)type];

    public static string Name(PoseType type)
        => PoseNames[(int)type];

    private PoseType TranslateState(byte state)
    {
        return state switch
        {
            1 => PoseType.GroundSit,
            2 => PoseType.Sit,
            3 => PoseType.Doze,
            _ => Umbrella ? PoseType.Umbrella : WeaponDrawn ? PoseType.WeaponDrawn : Accessory ? PoseType.Accessory : PoseType.Idle,
        };
    }

    public const byte DefaultPose   = byte.MaxValue;
    public const byte UnchangedPose = byte.MaxValue - 1;

    private readonly CommandManager _commandManager;

    private readonly byte[] _defaultPoses = new byte[NumPoses.Length];

    public Character* PlayerPointer { get; set; } = null;
    public bool       WeaponDrawn   { get; set; } = false;
    public bool       Umbrella      { get; set; } = false;
    public bool       Accessory     { get; set; } = false;

    private int GetCPoseActorState()
        => PlayerPointer->EmoteController.CPoseState;

    private static byte GetPose(PoseType which)
        => PlayerState.Instance()->CurrentPose(which);

    private static void WritePose(PoseType which, byte pose)
        => PlayerState.Instance()->SelectedPoses[(int)which] = pose;

    public void SetPose(PoseType which, byte toWhat)
    {
        if (toWhat == UnchangedPose)
            return;

        if (toWhat == DefaultPose)
        {
            toWhat = _defaultPoses[(int)which];
        }
        else if (toWhat >= NumPoses[(int)which])
        {
            Dalamud.Log.Error($"Higher pose requested than possible for {Name(which)}: {toWhat} / {Num(which)}.");
            return;
        }

        if (PlayerPointer == null)
            return;

        var currentState = TranslateState(PlayerPointer->ModeParam);
        var pose         = GetPose(which);
        if (currentState == which)
        {
            if (toWhat == GetCPoseActorState())
            {
                if (pose != toWhat)
                {
                    WritePose(which, toWhat);
                    Dalamud.Log.Debug("Overwrote {OldPose} with {NewPose} for {WhichPose:l}, currently in {CurrentState:l}.", pose, toWhat,
                        Name(which), Name(currentState));
                }
            }
            else
            {
                Task.Run(() =>
                {
                    var i = 0;
                    do
                    {
                        Dalamud.Log.Debug("Execute /cpose to get from {OldPose} to {NewPose} of {CurrentState:l}.", pose, toWhat,
                            Name(currentState));
                        _commandManager.Execute("/cpose");
                        Task.Delay(50);
                    } while (toWhat != GetCPoseActorState() && i++ < 8);

                    if (i > 8)
                        Dalamud.Log.Error("Could not change pose of {CurrentState:l}.", PoseNames[GetCPoseActorState()]);
                });
            }
        }
        else if (pose != toWhat)
        {
            WritePose(which, toWhat);
            Dalamud.Log.Debug("Overwrote {OldPose} with {NewPose} for {WhichPose:l}, currently in {CurrentState:l}.", pose, toWhat,
                Name(which), Name(currentState));
        }
    }

    public void ResetDefaultPoses()
    {
        foreach (var pose in Enum.GetValues<PoseType>())
            _defaultPoses[(int)pose] = GetPose(pose);
    }

    public CPoseManager(CommandManager commandManager)
    {
        _commandManager = commandManager;
        ResetDefaultPoses();
    }
}
