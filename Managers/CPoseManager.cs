using System;
using System.Linq;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AutoVisor.Managers;

public unsafe class CPoseManager
{
    private static readonly int[] NumPoses = Enum.GetValues<EmoteController.PoseType>().Select(p => EmoteController.GetAvailablePoses(p) + 1).ToArray();

    public static readonly string[] PoseNames = Enum.GetValues<EmoteController.PoseType>().Select(p => p switch
    {
        EmoteController.PoseType.Idle        => "Standing Pose",
        EmoteController.PoseType.WeaponDrawn => "Weapon Drawn Pose",
        EmoteController.PoseType.Sit         => "Sitting Pose",
        EmoteController.PoseType.GroundSit   => "Sitting on Ground Pose",
        EmoteController.PoseType.Doze        => "Dozing Pose",
        EmoteController.PoseType.Umbrella    => "Umbrella Pose",
        EmoteController.PoseType.Accessory   => "Accessory Pose",
        _                                    => $"{p} Pose",
    }).ToArray();

    public static int Num(EmoteController.PoseType type)
        => NumPoses[(int)type];

    public static string Name(EmoteController.PoseType type)
        => PoseNames[(int)type];

    private EmoteController.PoseType TranslateState(byte state)
    {
        return state switch
        {
            1 => EmoteController.PoseType.GroundSit,
            2 => EmoteController.PoseType.Sit,
            3 => EmoteController.PoseType.Doze,
            _ => Umbrella ? EmoteController.PoseType.Umbrella : WeaponDrawn ? EmoteController.PoseType.WeaponDrawn : Accessory ? EmoteController.PoseType.Accessory : EmoteController.PoseType.Idle,
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

    private static byte GetPose(EmoteController.PoseType which)
        => PlayerState.Instance()->CurrentPose(which);

    private static void WritePose(EmoteController.PoseType which, byte pose)
        => PlayerState.Instance()->SelectedPoses[(int)which] = pose;

    public void SetPose(EmoteController.PoseType which, byte toWhat)
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
        foreach (var pose in Enum.GetValues<EmoteController.PoseType>())
            _defaultPoses[(int)pose] = GetPose(pose);
    }

    public CPoseManager(CommandManager commandManager)
    {
        _commandManager = commandManager;
        ResetDefaultPoses();
    }
}
