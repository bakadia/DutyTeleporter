using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DutyTeleporter.Windows;

namespace DutyTeleporter;

public sealed class Plugin : IDalamudPlugin
{
    // [PluginService] 由 Dalamud 在构造前注入，不要在构造函数里自己 new。
    // 完整可用服务列表：https://goatcorp.github.io/Dalamud/api/Dalamud.Plugin.Services.html
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/dtp";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("DutyTeleporter");

    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "打开 DutyTeleporter 主窗口",
        });

        // 把窗口交给 WindowSystem 统一绘制，这样能接入原生的窗口排序、固定、透明度控制。
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // 让插件安装器里的齿轮/主页按钮能打开我们的窗口。
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information($"[{PluginInterface.Manifest.Name}] 已加载");
    }

    // 游戏内 /xlplugins 卸载插件时会调用，必须把所有事件订阅和命令反注册干净，
    // 否则热重载时会重复订阅，表现为窗口画两遍、命令触发两次。
    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args) => MainWindow.Toggle();

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
