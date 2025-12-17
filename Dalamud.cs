using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace AutoVisor;

public class Dalamud
{
    public static void Initialize(IDalamudPluginInterface pluginInterface)
        => pluginInterface.Create<Dalamud>();

        // @formatter:off
        [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static ICommandManager         Commands        { get; private set; } = null!;
        [PluginService] public static IClientState            ClientState     { get; private set; } = null!;
        [PluginService] public static IPlayerState            PlayerState     { get; private set; } = null!;
        [PluginService] public static IChatGui                Chat            { get; private set; } = null!;
        [PluginService] public static IFramework              Framework       { get; private set; } = null!;
        [PluginService] public static ICondition              Conditions      { get; private set; } = null!;
        [PluginService] public static IObjectTable            Objects { get; private set; } = null!;
        [PluginService] public static IGameInteropProvider    Interop         { get; private set; } = null!;
        [PluginService] public static IPluginLog              Log             { get; private set; } = null!;
    // @formatter:on
}
