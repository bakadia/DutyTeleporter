using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DutyTeleporter.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin)
        : base("DutyTeleporter 设置###DutyTeleporterConfigWindow")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse
              | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(280, 100);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // 属性不能直接 ref，先取局部变量。
        var value = configuration.SomePropertyToBeSavedAndWithADefault;
        if (ImGui.Checkbox("某个开关", ref value))
        {
            configuration.SomePropertyToBeSavedAndWithADefault = value;
            // 想省掉「保存并关闭」按钮，就改一次存一次。
            configuration.Save();
        }
    }
}
