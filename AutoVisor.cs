using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using AutoVisor.Classes;
using AutoVisor.GUI;
using AutoVisor.Managers;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using CommandManager = AutoVisor.Managers.CommandManager;

namespace AutoVisor
{
    public class AutoVisor : IDalamudPlugin
    {
        public string Name => "AutoVisor";

        private DalamudPluginInterface _pluginInterface;
        private AutoVisorConfiguration _configuration;
        private CommandManager         _commandManager;
        private AutoVisorUi            _ui;
        public  VisorManager           VisorManager;

        public void Initialize( DalamudPluginInterface pluginInterface )
        {
            _pluginInterface = pluginInterface;
            _commandManager =
                new CommandManager( pluginInterface, "AutoVisor", Serilog.Events.LogEventLevel.Verbose );
            _configuration = pluginInterface.GetPluginConfig() as AutoVisorConfiguration ??
                new AutoVisorConfiguration();
            VisorManager = new VisorManager( _pluginInterface, _configuration, _commandManager );
            _ui           = new AutoVisorUi( this, _pluginInterface, _configuration );

            if( _configuration.Enabled )
                VisorManager.Activate();

            _pluginInterface.CommandManager.AddHandler( "/autovisor", new CommandInfo( OnAutoVisor )
            {
                HelpMessage = "Use to open the graphical interface.",
                ShowInHelp  = true
            } );

            _pluginInterface.UiBuilder.OnBuildUi      += _ui.Draw;
            _pluginInterface.UiBuilder.OnOpenConfigUi += OnConfigCommandHandler;
        }

        public void Dispose()
        {
            VisorManager.Dispose();
            _pluginInterface.SavePluginConfig( _configuration );
            _pluginInterface.CommandManager.RemoveHandler( "/autovisor" );
            _pluginInterface.Dispose();
        }

        private void OnAutoVisor( string command, string _ )
            => _ui.Visible = !_ui.Visible;

        private void OnConfigCommandHandler( object _, object _2 ) { _ui.Visible = true; }
    }
}
