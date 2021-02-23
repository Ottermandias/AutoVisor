using System;
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

        public const  int      ActorJobOffset       = 0x01E2;
        public const  int      ActorRaceOffset      = 0x1878;
        public const  int      ActorHatOffset       = 0x1040;
        public const  int      ActorFlagsOffset     = 0x106C;
        public const  byte     ActorFlagsHideWeapon = 0b000010;
        public const  byte     ActorFlagsHideHat    = 0b000001;
        public const  byte     ActorFlagsVisor      = 0b010000;
        public const  string   VisorCommandEN       = "/visor";
        public const  string   VisorCommandDE       = "/visier";
        public const  string   VisorCommandJP       = "/visor";
        public const  string   VisorCommandFR       = "/visière";
        public static string[] VisorCommands        = InitVisorCommands();

        private static string[] InitVisorCommands()
        {
            var ret = new string[4];
            ret[ ( int )Dalamud.ClientLanguage.English ]  = VisorCommandEN;
            ret[ ( int )Dalamud.ClientLanguage.German ]   = VisorCommandDE;
            ret[ ( int )Dalamud.ClientLanguage.Japanese ] = VisorCommandJP;
            ret[ ( int )Dalamud.ClientLanguage.French ]   = VisorCommandFR;
            return ret;
        }

        public const uint GimmickVisorEnabledFlag  = 0b01;
        public const uint GimmickVisorAnimatedFlag = 0b10;

        public const ulong EqpHatHrothgarFlag = 0x0100000000000000;
        public const ulong EqpHatVieraFlag    = 0x0200000000000000;

        public bool IsActive { get; private set; }

        private const    int     NumStateLongs = 12;
        private readonly ulong[] _currentState = new ulong[NumStateLongs];

        private IntPtr _conditionPtr;
        private ushort _currentHatModelId;
        private bool   _enabled;
        private Job    _currentJob;
        private Race   _currentRace;
        private bool   _hatIsShown;
        private bool   _hatIsUseable;
        private bool   _visorIsEnabled;

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

        public unsafe void OnFrameworkUpdate( object framework )
        {
            for( var i = 0; i < NumStateLongs; ++i )
            {
                var condition = *( ulong* )( _conditionPtr + 8 * i ).ToPointer();
                if( condition != _currentState[ i ] )
                {
                    _currentState[ i ] = condition;
                    for ( ; i < NumStateLongs; ++i)
                        _currentState[ i ] = *( ulong* )( _conditionPtr + 8 * i ).ToPointer();;
                    break;
                }

                if( i == NumStateLongs - 1 )
                    return;
            }

            var player = Player();
            if( !_enabled || !_config.States.TryGetValue( player.Name, out var config )
                || !config.Enabled || !UpdateActor( player ) )
                return;

            UpdateJob( player );
            if( !config.PerJob.TryGetValue( _currentJob, out var flags ) )
                flags = config.PerJob[ Job.Default ];

            HandleState( flags.Set, flags.State, _pi.ClientState.Condition );
        }

        private void HandleState( VisorChangeStates set, VisorChangeStates state, Condition condition )
        {
            bool ApplyVisorChange( VisorChangeStates flag )
            {
                if( !set.HasFlag( flag ) )
                    return false;
                ToggleVisor( state.HasFlag( flag ) );
                return true;
            }

            if( condition[ ConditionFlag.Fishing ] && ApplyVisorChange( VisorChangeStates.Fishing ) )
                return;
            if( condition[ ConditionFlag.Gathering ] && ApplyVisorChange( VisorChangeStates.Gathering ) )
                return;
            if( condition[ ConditionFlag.Crafting ] && ApplyVisorChange( VisorChangeStates.Crafting ) )
                return;
            if( condition[ ConditionFlag.InFlight ] && ApplyVisorChange( VisorChangeStates.Flying ) )
                return;
            if( condition[ ConditionFlag.Diving ] && ApplyVisorChange( VisorChangeStates.Diving ) )
                return;
            if( condition[ ConditionFlag.UsingParasol ] && ApplyVisorChange( VisorChangeStates.Fashion ) )
                return;
            if( condition[ ConditionFlag.Mounted ] && ApplyVisorChange( VisorChangeStates.Mounted ) )
                return;
            if( condition[ ConditionFlag.Swimming ] && ApplyVisorChange( VisorChangeStates.Swimming ) )
                return;
            if( condition[ ConditionFlag.Casting ] && ApplyVisorChange( VisorChangeStates.Casting ) )
                return;
            if( condition[ ConditionFlag.InCombat ] && ApplyVisorChange( VisorChangeStates.Combat ) )
                return;
            if( condition[ ConditionFlag.BoundByDuty ] && ApplyVisorChange( VisorChangeStates.Duty ) )
                return;
            if( condition[ ConditionFlag.NormalConditions ] && ApplyVisorChange( VisorChangeStates.Normal ) )
                return;
        }

        private void ToggleVisor( bool on )
        {
            if( on == _visorIsToggled )
                return;
            _commandManager.Execute( VisorCommands[ ( int )_pi.ClientState.ClientLanguage ] );
        }

        private Actor Player()
        {
            var player = _pi.ClientState.LocalPlayer;
            _enabled = player != null;
            return player;
        }

        private bool UpdateActor( Actor player )
        {
            _enabled &= UpdateFlags( player );
            if( !_enabled )
                return false;

            _enabled &= UpdateHat( player );
            return _enabled;
        }

        private bool UpdateJob( Actor actor )
        {
            var job = ( Job )Marshal.ReadByte( actor.Address + ActorJobOffset );
            var ret = job != _currentJob;
            _currentJob = job;
            return ret;
        }

        private bool UpdateRace( Actor actor )
        {
            var race = ( Race )Marshal.ReadByte( actor.Address + ActorRaceOffset );
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

        private bool UpdateHat( Actor actor )
        {
            var hat = ( ushort )Marshal.ReadInt16( actor.Address + ActorHatOffset );
            if( hat != _currentHatModelId )
            {
                _currentHatModelId = hat;
                if( !UpdateVisor() )
                    return false;

                UpdateRace( actor );
                return UpdateUsable();
            }

            if( UpdateRace( actor ) ) return UpdateUsable();

            return _visorIsEnabled && _hatIsUseable;
        }

        private bool UpdateFlags( Actor actor )
        {
            var flags = Marshal.ReadByte( actor.Address + ActorFlagsOffset );
            _hatIsShown     = ( flags & ActorFlagsHideHat ) != ActorFlagsHideHat;
            _visorIsToggled = ( flags & ActorFlagsVisor ) == ActorFlagsVisor;
            return _hatIsShown;
        }
    }
}
