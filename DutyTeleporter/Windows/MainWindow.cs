using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace DutyTeleporter.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    // ### 后面是固定 ID：标题可以变，ImGui 内部靠这个 ID 认窗口。
    public MainWindow(Plugin plugin)
        : base("DutyTeleporter###DutyTeleporterMainWindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.Button("打开设置"))
        {
            plugin.ToggleConfigUi();
        }

        ImGui.SameLine();
        if (ImGui.Button("实验窗口"))
        {
            plugin.ToggleExperimentUi();
        }

        ImGui.Separator();

        var player = Plugin.PlayerState;
        if (!player.IsLoaded)
        {
            ImGui.Text("尚未登录角色。");
            return;
        }

        // 硬编码的像素值都要乘 GlobalScale，否则玩家 HUD 缩放不是 100% 时会错位。
        ImGui.AlignTextToFramePadding();
        ImGui.Text("当前职业：");
        ImGui.SameLine(120 * ImGuiHelpers.GlobalScale);

        var jobIconId = 62100 + player.ClassJob.RowId;
        var iconTexture = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(jobIconId)).GetWrapOrEmpty();
        ImGui.Image(iconTexture.Handle, new Vector2(28, 28) * ImGuiHelpers.GlobalScale);

        ImGui.SameLine();
        ImGui.Text(player.ClassJob.Value.Abbreviation.ToString());
        ImGui.SameLine();
        ImGui.Text($"Lv.{player.Level}");

        var territoryId = Plugin.ClientState.TerritoryType;
        if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
        {
            ImGui.Text("当前区域：");
            ImGui.SameLine(120 * ImGuiHelpers.GlobalScale);
            ImGui.Text(territory.PlaceName.Value.Name.ToString());
        }
        else
        {
            ImGui.Text("当前区域未知。");
        }

        ImGui.Spacing();

        using (var child = ImRaii.Child("PlayerInfoChild", Vector2.Zero, true))
        {
            if (child.Success)
            {
                ImGui.Text("在这里写你的功能。");
                ImGuiHelpers.ScaledDummy(10f);
                ImGui.Text($"配置项当前值：{plugin.Configuration.SomePropertyToBeSavedAndWithADefault}");
            }
        }
    }
}
