using System.Linq;
using System.Reflection;
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
        public string Name
            => "AutoVisor";

        public static string Version = "";

        private DalamudPluginInterface? _pluginInterface;
        private AutoVisorConfiguration? _configuration;
        private CommandManager?         _commandManager;
        private AutoVisorUi?            _ui;
        public  VisorManager?           VisorManager;


        private bool VerifySettingIntegrity()
        {
            var changes = false;
            foreach (var player in _configuration!.States.Values)
            {
                if (player.PerJob.Any(kvp => kvp.Key > Job.DNC))
                {
                    player.PerJob = player.PerJob.Where(kvp => kvp.Key <= Job.DNC).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    changes       = true;
                }

                foreach (var values in player.PerJob.Values)
                    changes |= values.CheckIntegrity();
            }

            return changes;
        }

        private void CheckSettings()
        {
            var update = UpdateConfigV1To2();
            update |= VerifySettingIntegrity();

            if (update)
                _pluginInterface!.SavePluginConfig(_configuration);
        }

        private bool UpdateConfigV1To2()
        {
            if (_configuration!.Version == 1)
            {
                foreach (var player in _configuration!.States.Values)
                    player.PerJob = player.PerJob.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ResetPoses());

                _configuration.Version = 2;
                return true;
            }

            return false;
        }

        private const string PPoseHelp = "Use with [Stand, Weapon, Sit, GroundSit, Doze] [#] to set specific pose.";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            _pluginInterface = pluginInterface;
            Version          = Assembly.GetExecutingAssembly()?.GetName().Version.ToString() ?? "";
            _commandManager  = new CommandManager(pluginInterface);
            _configuration   = pluginInterface.GetPluginConfig() as AutoVisorConfiguration ?? new AutoVisorConfiguration();
            CheckSettings();
            VisorManager     = new VisorManager(_pluginInterface, _configuration, _commandManager);
            _ui              = new AutoVisorUi(this, _pluginInterface, _configuration);

            if (_configuration.Enabled)
                VisorManager.Activate();

            _pluginInterface!.CommandManager.AddHandler("/autovisor", new CommandInfo(OnAutoVisor)
            {
                HelpMessage = "Use to open the graphical interface.",
                ShowInHelp  = true,
            });

            _pluginInterface!.CommandManager.AddHandler("/ppose", new CommandInfo(OnPPose)
            {
                HelpMessage = PPoseHelp,
                ShowInHelp  = true,
            });

            _pluginInterface!.UiBuilder.OnBuildUi     += _ui.Draw;
            _pluginInterface.UiBuilder.OnOpenConfigUi += OnConfigCommandHandler;
        }

        public void Dispose()
        {
            VisorManager?.Dispose();
            _pluginInterface!.SavePluginConfig(_configuration);
            _pluginInterface!.CommandManager.RemoveHandler("/autovisor");
            _pluginInterface!.CommandManager.RemoveHandler("/ppose");
            _pluginInterface!.Dispose();
        }

        private void OnAutoVisor(string command, string _)
            => _ui!.Visible = !_ui.Visible;

        private void OnConfigCommandHandler(object _, object _2)
        {
            _ui!.Visible = true;
        }

        public readonly string[] PoseTypes = new string[]
        {
            "Stand",
            "Weapon",
            "Sit",
            "GroundSit",
            "Doze",
        };

        private void OnPPose(string command, string arguments)
        {
            var chat = _pluginInterface!.Framework.Gui.Chat;
            var args = arguments.Split();
            if (args.Length < 2)
            {
                chat.Print(PPoseHelp);
                return;
            }

            int  whichPoseType;
            switch (args[0].ToLowerInvariant())
            {
                case "stand":     whichPoseType = 0;
                    break;
                case "weapon":    whichPoseType = 1;
                    break;
                case "sit":       whichPoseType = 2;
                    break;
                case "groundsit": whichPoseType = 3;
                    break;
                case "doze":      whichPoseType = 4;
                    break;
                default:
                    if (!int.TryParse(args[0], out whichPoseType) || whichPoseType < 0 || whichPoseType > 4)
                    {
                        chat.Print(PPoseHelp);
                        return;
                    }

                    break;
            }

            if (!byte.TryParse(args[1], out var whichPose))
            {
                chat.Print(PPoseHelp);
                return;
            }
            if (whichPose == 0 || whichPose > CPoseManager.NumPoses[whichPoseType])
            {
                chat.PrintError($"Pose {whichPose} for {PoseTypes[whichPoseType]} does not exist, only {CPoseManager.NumPoses[whichPoseType]} poses are supported.");
                return;
            }

            VisorManager!.CPoseManager.SetPose(whichPoseType, (byte) (whichPose - 1));
            chat.Print($"Set {PoseTypes[whichPoseType]} pose to {whichPose}.");
        }
    }
}
