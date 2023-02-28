﻿using Dalamud.Interface.Windowing;
using RankSSpawnHelper.Ui.Window;
using RankSSpawnHelper.UI.Window;

namespace RankSSpawnHelper.Ui
{
    internal class Windows
    {
        public CounterWindow CounterWindow;
        public HuntMapWindow HuntMapWindow;
        public ConfigWindow PluginWindow;
        public WeeEaWindow WeeEaWindow;

        public Windows(ref WindowSystem windowSystem)
        {
            PluginWindow  = new ConfigWindow();
            CounterWindow = new CounterWindow();
            WeeEaWindow   = new WeeEaWindow();
            HuntMapWindow = new HuntMapWindow();

            windowSystem.AddWindow(PluginWindow);
            windowSystem.AddWindow(CounterWindow);
            windowSystem.AddWindow(WeeEaWindow);
            windowSystem.AddWindow(HuntMapWindow);
        }
    }
}