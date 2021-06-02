using Dalamud.Game;

namespace AutoVisor.SeFunctions
{
    public delegate ulong CurrentPoseStateDelegate(ulong unk);

    public sealed class GetCurrentPoseState : SeFunctionBase<CurrentPoseStateDelegate>
    {
        public GetCurrentPoseState(SigScanner sigScanner)
            : base(sigScanner, "40 ?? 48 83 ?? ?? 48 ?? ?? ?? 48 8B ?? ?? 83 ?? ?? ?? ?? ?? ?? 0F")
        { }
    }
}
