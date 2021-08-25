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

        public static    AutoVisorConfiguration Config = null!;
        public readonly  VisorManager           VisorManager;
        private readonly CommandManager         _commandManager;
        private readonly AutoVisorUi            _ui;

        private bool VerifySettingIntegrity()
        {
            var changes = false;
            foreach (var player in Config!.States.Values)
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
                Config.Save();
        }

        private bool UpdateConfigV1To2()
        {
            if (Config.Version == 1)
            {
                foreach (var player in Config.States.Values)
                    player.PerJob = player.PerJob.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ResetPoses());

                Config.Version = 2;
                return true;
            }

            return false;
        }

        private const string PPoseHelp = "Use with [Stand, Weapon, Sit, GroundSit, Doze] [#] to set specific pose.";

        public AutoVisor(DalamudPluginInterface pluginInterface)
        {
            Dalamud.Initialize(pluginInterface);
            Version         = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
            _commandManager = new CommandManager(Dalamud.SigScanner);
            Config          = AutoVisorConfiguration.Load();
            CheckSettings();
            VisorManager = new VisorManager(_commandManager);
            _ui          = new AutoVisorUi(this);

            if (Config.Enabled)
                VisorManager.Activate();

            Dalamud.Commands.AddHandler("/autovisor", new CommandInfo(OnAutoVisor)
            {
                HelpMessage = "Use to open the graphical interface.",
                ShowInHelp  = true,
            });

            Dalamud.Commands.AddHandler("/ppose", new CommandInfo(OnPPose)
            {
                HelpMessage = PPoseHelp,
                ShowInHelp  = true,
            });

            Dalamud.PluginInterface.UiBuilder.Draw         += _ui.Draw;
            Dalamud.PluginInterface.UiBuilder.OpenConfigUi += OnConfigCommandHandler;
        }

        public void Dispose()
        {
            VisorManager.Dispose();
            Dalamud.Commands.RemoveHandler("/autovisor");
            Dalamud.Commands.RemoveHandler("/ppose");
        }

        private void OnAutoVisor(string command, string _)
            => _ui!.Visible = !_ui.Visible;

        private void OnConfigCommandHandler()
            => _ui!.Visible = true;

        public readonly string[] PoseTypes =
        {
            "Stand",
            "Weapon",
            "Sit",
            "GroundSit",
            "Doze",
        };

        private void OnPPose(string command, string arguments)
        {
            var args = arguments.Split();
            if (args.Length < 2)
            {
                Dalamud.Chat.Print(PPoseHelp);
                return;
            }

            int whichPoseType;
            switch (args[0].ToLowerInvariant())
            {
                case "stand":
                    whichPoseType = 0;
                    break;
                case "weapon":
                    whichPoseType = 1;
                    break;
                case "sit":
                    whichPoseType = 2;
                    break;
                case "groundsit":
                    whichPoseType = 3;
                    break;
                case "doze":
                    whichPoseType = 4;
                    break;
                default:
                    if (!int.TryParse(args[0], out whichPoseType) || whichPoseType < 0 || whichPoseType > 4)
                    {
                        Dalamud.Chat.Print(PPoseHelp);
                        return;
                    }

                    break;
            }

            if (!byte.TryParse(args[1], out var whichPose))
            {
                Dalamud.Chat.Print(PPoseHelp);
                return;
            }

            if (whichPose == 0 || whichPose > CPoseManager.NumPoses[whichPoseType])
            {
                Dalamud.Chat.PrintError(
                    $"Pose {whichPose} for {PoseTypes[whichPoseType]} does not exist, only {CPoseManager.NumPoses[whichPoseType]} poses are supported.");
                return;
            }

            VisorManager!.CPoseManager.SetPose(whichPoseType, (byte) (whichPose - 1));
            Dalamud.Chat.Print($"Set {PoseTypes[whichPoseType]} pose to {whichPose}.");
        }
    }
}
