using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AutoVisor.Classes;
using Dalamud.Plugin;
using ImGuiNET;

namespace AutoVisor.GUI
{
    public class AutoVisorUi
    {
        private const string PluginName   = "AutoVisor Configuration";
        private const string LabelEnabled = "Enable AutoVisor";

        private static readonly Vector2  MinSize  = new( 875, 200 );
        private static readonly Vector2  MaxSize  = new( 875, 50000 );
        private static readonly Job[]    Jobs     = Enum.GetValues( typeof( Job ) ).Cast< Job >().ToArray();
        private static readonly string[] JobNames = Enum.GetNames( typeof( Job ) );

        private static readonly VisorChangeStates[] VisorStates =
            Enum.GetValues( typeof( VisorChangeStates ) ).Cast< VisorChangeStates >().ToArray();

        private static readonly string[] VisorStateNames = Enum.GetNames( typeof( VisorChangeStates ) );


        private readonly AutoVisor              _plugin;
        private readonly DalamudPluginInterface _pi;
        private readonly AutoVisorConfiguration _config;

        public bool Visible;

        private readonly List< string > _players;

        private int _currentPlayer = 0;
        private int _currentJob    = 0;

        public AutoVisorUi( AutoVisor plugin, DalamudPluginInterface pi, AutoVisorConfiguration config )
        {
            _plugin  = plugin;
            _pi      = pi;
            _config  = config;
            _players = _config.States.Select( kvp => kvp.Key ).ToList();
        }

        private bool AddPlayer( PlayerConfig config )
        {
            var name = _pi.ClientState.LocalPlayer?.Name ?? "";
            if( name.Length == 0 || _config.States.ContainsKey( name ) )
                return false;
            _players.Add( name );
            _config.States[ name ] = config.Clone();
            Save();
            return true;
        }

        private bool AddPlayer()
            => AddPlayer( new PlayerConfig() );

        private void RemovePlayer( string name )
        {
            _players.Remove( name );
            _config.States.Remove( name );
            Save();
        }

        private void Save()
        {
            _pi.SavePluginConfig( _config );
            _plugin.ResetVisorState();
        }

        private void DrawEnabledCheckbox()
        {
            var tmp = _config.Enabled;
            if( ImGui.Checkbox( LabelEnabled, ref tmp ) && _config.Enabled != tmp )
            {
                _config.Enabled = tmp;
                Save();
            }
        }

        private void DrawSettingsHeaders()
        {
            ImGui.Columns( VisorStateNames.Length + 1, $"##header_{_currentPlayer}", true );
            ImGui.NextColumn();
            foreach( var name in VisorStateNames )
            {
                ImGui.Text( name );
                ImGui.NextColumn();
            }
        }

        private void DrawAddJobLine( PlayerConfig settings )
        {
            var availableJobsAndIndices = JobNames.Select( ( j, i ) => ( j, i ) ).Where( p => !settings.PerJob.ContainsKey( Jobs[ p.i ] ) );
            var (jobs, indices) = ( availableJobsAndIndices.Select( j => j.j ).ToArray(),
                availableJobsAndIndices.Select( j => j.i ).ToArray() );
            if( jobs.Length == 0 )
                return;
            var which = 0;

            ImGui.PushStyleVar( ImGuiStyleVar.FramePadding, Vector2.Zero );
            if( ImGui.Combo( $"##AddJob_{_currentPlayer}", ref which, jobs, jobs.Length ) )
            {
                settings.PerJob.Add( Jobs[ indices[ which ] ], ( 0, 0 ) );
                Save();
            }

            ImGui.PopStyleVar();

            ImGui.NextColumn();
            foreach( var v in VisorStates )
                ImGui.NextColumn();
        }

        private void DrawSettingsLine( PlayerConfig settings )
        {
            var jobSettings = settings.PerJob.ElementAt( _currentJob );
            var job         = jobSettings.Key;
            var name        = JobNames[ ( int )jobSettings.Key ];
            var set         = jobSettings.Value.Set;
            var state       = jobSettings.Value.State;

            ImGui.Separator();
            if( job != Job.Default )
            {
                if( ImGui.Button( $"-##0{_currentPlayer}_{_currentJob}", new Vector2( 20, 23 ) ) )
                {
                    settings.PerJob.Remove( settings.PerJob.ElementAt( _currentJob ).Key );
                    _currentJob = Math.Max( 0, _currentJob - 1 );
                    Save();
                }

                ImGui.SameLine();
            }

            ImGui.Text( name );
            ImGui.NextColumn();
            foreach( var v in VisorStates )
            {
                var tmp1 = set.HasFlag( v );
                ImGui.Checkbox( $"##0_{_currentPlayer}_{_currentJob}_{v}", ref tmp1 );
                if( ImGui.IsItemHovered() )
                    ImGui.SetTooltip( "Enable visor change on this state." );
                if( !tmp1 )
                    ImGui.PushStyleVar( ImGuiStyleVar.Alpha, 0.35f );

                var tmp2 = tmp1 && state.HasFlag( v );
                ImGui.SameLine();
                ImGui.Checkbox( $"##1_{_currentPlayer}_{_currentJob}_{v}", ref tmp2 );
                if( !tmp1 )
                {
                    tmp2 = false;
                    ImGui.PopStyleVar();
                }

                if( ImGui.IsItemHovered() )
                    ImGui.SetTooltip( "Visor off/on." );


                if( tmp1 != set.HasFlag( v ) || tmp2 != state.HasFlag( v ) )
                {
                    var newSet   = tmp1 ? set | v : set & ~v;
                    var newState = tmp2 ? state | v : state & ~v;
                    settings.PerJob[ job ] = ( newSet, newState );
                    Save();
                }

                ImGui.NextColumn();
            }
        }

        private void DrawPlayerGroup()
        {
            var name = _players.ElementAt( _currentPlayer );

            if( !ImGui.CollapsingHeader( name ) )
                return;

            ImGui.Dummy( new Vector2( 0, 5 ) );

            var playerConfig = _config.States[ name ];
            var tmp          = playerConfig.Enabled;
            if( ImGui.Checkbox( $"Enabled##{name}", ref tmp ) && tmp != playerConfig.Enabled )
            {
                playerConfig.Enabled = tmp;
                Save();
            }

            ImGui.SameLine();
            if( ImGui.Button( $"Delete##{name}" ) )
            {
                RemovePlayer( name );
                --_currentPlayer;
                ImGui.Columns( 1 );
                return;
            }

            ImGui.SameLine();
            if( ImGui.Button( $"Duplicate##{name}" ) )
                AddPlayer( _config.States[ name ] );

            ImGui.Dummy( new Vector2( 0, 5 ) );
            DrawSettingsHeaders();

            for( _currentJob = 0; _currentJob < playerConfig.PerJob.Count; ++_currentJob )
                DrawSettingsLine( playerConfig );

            DrawAddJobLine( playerConfig );
            ImGui.Columns( 1 );
        }

        private static void DrawHelp()
        {
            if( !ImGui.CollapsingHeader( "Help", ImGuiTreeNodeFlags.DefaultOpen ) )
                return;

            ImGui.TextWrapped( "AutoVisor allows you to automatically use /visor on certain conditions. " +
                "The configuration is character-name and job-specific, with a default job that triggers if no specific job is active. " +
                "The first checkbox per column activates automatic changing for the specific condition, the second indicates to which state the visor should be changed.\n" +
                "Precedences are:\n" +
                "\t1. Fishing\n" +
                "\t2. Gathering ~ Crafting\n" +
                "\t3. In Flight ~ Diving\n" +
                "\t4. Mounted ~ Swimming ~ Wearing Fashion Accessories\n" +
                "\t5. Casting\n" +
                "\t6. In Combat\n" +
                "\t7. In Duty\n" +
                "\t8. Normal." );
        }

        private void DrawPlayerAdd()
        {
            var name = _pi.ClientState.LocalPlayer?.Name ?? "";
            if( name.Length == 0 || _config.States.ContainsKey( name ) )
                return;

            if( ImGui.Button( "Add settings for this character..." ) )
                AddPlayer();
        }

        public void Draw()
        {
            if( !Visible )
                return;

            ImGui.SetNextWindowSizeConstraints( MinSize, MaxSize );
            if( !ImGui.Begin( PluginName, ref Visible ) )
                return;

            DrawEnabledCheckbox();
            ImGui.SameLine();
            DrawPlayerAdd();

            ImGui.Dummy( new Vector2( 0, 10 ) );

            DrawHelp();
            ImGui.Dummy( new Vector2( 0, 10 ) );
            for( _currentPlayer = 0; _currentPlayer < _players.Count; ++_currentPlayer )
            {
                DrawPlayerGroup();
                ImGui.Dummy( new Vector2( 0, 5 ) );
            }

            ImGui.End();
        }
    }
}
