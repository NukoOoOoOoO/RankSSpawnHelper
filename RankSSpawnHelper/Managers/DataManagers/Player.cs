﻿using System;
using System.Diagnostics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace RankSSpawnHelper.Managers.DataManagers;

internal class Player
{
    public string GetCurrentTerritory()
    {
        try
        {
            var instanceNumber = GetCurrentInstance();

            return Plugin.Managers.Data.FormatInstance(DalamudApi.ClientState.LocalPlayer.CurrentWorld.Id, DalamudApi.ClientState.TerritoryType, (uint)instanceNumber);
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, $"Exception from Managers::Data::GetCurrentInstance(). Last CallStack:{new StackFrame(1).GetMethod()?.Name}");
            return string.Empty;
        }
    }

    public string GetCurrentWorldName()
    {
        return DalamudApi.ClientState.LocalPlayer?.CurrentWorld.GameData!.Name != null ? DalamudApi.ClientState.LocalPlayer?.CurrentWorld.GameData.Name.RawString : "";
    }

    public uint GetCurrentWorldId()
    {
        if (DalamudApi.ClientState.LocalPlayer?.CurrentWorld.GameData!.RowId != null)
            return (uint)DalamudApi.ClientState.LocalPlayer?.CurrentWorld.GameData.RowId;

        return 0;
    }

    public unsafe int GetCurrentInstance()
    {
        return (int)UIState.Instance()->PublicInstance.InstanceId;
    }

    public static string GetLocalPlayerName()
    {
        return DalamudApi.ClientState.LocalPlayer == null ? string.Empty : $"{DalamudApi.ClientState.LocalPlayer.Name}@{DalamudApi.ClientState.LocalPlayer.HomeWorld.GameData.Name}";
    }
}