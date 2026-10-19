using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace DutyTeleporter.Windows;

/// <summary>
/// 临时实验窗口：验证服务端是「暂时隐藏」了非四人副本的自由探索，还是根本没做这个场景。
/// 协议与结果判读见 docs/experiment-explorer-mode.md。
/// 实验有结论后，这个文件和 Plugin.cs 里对它的引用可以整个删掉。
/// </summary>
public unsafe class ExperimentWindow : Window, IDisposable
{
    /// <summary>是否每帧强制把 IsExplorerMode 置为 true。</summary>
    public bool ForceExplorerMode { get; set; }

    public ExperimentWindow()
        : base("自由探索实验###DutyTeleporterExperiment")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 250),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose() { }

    /// <summary>由 Plugin 的 Framework.Update 每帧调用，不依赖窗口是否可见。</summary>
    public void Tick()
    {
        if (!ForceExplorerMode)
            return;

        var finder = ContentsFinder.Instance();
        if (finder == null)
            return;

        // 在排本器里选中副本时，客户端可能按 AllowExplorerMode 把这一位重置回去，
        // 所以每帧重写一次，确保真正送出去的请求里它是 true。
        finder->IsExplorerMode = true;
    }

    public override void Draw()
    {
        var finder = ContentsFinder.Instance();
        var current = finder != null && finder->IsExplorerMode;

        ImGui.TextWrapped("目的：判断服务端是「暂时隐藏」了非四人副本的自由探索，还是根本没做这个场景。");
        ImGui.TextWrapped("协议与结果判读见 docs/experiment-explorer-mode.md。务必先跑对照组，否则结论无意义。");
        ImGui.Separator();

        var force = ForceExplorerMode;
        if (ImGui.Checkbox("每帧强制 IsExplorerMode = true", ref force))
            ForceExplorerMode = force;

        ImGui.SameLine();
        ImGui.Text($"实际值：{(current ? "true" : "false")}");

        ImGui.Separator();
        ImGui.TextWrapped("勾上之后，自己在游戏里打开排本器，选中目标副本，点申请。");
        ImGui.TextWrapped("本窗口刻意不代劳排队——让排队走游戏原生的完整流程，避免引入额外变量。");
        ImGui.Spacing();
        ImGui.TextWrapped("对照组：沙斯塔夏溶洞（rowId 4，已知开放自由探索）");
        ImGui.TextWrapped("实验组 A：伊弗利特歼灭战（rowId 59，讨伐战）");
        ImGui.TextWrapped("实验组 B：巴哈姆特零式大迷宫 入侵之章1（rowId 103，零式）");
        ImGui.TextWrapped("实验组 C：水晶塔 古代人迷宫（rowId 92，24 人）");
    }
}
