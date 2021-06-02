using Dalamud.Game;

namespace AutoVisor.SeFunctions
{
    public sealed class CPoseSettings : SeAddressBase
    {
        public CPoseSettings(SigScanner sigScanner)
            : base(sigScanner, "48 8D 05 ?? ?? ?? ?? 0F B6 1C 38")
        { }
    }
}
