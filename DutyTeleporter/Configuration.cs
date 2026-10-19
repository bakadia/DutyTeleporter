using System;
using Dalamud.Configuration;

namespace DutyTeleporter;

[Serializable]
public class Configuration : IPluginConfiguration
{
    // 改了配置结构就 +1，方便以后写迁移逻辑。
    public int Version { get; set; } = 0;

    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
