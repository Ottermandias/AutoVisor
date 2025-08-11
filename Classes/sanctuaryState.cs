using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AutoVisor.Classes;

public unsafe class sanctuaryState
{
    public static bool IsInSanctuary()
        => TerritoryInfo.Instance()->InSanctuary;
}
