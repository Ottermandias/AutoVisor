using System;

namespace AutoVisor.Classes;

[Flags]
public enum VisorChangeStates : ulong
{
    Normal    = 1 << 0,
    Mounted   = 1 << 1,
    Flying    = 1 << 2,
    Swimming  = 1 << 3,
    Diving    = 1 << 4,
    Fashion   = 1 << 5,
    Crafting  = 1 << 6,
    Gathering = 1 << 7,
    Fishing   = 1 << 8,
    Combat    = 1 << 9,
    Casting   = 1 << 10,
    Duty      = 1 << 11,
    Drawn     = 1 << 12,
}
