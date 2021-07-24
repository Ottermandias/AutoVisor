using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Remoting.Messaging;
using AutoVisor.Classes;
using AutoVisor.Managers;
using Dalamud.Plugin;
using ImGuiNET;

namespace AutoVisor.GUI
{
    public class AutoVisorUi
    {
        private const string PluginName   = "AutoVisor";
        private const string LabelEnabled = "Enable AutoVisor";

        private static readonly Job[]    Jobs     = Enum.GetValues(typeof(Job)).Cast<Job>().ToArray();
        private static readonly string[] JobNames = Enum.GetNames(typeof(Job));

        private static readonly VisorChangeStates[] VisorStates =
            Enum.GetValues(typeof(VisorChangeStates)).Cast<VisorChangeStates>().ToArray();

        private static readonly string[] VisorStateNames = Enum.GetNames(typeof(VisorChangeStates));

        private static readonly string[] PoseOptions =
        {
            "Default",
            "Unchanged",
            "Pose 1",
            "Pose 2",
            "Pose 3",
            "Pose 4",
            "Pose 5",
            "Pose 6",
            "Pose 7",
        };

        private static readonly VisorChangeStates[] VisorStatesWeapon =
            VisorStates.Where(v => VisorManager.ValidStatesForWeapon[v]).ToArray();

        private static readonly string[] VisorStateWeaponNames =
            VisorStateNames.Where((v, i) => VisorManager.ValidStatesForWeapon[VisorStates[i]]).ToArray();

        private readonly string                 _configHeader;
        private readonly AutoVisor              _plugin;
        private readonly DalamudPluginInterface _pi;
        private readonly AutoVisorConfiguration _config;

        public bool Visible;

        private readonly List<string> _players;

        private int _currentPlayer = 0;
        private int _currentJob    = 0;

        private Vector2 _horizontalSpace = Vector2.Zero;

        public AutoVisorUi(AutoVisor plugin, DalamudPluginInterface pi, AutoVisorConfiguration config)
        {
            _plugin       = plugin;
            _configHeader = AutoVisor.Version.Length > 0 ? $"{PluginName} v{AutoVisor.Version}" : PluginName;
            _pi           = pi;
            _config       = config;
            _players      = _config.States.Select(kvp => kvp.Key).ToList();
            var idx = Array.IndexOf(VisorStateNames, "Drawn");
            if (idx < 0)
                return;

            VisorStateNames[idx] = "W. Drawn";
        }

        private bool AddPlayer(PlayerConfig config)
        {
            var name = _pi.ClientState.LocalPlayer?.Name ?? "";
            if (name.Length == 0 || _config.States.ContainsKey(name))
                return false;

            _players.Add(name);
            _config.States[name] = config.Clone();
            Save();
            return true;
        }

        private bool AddPlayer()
            => AddPlayer(new PlayerConfig());

        private void RemovePlayer(string name)
        {
            _players.Remove(name);
            _config.States.Remove(name);
            Save();
        }

        private void Save()
        {
            _pi.SavePluginConfig(_config);
            _plugin.VisorManager!.ResetState();
        }

        private void DrawEnabledCheckbox()
        {
            var tmp = _config.Enabled;
            if (!ImGui.Checkbox(LabelEnabled, ref tmp) || _config.Enabled == tmp)
                return;

            _config.Enabled = tmp;
            if (tmp)
                _plugin.VisorManager!.Activate();
            else
                _plugin.VisorManager!.Deactivate();
            Save();
        }

        private void DrawWaitFrameInput()
        {
            var tmp = _config.WaitFrames;
            ImGui.SetNextItemWidth(50);
            if (ImGui.InputInt("Wait Frames", ref tmp, 0, 0) && _config.WaitFrames != tmp)
            {
                _config.WaitFrames = tmp;
                Save();
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    "The number of frames to wait after a job change or visor toggle before checking state again.\n"
                  + "Keep this as is if you are not sure what it does.\n"
                  + "Otherwise, set it as low as possible and as high as necessary.");
        }

        private ImGuiRaii? DrawTableHeader(int type)
        {
            const ImGuiTableFlags flags = ImGuiTableFlags.Hideable
              | ImGuiTableFlags.BordersOuter
              | ImGuiTableFlags.BordersInner
              | ImGuiTableFlags.SizingFixedSame;

            var list = type switch
            {
                2 => VisorStateWeaponNames,
                3 => CPoseManager.PoseNames,
                _ => VisorStateNames,
            };

            var imgui = new ImGuiRaii();
            if (!imgui.Begin(() => ImGui.BeginTable($"##table_{type}_{_currentPlayer}", list.Length + 1, flags), ImGui.EndTable))
                return null;

            ImGui.TableSetupColumn($"Job##empty_{type}_{_currentPlayer}", ImGuiTableColumnFlags.NoHide);
            foreach (var name in list)
                ImGui.TableSetupColumn(name);

            ImGui.TableHeadersRow();
            return imgui;
        }

        private static readonly string[] PoseTooltips = new string[]
        {
            "Change your default pose when standing upright.\nDefault sets the pose from login with that character.\nUnchanged does not change the pose at all.",
            "Change your default pose when standing with your weapon drawn.\nDefault sets the pose from login with that character.\nUnchanged does not change the pose at all.",
            "Change your default pose when sitting on the ground.\nDefault sets the pose from login with that character.\nUnchanged does not change the pose at all.",
            "Change your default pose when sitting on an object.\nDefault sets the pose from login with that character.\nUnchanged does not change the pose at all.",
            "Change your default pose when dozing in a bed.\nDefault sets the pose from login with that character.\nUnchanged does not change the pose at all.",
        };

        public void DrawPoseTableContent(PlayerConfig settings, Job job, VisorChangeGroup jobSettings)
        {
            for (var i = 0; i < CPoseManager.PoseNames.Length; ++i)
            {
                ImGui.TableNextColumn();
                int tmp = i switch
                {
                    0 => jobSettings.StandingPose,
                    1 => jobSettings.WeaponDrawnPose,
                    2 => jobSettings.SittingPose,
                    3 => jobSettings.GroundSittingPose,
                    4 => jobSettings.DozingPose,
                    _ => throw new NotImplementedException("There are no more Cpose targets."),
                };
                tmp = tmp switch
                {
                    CPoseManager.DefaultPose   => 0,
                    CPoseManager.UnchangedPose => 1,
                    _                          => tmp + 2,
                };
                var copy = tmp;

                ImGui.SetNextItemWidth(-1);
                if (ImGui.Combo($"##03_{_currentPlayer}_{_currentJob}_pose_{i}", ref tmp, PoseOptions, CPoseManager.NumPoses[i] + 2)
                 && tmp != copy)
                {
                    var value = (byte) (tmp switch
                    {
                        0 => CPoseManager.DefaultPose,
                        1 => CPoseManager.UnchangedPose,
                        _ => tmp - 2,
                    });

                    switch (i)
                    {
                        case 0:
                            jobSettings.StandingPose = value;
                            break;
                        case 1:
                            jobSettings.WeaponDrawnPose = value;
                            break;
                        case 2:
                            jobSettings.SittingPose = value;
                            break;
                        case 3:
                            jobSettings.GroundSittingPose = value;
                            break;
                        case 4:
                            jobSettings.DozingPose = value;
                            break;
                    }

                    settings.PerJob[job] = jobSettings;
                    Save();
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(PoseTooltips[i]);
            }
        }

        private void DrawTableContent(PlayerConfig settings, int type)
        {
            var jobSettings = settings.PerJob.ElementAt(_currentJob);
            var job         = jobSettings.Key;
            var name        = JobNames[(int) jobSettings.Key];
            var group       = jobSettings.Value;

            var (set, state, tooltip1, tooltip2) = type switch
            {
                0 => (group.VisorSet, group.VisorState, "Enable visor toggle on this state.", "Visor off/on."),
                1 => (group.HideHatSet, group.HideHatState, "Enable headslot toggle on this state.", "Headslot off/on."),
                _ => (group.HideWeaponSet, group.HideWeaponState, "Enable weapon toggle on this state.", "Weapon off / on."),
            };
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            using var imgui = new ImGuiRaii()
                .PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(2 * ImGui.GetIO().FontGlobalScale, 0));
            if (job != Job.Default)
            {
                if (ImGui.Button($"−##0{_currentPlayer}_{_currentJob}_{type}", new Vector2(20, 20) * ImGui.GetIO().FontGlobalScale))
                {
                    settings.PerJob.Remove(settings.PerJob.ElementAt(_currentJob).Key);
                    _currentJob = Math.Max(0, _currentJob - 1);
                    Save();
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Delete the job specific settings for {name}.");

                ImGui.SameLine();
            }
            else
            {
                ImGui.AlignTextToFramePadding();
            }

            ImGui.Text(name);
            imgui.PopStyles();

            if (type == 3)
            {
                DrawPoseTableContent(settings, job, group);
                return;
            }

            foreach (var v in type == 2 ? VisorStatesWeapon : VisorStates)
            {
                ImGui.TableNextColumn();
                var tmp1 = set.HasFlag(v);
                ImGui.Checkbox($"##0{type}_{_currentPlayer}_{_currentJob}_{v}", ref tmp1);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(tooltip1);
                if (!tmp1)
                    imgui.PushStyle(ImGuiStyleVar.Alpha, 0.35f);

                var tmp2 = tmp1 && state.HasFlag(v);
                ImGui.SameLine();
                ImGui.Checkbox($"##1{type}_{_currentPlayer}_{_currentJob}_{v}", ref tmp2);
                if (!tmp1)
                {
                    tmp2 = false;
                    imgui.PopStyles();
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(tooltip2);


                if (tmp1 != set.HasFlag(v) || tmp2 != state.HasFlag(v))
                {
                    switch (type)
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

                    settings.PerJob[job] = group;
                    Save();
                }
            }
        }

        private void DrawTable(PlayerConfig settings, int type)
        {
            using var table = DrawTableHeader(type);
            if (table == null)
                return;

            for (_currentJob = 0; _currentJob < settings.PerJob.Count; ++_currentJob)
                DrawTableContent(settings, type);
        }

        private void DrawAddJobSelector(PlayerConfig settings)
        {
            if (settings.PerJob.Count == JobNames.Length)
                return;

            var availableJobsAndIndices = JobNames.Select((j, i) => (j, i)).Where(p => !settings.PerJob.ContainsKey(Jobs[p.i]));

            using var combo = new ImGuiRaii();

            if (!combo.Begin(() => ImGui.BeginCombo($"Add Job##{_currentPlayer}", "", ImGuiComboFlags.NoPreview), ImGui.EndCombo))
                return;

            foreach (var (job, index) in availableJobsAndIndices)
            {
                if (!ImGui.Selectable($"{job}##{_currentPlayer}", false))
                    continue;

                settings.PerJob.Add(Jobs[index], VisorChangeGroup.Empty);
                Save();
            }
        }

        private void DrawPlayerGroup()
        {
            var name = _players.ElementAt(_currentPlayer);

            if (!ImGui.CollapsingHeader(name))
                return;

            ImGui.Dummy(_horizontalSpace);

            var playerConfig = _config.States[name];
            var tmp          = playerConfig.Enabled;
            if (ImGui.Checkbox($"Enabled##{name}", ref tmp) && tmp != playerConfig.Enabled)
            {
                playerConfig.Enabled = tmp;
                Save();
            }

            ImGui.SameLine();
            if (ImGui.Button($"Delete##{name}"))
            {
                RemovePlayer(name);
                --_currentPlayer;
                ImGui.Columns(1);
                return;
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Delete all settings for the character {name}.");

            if (name != (_pi.ClientState.LocalPlayer?.Name ?? ""))
            {
                ImGui.SameLine();
                if (ImGui.Button($"Duplicate##{name}"))
                    AddPlayer(_config.States[name]);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Duplicate the settings for this character to your current character.");
            }

            ImGui.SameLine();
            DrawAddJobSelector(playerConfig);

            ImGui.Dummy(_horizontalSpace);
            var cursor = ImGui.GetCursorPosX();
            if (ImGui.TreeNode($"Visor State##{name}"))
            {
                ImGui.SetCursorPosX(cursor);
                DrawTable(playerConfig, 0);
                ImGui.TreePop();
            }

            if (ImGui.TreeNode($"Headslot State##{name}"))
            {
                ImGui.SetCursorPosX(cursor);
                DrawTable(playerConfig, 1);
                ImGui.TreePop();
            }

            if (ImGui.TreeNode($"Weapon State##{name}"))
            {
                ImGui.SetCursorPosX(cursor);
                DrawTable(playerConfig, 2);
                ImGui.TreePop();
            }

            if (ImGui.TreeNode($"Poses##{name}"))
            {
                ImGui.SetCursorPosX(cursor);
                DrawTable(playerConfig, 3);
                ImGui.TreePop();
            }
        }

        private static void DrawHelp()
        {
            if (!ImGui.CollapsingHeader("Help", ImGuiTreeNodeFlags.DefaultOpen))
                return;

            ImGui.TextWrapped("AutoVisor allows you to automatically use /visor, /displayhead or /displayarms on certain conditions. "
              + "The configuration is character-name and job-specific, with a default job that triggers if no specific job is active. "
              + "The first checkbox per column activates automatic changing for the specific condition, the second indicates to which state the setting should be changed.\n"
              + "Precedences are:\n"
              + "\t1. Fishing\n"
              + "\t2. Gathering ~ Crafting\n"
              + "\t3. In Flight ~ Diving\n"
              + "\t4. Mounted ~ Swimming ~ Wearing Fashion Accessories\n"
              + "\t5. Casting\n"
              + "\t6. In Combat\n"
              + "\t7. Weapon Drawn\n"
              + "\t8. In Duty\n"
              + "\t9. Normal.");
        }

        private void DrawPlayerAdd()
        {
            var name = _pi.ClientState.LocalPlayer?.Name ?? "";
            if (name.Length == 0 || _config.States.ContainsKey(name))
                return;

            if (ImGui.Button("Add settings for this character..."))
                AddPlayer();
        }

        public void Draw()
        {
            if (!Visible)
                return;

            ImGui.SetNextWindowSizeConstraints(
                new Vector2(980, 500) * ImGui.GetIO().FontGlobalScale + ImGui.GetStyle().ScrollbarSize * Vector2.UnitX,
                new Vector2(4000, 4000));
            if (!ImGui.Begin(_configHeader, ref Visible))
                return;

            try
            {
                DrawEnabledCheckbox();
                ImGui.SameLine();
                DrawWaitFrameInput();
                ImGui.SameLine();
                DrawPlayerAdd();

                _horizontalSpace = new Vector2(0, 5 * ImGui.GetIO().FontGlobalScale);

                ImGui.Dummy(_horizontalSpace * 2);

                DrawHelp();
                ImGui.Dummy(_horizontalSpace * 2);
                for (_currentPlayer = 0; _currentPlayer < _players.Count; ++_currentPlayer)
                {
                    DrawPlayerGroup();
                    ImGui.Dummy(_horizontalSpace);
                }
            }
            finally
            {
                ImGui.End();
            }
        }
    }
}
