﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RankSSpawnHelper.Features;

internal class SearchCounter : IDisposable
{
    private const uint TextNodeId = 0x133769;
    private readonly List<long> _playerIds = new();
    private readonly nint _rdataBegin;
    private readonly nint _rdataEnd;
    private int _searchCount;

    public SearchCounter()
    {
        _rdataBegin = DalamudApi.SigScanner.RDataSectionBase;
        _rdataEnd   = DalamudApi.SigScanner.RDataSectionBase + DalamudApi.SigScanner.RDataSectionSize;
        /*PluginLog.Debug($".data begin: {DalamudApi.SigScanner.DataSectionBase:X}, end: {DalamudApi.SigScanner.DataSectionBase + DalamudApi.SigScanner.DataSectionSize:X}, offset: {DalamudApi.SigScanner.DataSectionOffset}");
        PluginLog.Debug($".rdata begin: {DalamudApi.SigScanner.RDataSectionBase:X}, end: {DalamudApi.SigScanner.RDataSectionBase + DalamudApi.SigScanner.RDataSectionSize:X}, offset: {DalamudApi.SigScanner.RDataSectionOffset}");
        PluginLog.Debug($".text begin: {DalamudApi.SigScanner.TextSectionBase:X}, end: {DalamudApi.SigScanner.TextSectionBase + DalamudApi.SigScanner.TextSectionSize:X}, offset: {DalamudApi.SigScanner.TextSectionOffset}");
        PluginLog.Debug($"{DalamudApi.SigScanner.SearchBase:X}");*/

        SignatureHelper.Initialise(this);

        ProcessSocailListPacket.Enable();
        SocialListDraw.Enable();
        DalamudApi.Condition.ConditionChange += Condition_ConditionChange;
    }

    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("48 89 5C 24 ?? 56 48 83 EC 20 48 8B 0D ?? ?? ?? ?? 48 8B F2",
               DetourName = nameof(Detour_ProcessSocialListPacket))]
    private Hook<ProcessSocialListPacketDelegate> ProcessSocailListPacket { get; init; } = null!;

    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    [Signature("40 53 48 83 EC ?? 80 B9 ?? ?? ?? ?? ?? 48 8B D9 0F 29 74 24 ?? 0F 28 F1 74 ?? E8 ?? ?? ?? ?? C6 83",
               DetourName = nameof(Detour_SocialList_Draw))]
    private Hook<SocialListAddonShow> SocialListDraw { get; init; } = null!;

    public void Dispose()
    {
        ProcessSocailListPacket.Dispose();
        SocialListDraw.Dispose();
        DalamudApi.Condition.ConditionChange -= Condition_ConditionChange;
    }

    private void Condition_ConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.BetweenAreas51 || value)
        {
            return;
        }

        _playerIds.Clear();
        _searchCount = 0;
    }

    private unsafe nint Detour_ProcessSocialListPacket(nint a1, nint packetData)
    {
        var original         = ProcessSocailListPacket.Original(a1, packetData);
        var socialListStruct = Marshal.PtrToStructure<SocialList>(packetData);
        if (socialListStruct.ListType != 4) // we only need results from player search
            return original;

        var uiModule   = (UIModule*)DalamudApi.GameGui.GetUIModule();
        var infoModule = uiModule->GetInfoModule();
        var proxy      = (InfoProxySearch*)infoModule->GetInfoProxyById(InfoProxyId.PlayerSearch);

        var currentTerritoryId = DalamudApi.ClientState.TerritoryType;

        for (var i = 0u; i < proxy->InfoProxyCommonList.DataSize; i++)
        {
            var entry = proxy->InfoProxyCommonList.GetEntry(i);
            if (entry == null)
            {
                PluginLog.Debug($"Valid size {i}");
                break;
            }

            // when a player isn't in the same map as localplayer is, then just break
            // the info proxy is location-base order, the players in the same map as localplayer is,
            // they will be shown first
            if (currentTerritoryId != entry->Location)
                break;

            if (_playerIds.Contains(entry->ContentId))
                continue;

            _playerIds.Add(entry->ContentId);
            PluginLog.Debug($"{Marshal.PtrToStringUTF8((IntPtr)entry->Name)} / content_id: {entry->ContentId}");
        }

        // sometimes original would be at .rdata section
        // so we are gonna skip that
        if (original == 1 || (original >= (nint?)_rdataBegin && original <= (nint?)_rdataEnd))
            return original;

        _searchCount++;
        Plugin.Print(new List<Payload>
        {
            new TextPayload("在经过 "),
            new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor),
            new TextPayload($"{_searchCount} "),
            new UIForegroundPayload(0),
            new TextPayload("次搜索后, 和你在同一张图里大约有 "),
            new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor),
            new TextPayload($"{_playerIds.Count} "),
            new UIForegroundPayload(0),
            new TextPayload("人.")
        });

        if (Plugin.Configuration.PlayerSearchTip)
        {
            Plugin.Print(new List<Payload>
            {
                new TextPayload("对计数有疑问?或者不知道怎么用? 可以试试下面的方法: " +
                                "\n1. 先搜当前地图人数(不选择任何军队以及其他选项,只选地图)" +
                                "\n2. 如果得到的人数超过200,那就只选一个军队进行搜索" +
                                "\n      比如: 先搜双蛇,再搜恒辉,最后搜黑涡,以此反复循环" +
                                "\n3. 得到的人数将会是这几次搜索的总人数(已经排除重复的人)"),
                new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor),
                new TextPayload("\n本消息可以在 设置 -> 其他 里关掉")
            });
        }

        return original;
    }

    private unsafe void Detour_SocialList_Draw(AtkUnitBase* unitBase)
    {
        SocialListDraw.Original(unitBase);

        if (unitBase == null || (nint)unitBase == nint.Zero)
            return;

        if (!unitBase->IsVisible || unitBase->UldManager.NodeList == null)
            return;

        // for whatever reason, this function is also called in other addon..
        // probably some kind of inherited addon
        var name = Marshal.PtrToStringUTF8(new IntPtr(unitBase->Name));
        if (name is not "SocialList")
            return;

        var numberNodeRes = unitBase->UldManager.NodeList[3];
        if (numberNodeRes->Type != NodeType.Text)
            return;

        var numberNode = (AtkTextNode*)numberNodeRes;

        // Check if the node we created exists
        AtkTextNode* textNode = null;
        for (var i = 0; i < unitBase->UldManager.NodeListCount; i++)
        {
            if (unitBase->UldManager.NodeList[i] == null) continue;
            if (unitBase->UldManager.NodeList[i]->NodeID != TextNodeId) continue;
            textNode = (AtkTextNode*)unitBase->UldManager.NodeList[i];
            break;
        }

        if (_playerIds.Count == 0 && textNode != null)
        {
            textNode->AtkResNode.ToggleVisibility(false);
            return;
        }

        // Create one if it doesn't exist
        if (textNode == null)
        {
            var lastNode = unitBase->RootNode;
            if (lastNode == null)
                return;

            var newTextNode = (AtkTextNode*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkTextNode), 8);
            if (newTextNode == null)
                return;

            IMemorySpace.Memset(newTextNode, 0, (ulong)sizeof(AtkTextNode));
            newTextNode->Ctor();
            textNode = newTextNode;

            newTextNode->AtkResNode.Type      = NodeType.Text;
            newTextNode->AtkResNode.Flags     = numberNode->AtkResNode.Flags;
            newTextNode->AtkResNode.DrawFlags = 0;
            newTextNode->AtkResNode.SetPositionShort(1, 1);
            newTextNode->AtkResNode.SetWidth(numberNodeRes->GetWidth());
            newTextNode->AtkResNode.SetHeight(numberNodeRes->GetHeight());

            newTextNode->LineSpacing       = numberNode->LineSpacing;
            newTextNode->AlignmentFontType = (byte)AlignmentType.Right;
            newTextNode->FontSize          = numberNode->FontSize;
            newTextNode->TextFlags         = numberNode->TextFlags;
            newTextNode->TextFlags2        = numberNode->TextFlags2;

            newTextNode->AtkResNode.NodeID = TextNodeId;

            if (lastNode->ChildNode != null)
            {
                lastNode = lastNode->ChildNode;
                while (lastNode->PrevSiblingNode != null)
                {
                    lastNode = lastNode->PrevSiblingNode;
                }

                newTextNode->AtkResNode.NextSiblingNode = lastNode;
                newTextNode->AtkResNode.ParentNode      = unitBase->RootNode;
                lastNode->PrevSiblingNode               = (AtkResNode*)newTextNode;
            }
            else
            {
                lastNode->ChildNode                = (AtkResNode*)newTextNode;
                newTextNode->AtkResNode.ParentNode = lastNode;
            }

            unitBase->UldManager.UpdateDrawNodeList();
        }

        textNode->CharSpacing      = numberNode->CharSpacing;
        textNode->AtkResNode.Color = numberNode->AtkResNode.Color;
        textNode->EdgeColor        = numberNode->EdgeColor;
        textNode->TextColor        = numberNode->TextColor;

        ushort myTextWidth  = 0;
        ushort myTextHeight = 0;
        textNode->SetText($"在同一地图的有大概 {_playerIds.Count} 人");
        textNode->GetTextDrawSize(&myTextWidth, &myTextHeight);

        var drawPosX = numberNodeRes->X - numberNodeRes->Width;
        var drawPosY = numberNodeRes->Y;

        SetNodePosition((AtkResNode*)textNode, drawPosX, drawPosY);
        if (!textNode->AtkResNode.IsVisible)
            textNode->AtkResNode.ToggleVisibility(true);
    }

    private unsafe void SetNodePosition(AtkResNode* node, float x, float y)
    {
        node->X = x;
        node->Y = y;
    }

    [StructLayout(LayoutKind.Explicit, Size = 88, Pack = 1)]
    public struct SearchPlayerEntry
    {
        [FieldOffset(0x0)] public ulong Id;

        [FieldOffset(0x14)] public uint territoryType;
    }

    [StructLayout(LayoutKind.Sequential, Size = 896)]
    public struct SocialList
    {
        public ulong CommunityID; // 0
        public ushort NextIndex; // 8
        public ushort Index; // 10
        public byte ListType; // 12
        public byte RequestKey; // 13
        public byte RequestParam; // 14
        public byte __padding1; // 15

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public SearchPlayerEntry[] entries;
    }

    private delegate nint ProcessSocialListPacketDelegate(nint a1, nint packetData);

    private unsafe delegate void SocialListAddonShow(AtkUnitBase* a1);
}