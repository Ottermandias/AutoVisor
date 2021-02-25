using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using AutoVisor.Classes;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Internal;
using Dalamud.Plugin;

namespace AutoVisor.Managers
{
    public class VisorManager : IDisposable
    {
        public const string EquipmentParameters = "chara/xls/equipmentparameter/equipmentparameter.eqp";
        public const string GimmickParameters   = "chara/xls/equipmentparameter/gimmickparameter.gmp";

        public const  int      ActorJobOffset         = 0x01E2;
        public const  int      ActorRaceOffset        = 0x1878;
        public const  int      ActorHatOffset         = 0x1040;
        public const  int      ActorFlagsOffset       = 0x106C;
        public const  int      ActorWeaponDrawnOffset = 0x1980;
        public const  byte     ActorFlagsHideWeapon   = 0b000010;
        public const  byte     ActorFlagsHideHat      = 0b000001;
        public const  byte     ActorFlagsVisor        = 0b010000;
        public const  byte     ActorWeaponDrawn       = 0b100;
        public static string[] VisorCommands          = InitVisorCommands();
        public static string[] HideHatCommands        = InitHideHatCommands();
        public static string[] HideWeaponCommands     = InitHideWeaponCommands();
        public static string[] OnStrings              = InitOnStrings();
        public static string[] OffStrings             = InitOffStrings();


        public static readonly Dictionary< VisorChangeStates, bool > ValidStatesForWeapon = new()
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
            { VisorChangeStates.Drawn, false }
        };

        private static string[] InitVisorCommands()
        {
            var ret = new string[4];
            ret[ ( int )Dalamud.ClientLanguage.English ]  = "/visor";
            ret[ ( int )Dalamud.ClientLanguage.German ]   = "/visier";
            ret[ ( int )Dalamud.ClientLanguage.Japanese ] = "/visor";
            ret[ ( int )Dalamud.ClientLanguage.French ]   = "/visière";
            return ret;
        }

        private static string[] InitHideHatCommands()
        {
            var ret = new string[4];
            ret[ ( int )Dalamud.ClientLanguage.English ]  = "/displayhead";
            ret[ ( int )Dalamud.ClientLanguage.German ]   = "/helm";
            ret[ ( int )Dalamud.ClientLanguage.Japanese ] = "/displayhead";
            ret[ ( int )Dalamud.ClientLanguage.French ]   = "/affichagecouvreche";
            return ret;
        }

        private static string[] InitHideWeaponCommands()
        {
            var ret = new string[4];
            ret[ ( int )Dalamud.ClientLanguage.English ]  = "/displayarms";
            ret[ ( int )Dalamud.ClientLanguage.German ]   = "/waffe";
            ret[ ( int )Dalamud.ClientLanguage.Japanese ] = "/displayarms";
            ret[ ( int )Dalamud.ClientLanguage.French ]   = "/affichagearmes";
            return ret;
        }

        private static string[] InitOnStrings()
        {
            var ret = new string[4];
            ret[ ( int )Dalamud.ClientLanguage.English ]  = "on";
            ret[ ( int )Dalamud.ClientLanguage.German ]   = "ein";
            ret[ ( int )Dalamud.ClientLanguage.Japanese ] = "on";
            ret[ ( int )Dalamud.ClientLanguage.French ]   = "activé";
            return ret;
        }

        private static string[] InitOffStrings()
        {
            var ret = new string[4];
            ret[ ( int )Dalamud.ClientLanguage.English ]  = "off";
            ret[ ( int )Dalamud.ClientLanguage.German ]   = "aus";
            ret[ ( int )Dalamud.ClientLanguage.Japanese ] = "off";
            ret[ ( int )Dalamud.ClientLanguage.French ]   = "désactivé";
            return ret;
        }

        public const uint GimmickVisorEnabledFlag  = 0b01;
        public const uint GimmickVisorAnimatedFlag = 0b10;

        public const ulong EqpHatHrothgarFlag = 0x0100000000000000;
        public const ulong EqpHatVieraFlag    = 0x0200000000000000;

        public bool IsActive { get; private set; }

        private const    int     NumStateLongs = 12;
        private readonly ulong[] _currentState = new ulong[NumStateLongs];
        private          byte    _currentWeaponDrawn;

        private readonly IntPtr _actorTablePtr;
        private readonly IntPtr _conditionPtr;
        private          ushort _currentHatModelId;
        private          Job    _currentJob;
        private          Race   _currentRace;
        private          string _currentName;
        private          bool   _hatIsShown;
        private          bool   _weaponIsShown;
        private          bool   _hatIsUseable;
        private          bool   _visorIsEnabled;
        private          bool   _visorEnabled;

        private bool _visorIsToggled;
        // private bool   _visorIsAnimated;


        private readonly DalamudPluginInterface _pi;
        private readonly AutoVisorConfiguration _config;
        private readonly CommandManager         _commandManager;
        private readonly EqpFile                _eqpFile;
        private readonly EqpFile                _gmpFile;

        public VisorManager( DalamudPluginInterface pi, AutoVisorConfiguration config, CommandManager commandManager )
            : this( pi, config, commandManager
                , new EqpFile( pi.Data.GetFile( EquipmentParameters ) )
                , new EqpFile( pi.Data.GetFile( GimmickParameters ) ) )
        { }

        public VisorManager( DalamudPluginInterface pi, AutoVisorConfiguration config, CommandManager commandManager, EqpFile eqp, EqpFile gmp )
        {
            _pi             = pi;
            _config         = config;
            _commandManager = commandManager;
            _eqpFile        = eqp;
            _gmpFile        = gmp;
            // Some hacky shit to not resolve the address again.
            _conditionPtr = BaseAddressResolver.DebugScannedValues[ "ClientStateAddressResolver" ]
                .Find( kvp => kvp.Item1 == "ConditionFlags" ).Item2;
            _actorTablePtr = BaseAddressResolver.DebugScannedValues[ "ClientStateAddressResolver" ]
                .Find( kvp => kvp.Item1 == "ActorTable" ).Item2;
        }

        public void Dispose()
            => Deactivate();

        public void Activate()
        {
            if( !IsActive )
            {
                IsActive                    =  true;
                _pi.Framework.OnUpdateEvent += OnFrameworkUpdate;
            }
        }

        public void Deactivate()
        {
            if( IsActive )
            {
                IsActive                    =  false;
                _pi.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            }
        }

        public void ResetState()
            => Array.Clear( _currentState, 0, NumStateLongs );

        private static readonly ulong[] RelevantConditionsBitmask = new ulong[NumStateLongs]
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
            0x0001000000000000
        };

        private static readonly ulong[] WaitStateConditionsBitmask = new ulong[NumStateLongs]
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
            0x0000FFFF00000000
        };

        public unsafe void OnFrameworkUpdate( object framework )
        {
            for( var i = 0; i < NumStateLongs; ++i )
            {
                if( ( WaitStateConditionsBitmask[ i ] & *( ulong* )( _conditionPtr + 8 * i ).ToPointer() ) != 0ul )
                    return;
            }

            var player = Player();
            if( player == IntPtr.Zero )
                return;
            UpdateJob( player );
            UpdateName( player );

            for( var i = 0; i < NumStateLongs; ++i )
            {
                var condition = RelevantConditionsBitmask[ i ] & *( ulong* )( _conditionPtr + 8 * i ).ToPointer();
                if( condition != _currentState[ i ] )
                {
                    _currentState[ i ] = condition;
                    for( ; i < NumStateLongs; ++i )
                        _currentState[ i ] = RelevantConditionsBitmask[ i ] & *( ulong* )( _conditionPtr + 8 * i ).ToPointer();
                    ;
                    break;
                }

                if( i == NumStateLongs - 1 )
                {
                    var weaponDrawn = *( ( byte* )player.ToPointer() + ActorWeaponDrawnOffset );
                    if( weaponDrawn == _currentWeaponDrawn )
                        return;
                    _currentWeaponDrawn = weaponDrawn;
                }
            }

            if( !_visorEnabled || !_config.States.TryGetValue( _currentName, out var config ) || !config.Enabled )
                return;

            UpdateActor( player );
            if( !config.PerJob.TryGetValue( _currentJob, out var flags ) )
                flags = config.PerJob[ Job.Default ];

            HandleState( flags, _pi.ClientState.Condition );
        }

        private static readonly (ConditionFlag, VisorChangeStates)[] Conditions = new (ConditionFlag, VisorChangeStates)[]
        {
            ( ConditionFlag.Fishing, VisorChangeStates.Fishing ),
            ( ConditionFlag.Gathering, VisorChangeStates.Gathering ),
            ( ConditionFlag.Crafting, VisorChangeStates.Crafting ),
            ( ConditionFlag.InFlight, VisorChangeStates.Flying ),
            ( ConditionFlag.Diving, VisorChangeStates.Diving ),
            ( ConditionFlag.UsingParasol, VisorChangeStates.Fashion ),
            ( ConditionFlag.Mounted, VisorChangeStates.Mounted ),
            ( ConditionFlag.Swimming, VisorChangeStates.Swimming ),
            ( ConditionFlag.Casting, VisorChangeStates.Casting ),
            ( ConditionFlag.InCombat, VisorChangeStates.Combat ),
            ( ConditionFlag.None, VisorChangeStates.Drawn ),
            ( ConditionFlag.BoundByDuty, VisorChangeStates.Duty ),
            ( ConditionFlag.NormalConditions, VisorChangeStates.Normal )
        };

        private void HandleState( VisorChangeGroup visor, Condition condition )
        {
            var hatSet    = !_hatIsUseable || visor.HideHatSet == 0;
            var visorSet  = hatSet && _visorEnabled && visor.VisorSet == 0;
            var weaponSet = visor.HideWeaponSet == 0;

            bool ApplyHatChange( VisorChangeStates flag )
            {
                if( !visor.HideHatSet.HasFlag( flag ) )
                    return false;
                ToggleHat( visor.HideHatState.HasFlag( flag ) );
                return true;
            }

            bool ApplyVisorChange( VisorChangeStates flag )
            {
                if( !visor.VisorSet.HasFlag( flag ) )
                    return false;
                ToggleVisor( visor.VisorState.HasFlag( flag ) );
                return true;
            }

            bool ApplyWeaponChange( VisorChangeStates flag )
            {
                if( !ValidStatesForWeapon[ flag ] || !visor.HideWeaponSet.HasFlag( flag ) )
                    return false;
                ToggleWeapon( visor.HideWeaponState.HasFlag( flag ) );
                return true;
            }

            foreach( var (flag, state) in Conditions )
            {
                if( visorSet && hatSet && weaponSet )
                    return;

                var doStuff = state switch
                {
                    VisorChangeStates.Drawn => ( _currentWeaponDrawn & ActorWeaponDrawn ) == ActorWeaponDrawn,
                    _                       => condition[ flag ]
                };
                if( doStuff )
                {
                    hatSet    = hatSet || ApplyHatChange( state );
                    visorSet  = visorSet || ApplyVisorChange( state );
                    weaponSet = weaponSet || ApplyWeaponChange( state );
                }
            }
        }

        private void ToggleWeapon( bool on )
        {
            if( on == _weaponIsShown )
                return;
            var lang = ( int )_pi.ClientState.ClientLanguage;
            _commandManager.Execute( $"{HideWeaponCommands[ lang ]} {( on ? OnStrings[ lang ] : OffStrings[ lang ] )}" );
            _weaponIsShown = on;
        }

        private void ToggleHat( bool on )
        {
            if( on == _hatIsShown )
                return;
            var lang = ( int )_pi.ClientState.ClientLanguage;

            if( on )
            {
                _commandManager.Execute( $"{HideHatCommands[ lang ]} {OnStrings[ lang ]}" );
                _hatIsShown   = true;
                _visorEnabled = _visorIsEnabled;
            }
            else
            {
                _commandManager.Execute( $"{HideHatCommands[ lang ]} {OffStrings[ lang ]}" );
                _hatIsShown   = false;
                _visorEnabled = false;
            }
        }

        private void ToggleVisor( bool on )
        {
            if( !_visorEnabled || on == _visorIsToggled )
                return;
            _commandManager.Execute( VisorCommands[ ( int )_pi.ClientState.ClientLanguage ] );
            _visorIsToggled = on;
        }

        private IntPtr Player()
        {
            var player = Marshal.ReadIntPtr( _actorTablePtr );
            _visorEnabled = player != IntPtr.Zero;
            return player;
        }

        private void UpdateName( IntPtr player )
        {
            var name = Marshal.PtrToStringAnsi( player + 0x30, 30 ).TrimEnd( '\0' );
            if( name != _currentName )
            {
                ResetState();
                _currentName = name;
            }
        }

        private void UpdateActor( IntPtr player )
        {
            _visorEnabled &= UpdateFlags( player );
            _visorEnabled &= UpdateHat( player );
        }

        private void UpdateJob( IntPtr actor )
        {
            var job = ( Job )Marshal.ReadByte( actor + ActorJobOffset );
            if( job != _currentJob )
            {
                ResetState();
                _currentJob = ( Job )Marshal.ReadByte( actor + ActorJobOffset );
            }
        }

        private bool UpdateRace( IntPtr actor )
        {
            var race = ( Race )Marshal.ReadByte( actor + ActorRaceOffset );
            var ret  = race != _currentRace;
            _currentRace = race;
            return ret;
        }

        private bool UpdateVisor()
        {
            var gmpEntry = _gmpFile.GetEntry( _currentHatModelId );
            // _visorIsAnimated = ( gmpEntry & GimmickVisorAnimatedFlag ) == GimmickVisorAnimatedFlag;
            _visorIsEnabled = ( gmpEntry & GimmickVisorEnabledFlag ) == GimmickVisorEnabledFlag;
            return _visorIsEnabled;
        }

        private bool UpdateUsable()
        {
            _hatIsUseable = _currentRace switch
            {
                Race.Hrothgar => ( _eqpFile.GetEntry( _currentHatModelId ) & EqpHatHrothgarFlag ) == EqpHatHrothgarFlag,
                Race.Viera    => ( _eqpFile.GetEntry( _currentHatModelId ) & EqpHatVieraFlag ) == EqpHatVieraFlag,
                _             => true
            };

            return _hatIsUseable;
        }

        private bool UpdateHat( IntPtr actor )
        {
            var hat = ( ushort )Marshal.ReadInt16( actor + ActorHatOffset );
            if( hat != _currentHatModelId )
            {
                _currentHatModelId = hat;
                if( !UpdateVisor() )
                    return false;

                UpdateRace( actor );
                return UpdateUsable();
            }

            if( UpdateRace( actor ) )
                return UpdateUsable();

            return _visorIsEnabled && _hatIsUseable;
        }

        private bool UpdateFlags( IntPtr actor )
        {
            var flags = Marshal.ReadByte( actor + ActorFlagsOffset );
            _weaponIsShown  = ( flags & ActorFlagsHideWeapon ) != ActorFlagsHideWeapon;
            _hatIsShown     = ( flags & ActorFlagsHideHat ) != ActorFlagsHideHat;
            _visorIsToggled = ( flags & ActorFlagsVisor ) == ActorFlagsVisor;
            return _hatIsShown;
        }
    }
}
