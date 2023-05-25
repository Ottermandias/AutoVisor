namespace AutoVisor;

public static class Offsets
{
    public static class Character
    {
        public const int HatVisible    = 0x876;
        public const int VisorToggled  = 0x877;
        public const int WeaponDrawn   = 0x1B38;
        public const int WeaponHidden1 = 0x877;
        public const int CPose         = 0x641;

        public static class Flags
        {
            public const byte IsHatHidden     = 0x01;
            public const byte IsVisorToggled  = 0x08;
            public const byte IsWeaponDrawn   = 0x01;
            public const byte IsWeaponHidden1 = 0x01;
        }
    }

    public static class Meta
    {
        public static class Flags
        {
            public const uint GimmickVisorEnabled  = 0b01;
            public const uint GimmickVisorAnimated = 0b10;

            public const ulong EqpHatHrothgar = 0x0100000000000000;
            public const ulong EqpHatViera    = 0x0200000000000000;
        }
    }
}
