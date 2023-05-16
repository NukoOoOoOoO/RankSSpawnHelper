﻿using System;
using System.Diagnostics;
using Dalamud.Logging;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace RankSSpawnHelper.Managers.DataManagers
{
    internal class Player
    {
        private readonly ExcelSheet<TerritoryType> _terr;

        public Player()
        {
            _terr = DalamudApi.DataManager.GetExcelSheet<TerritoryType>();
        }

        public string GetCurrentTerritory()
        {
            try
            {
                var instanceNumber = GetCurrentInstance();

                return DalamudApi.ClientState.LocalPlayer?.CurrentWorld.GameData?.Name + "@" +
                       _terr.GetRow(DalamudApi.ClientState.TerritoryType)?.PlaceName.Value?.Name.ToDalamudString().TextValue +
                       "@" + instanceNumber;
            }
            catch (Exception e)
            {
                PluginLog.Error(e, $"Exception from Managers::Data::GetCurrentInstance(). Last CallStack:{new StackFrame(1).GetMethod()?.Name}");
                return string.Empty;
            }
        }

        public unsafe int GetCurrentInstance()
        {
            try
            {
                return UIState.Instance()->AreaInstance.Instance;
            }
            catch
            {
                return -1;
            }
        }

        public string GetLocalPlayerName()
        {
            if (DalamudApi.ClientState.LocalPlayer == null)
                return string.Empty;

            return $"{DalamudApi.ClientState.LocalPlayer.Name}@{DalamudApi.ClientState.LocalPlayer.HomeWorld.GameData.Name}";
        }
    }
}