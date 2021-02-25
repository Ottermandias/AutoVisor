using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AutoVisor.Classes;
using AutoVisor.Managers;
using Dalamud.Plugin;
using ImGuiNET;

namespace AutoVisor.GUI
{
    public class AutoVisorUi
    {
        private const string PluginName   = "AutoVisor Configuration";
        private const string LabelEnabled = "Enable AutoVisor";

        private static readonly Job[]    Jobs     = Enum.GetValues( typeof( Job ) ).Cast< Job >().ToArray();
        private static readonly string[] JobNames = Enum.GetNames( typeof( Job ) );

        private static readonly VisorChangeStates[] VisorStates =
            Enum.GetValues( typeof( VisorChangeStates ) ).Cast< VisorChangeStates >().ToArray();

        private static readonly string[] VisorStateNames = Enum.GetNames( typeof( VisorChangeStates ) );

        private static readonly VisorChangeStates[] VisorStatesWeapon =
            VisorStates.Where( v => VisorManager.ValidStatesForWeapon[ v ] ).ToArray();

        private static readonly string[] VisorStateWeaponNames =
            VisorStateNames.Where( ( v, i ) => VisorManager.ValidStatesForWeapon[ VisorStates[ i ] ] ).ToArray();

        private const int FirstColumnWidth  = 65;
        private const int OtherColumnWidths = 70;

        private static readonly float SizeX =
            FirstColumnWidth + VisorStates.Length * ( OtherColumnWidths + 2 );
        private static readonly Vector2 MinSize = new( SizeX, 200 );
        private static readonly Vector2 MaxSize = new( SizeX, 50000 );

        private readonly AutoVisor              _plugin;
        private readonly DalamudPluginInterface _pi;
        private readonly AutoVisorConfiguration _config;

        public bool Visible;

        private readonly List< string > _players;

        private int  _currentPlayer  = 0;
        private int  _currentJob     = 0;
        private bool _setColumnWidth = false;

        public AutoVisorUi( AutoVisor plugin, DalamudPluginInterface pi, AutoVisorConfiguration config )
        {
            _plugin                                       = plugin;
            _pi                                           = pi;
            _config                                       = config;
            _players                                      = _config.States.Select( kvp => kvp.Key ).ToList();
            var idx = Array.IndexOf( VisorStateNames, "Drawn" );
            if( idx < 0 )
                return;
            VisorStateNames[ idx ] = "W. Drawn";
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
            _plugin.VisorManager.ResetState();
        }

        private void DrawEnabledCheckbox()
        {
            var tmp = _config.Enabled;
            if( ImGui.Checkbox( LabelEnabled, ref tmp ) && _config.Enabled != tmp )
            {
                _config.Enabled = tmp;
                if( tmp )
                    _plugin.VisorManager.Activate();
                else
                    _plugin.VisorManager.Deactivate();
                Save();
            }
        }

        private void DrawSettingsHeaders( int which )
        {
            var names = which == 2 ? VisorStateWeaponNames : VisorStateNames;
            ImGui.Columns( names.Length + 1, $"##header_{_currentPlayer}", true );

            if( !_setColumnWidth )
            {
                ImGui.SetColumnWidth( 0, FirstColumnWidth );
                for( var i = 1; i <= VisorStateNames.Length; ++i )
                    ImGui.SetColumnWidth( i, OtherColumnWidths );
            }

            ImGui.NextColumn();
            foreach( var name in names )
            {
                ImGui.Text( name );
                ImGui.NextColumn();
            }
        }

        private void DrawSettingsLine( PlayerConfig settings, int which )
        {
            var jobSettings = settings.PerJob.ElementAt( _currentJob );
            var job         = jobSettings.Key;
            var name        = JobNames[ ( int )jobSettings.Key ];
            var group       = jobSettings.Value;
            var set         = which == 0 ? group.VisorSet : which == 1   ? group.HideHatSet : group.HideWeaponSet;
            var state       = which == 0 ? group.VisorState : which == 1 ? group.HideHatState : group.HideWeaponState;
            var tooltip1 = which switch
            {
                0 => "Enable visor toggle on this state.",
                1 => "Enable headslot toggle on this state.",
                _ => "Enable weapon toggle on this state."
            };
            var tooltip2 = which switch
            {
                0 => "Visor off/on.",
                1 => "Headslot off/on.",
                _ => "Weapon off/on."
            };

            ImGui.Separator();
            ImGui.PushStyleVar( ImGuiStyleVar.ItemSpacing, new Vector2( 2, 0 ) );
            if( job != Job.Default )
            {
                if( ImGui.Button( $"-##0{_currentPlayer}_{_currentJob}_{which}", new Vector2( 20, 23 ) ) )
                {
                    settings.PerJob.Remove( settings.PerJob.ElementAt( _currentJob ).Key );
                    _currentJob = Math.Max( 0, _currentJob - 1 );
                    Save();
                }

                if( ImGui.IsItemHovered() )
                    ImGui.SetTooltip( $"Delete the job specific settings for {name}." );

                ImGui.SameLine();
            }

            ImGui.Text( name );
            ImGui.PopStyleVar();
            ImGui.NextColumn();
            foreach( var v in which == 2 ? VisorStatesWeapon : VisorStates )
            {
                var tmp1 = set.HasFlag( v );
                ImGui.Checkbox( $"##0{which}_{_currentPlayer}_{_currentJob}_{v}", ref tmp1 );
                if( ImGui.IsItemHovered() )
                    ImGui.SetTooltip( tooltip1 );
                if( !tmp1 )
                    ImGui.PushStyleVar( ImGuiStyleVar.Alpha, 0.35f );

                var tmp2 = tmp1 && state.HasFlag( v );
                ImGui.SameLine();
                ImGui.Checkbox( $"##1{which}_{_currentPlayer}_{_currentJob}_{v}", ref tmp2 );
                if( !tmp1 )
                {
                    tmp2 = false;
                    ImGui.PopStyleVar();
                }

                if( ImGui.IsItemHovered() )
                    ImGui.SetTooltip( tooltip2 );


                if( tmp1 != set.HasFlag( v ) || tmp2 != state.HasFlag( v ) )
                {
                    switch( which )
                    {
                    case 0:
                        group.VisorSet   = tmp1 ? set | v : set & ~v;
                        group.VisorState = tmp2 ? state | v : state & ~v;
                        break;
                    case 1:
                        group.HideHatSet   = tmp1 ? set | v : set & ~v;
                        group.HideHatState = tmp2 ? state | v : state & ~v;
                        break;
                    default:
                        group.HideWeaponSet   = tmp1 ? set | v : set & ~v;
                        group.HideWeaponState = tmp2 ? state | v : state & ~v;
                        break;
                    }

                    settings.PerJob[ job ] = group;
                    Save();
                }

                ImGui.NextColumn();
            }
        }

        private void DrawAddJobSelector( PlayerConfig settings )
        {
            if( settings.PerJob.Count == JobNames.Length )
                return;

            var availableJobsAndIndices = JobNames.Select( ( j, i ) => ( j, i ) ).Where( p => !settings.PerJob.ContainsKey( Jobs[ p.i ] ) );

            if( !ImGui.BeginCombo( $"Add Job##{_currentPlayer}", "", ImGuiComboFlags.NoPreview ) )
                return;

            foreach( var (job, index) in availableJobsAndIndices )
            {
                if( ImGui.Selectable( $"{job}##{_currentPlayer}", false ) )
                {
                    settings.PerJob.Add( Jobs[ index ], VisorChangeGroup.Empty );
                    Save();
                }
            }

            ImGui.EndCombo();
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

            if( ImGui.IsItemHovered() )
                ImGui.SetTooltip( $"Delete all settings for the character {name}." );

            if( name != ( _pi.ClientState.LocalPlayer?.Name ?? "" ) )
            {
                ImGui.SameLine();
                if( ImGui.Button( $"Duplicate##{name}" ) )
                    AddPlayer( _config.States[ name ] );
                if( ImGui.IsItemHovered() )
                    ImGui.SetTooltip( $"Duplicate the settings for this character to your current character." );
            }

            ImGui.SameLine();
            DrawAddJobSelector( playerConfig );

            ImGui.Dummy( new Vector2( 0, 5 ) );
            if( ImGui.TreeNode( $"Visor State##{name}" ) )
            {
                DrawSettingsHeaders( 0 );

                for( _currentJob = 0; _currentJob < playerConfig.PerJob.Count; ++_currentJob )
                    DrawSettingsLine( playerConfig, 0 );

                ImGui.Columns( 1 );
                ImGui.TreePop();
            }

            if( ImGui.TreeNode( $"Headslot State##{name}" ) )
            {
                DrawSettingsHeaders( 1 );

                for( _currentJob = 0; _currentJob < playerConfig.PerJob.Count; ++_currentJob )
                    DrawSettingsLine( playerConfig, 1 );

                ImGui.Columns( 1 );
                ImGui.TreePop();
            }

            if( ImGui.TreeNode( $"Weapon State##{name}" ) )
            {
                DrawSettingsHeaders( 2 );

                for( _currentJob = 0; _currentJob < playerConfig.PerJob.Count; ++_currentJob )
                    DrawSettingsLine( playerConfig, 2 );

                ImGui.Columns( 1 );
                ImGui.TreePop();
            }
        }

        private static void DrawHelp()
        {
            if( !ImGui.CollapsingHeader( "Help", ImGuiTreeNodeFlags.DefaultOpen ) )
                return;

            ImGui.TextWrapped( "AutoVisor allows you to automatically use /visor, /displayhead or /displayarms on certain conditions. " +
                "The configuration is character-name and job-specific, with a default job that triggers if no specific job is active. " +
                "The first checkbox per column activates automatic changing for the specific condition, the second indicates to which state the setting should be changed.\n" +
                "Precedences are:\n" +
                "\t1. Fishing\n" +
                "\t2. Gathering ~ Crafting\n" +
                "\t3. In Flight ~ Diving\n" +
                "\t4. Mounted ~ Swimming ~ Wearing Fashion Accessories\n" +
                "\t5. Casting\n" +
                "\t6. In Combat\n" +
                "\t7. Weapon Drawn\n" +
                "\t8. In Duty\n" +
                "\t9. Normal." );
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

            _setColumnWidth = true;

            ImGui.End();
        }
    }
}
