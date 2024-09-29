﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace RankSSpawnHelper.Features;

internal class SpawnNotification : IDisposable
{
    /*private readonly Dictionary<string, HuntStatus> _huntStatus = new();*/

    private readonly Dictionary<ushort, uint> _monsterIdMap = new()
    {
        { 960, 10622 }, // 狭缝
        { 959, 10620 }, // 沉思之物
        { 958, 10619 }, // 阿姆斯特朗
        { 957, 10618 }, // 颇胝迦
        { 956, 10617 }, // 布弗鲁
        { 814, 8910 },  // 得到宽恕的炫学
        { 815, 8900 },  // 多智兽
        { 816, 8653 },  // 阿格拉俄珀
        { 817, 8890 },  // 伊休妲
        { 621, 5989 },  // 盐和光
        { 614, 5985 },  // 伽马
        { 613, 5984 },  // 巨大鳐
        { 612, 5987 },  // 优昙婆罗花
        { 402, 4380 },  // 卢克洛塔
        { 400, 4377 },  // 刚德瑞瓦
        { 397, 4374 },  // 凯撒贝希摩斯
        { 147, 2961 }   // 蚓螈巨虫
    };

    private bool _shouldNotNotify;

    public SpawnNotification()
    {
        DalamudApi.ChatGui.ChatMessage       += ChatGui_OnChatMessage;
        DalamudApi.Condition.ConditionChange += Condition_OnConditionChange;
    }

    public void Dispose()
    {
        DalamudApi.ChatGui.ChatMessage       -= ChatGui_OnChatMessage;
        DalamudApi.Condition.ConditionChange -= Condition_OnConditionChange;
    }

    private void ChatGui_OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool ishandled)
    {
        if (type != XivChatType.SystemMessage)
            return;

        if (message.TextValue != "感觉到了强大的恶名精英的气息……")
            return;

        _shouldNotNotify = true;

        /*var currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();
        _huntStatus.Remove(currentInstance);*/
    }

    private void Condition_OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.BetweenAreas51 || value)
            return;

        Task.Run(
                 async () =>
                 {
                     try
                     {
                         if (Plugin.Configuration.SpawnNotificationType == (int)SpawnNotificationType.Off)
                             return;

                         var territory = DalamudApi.ClientState.TerritoryType;

                         if (!_monsterIdMap.TryGetValue(territory, out var srankId))
                         {
                             return;
                         }

                         if (_shouldNotNotify)
                         {
                             _shouldNotNotify = false;
                             return;
                         }

                         var currentInstance = Plugin.Managers.Data.Player.GetCurrentTerritory();
                         var split           = currentInstance.Split('@');
                         var monsterName     = Plugin.Managers.Data.SRank.GetSRankNameById(srankId);

                         /*if (!_huntStatus.TryGetValue(currentInstance, out var result))
                         {
                             result = await Plugin.Managers.Data.SRank.FetchHuntStatus(split[0], monsterName, split.Length == 2
                                                                                                                  ? 0
                                                                                                                  : int.Parse(split[2]));
                         }*/

                         var result = await Plugin.Managers.Data.SRank.FetchHuntStatus(split[0], monsterName, split.Length == 2
                                                                                                                ? 0
                                                                                                                : int.Parse(split[2]));

                         var payloads = new List<Payload>
                         {
                             new UIForegroundPayload(1),
                             new TextPayload($"{currentInstance} - {monsterName}:")
                         };

                         var isSpawnable = DateTimeOffset.Now.ToUnixTimeSeconds() > result.expectMinTime;
                         if (isSpawnable)
                         {
                             payloads.Add(new TextPayload("\n当前可触发概率: "));
                             payloads.Add(new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor));
                             payloads.Add(
                                          new
                                              TextPayload(
                                                          $"{100 * ((DateTimeOffset.Now.ToUnixTimeSeconds() - result.expectMinTime) / (double)(result.expectMaxTime - result.expectMinTime)):F1}%"));
                             payloads.Add(new UIForegroundPayload(0));
                         }
                         else
                         {
                             if (Plugin.Configuration.SpawnNotificationType == SpawnNotificationType.SpawnableOnly)
                             {
                                 DalamudApi.PluginLog.Debug("Not spawnable, ignoring");
                                 return;
                             }

                             payloads.Add(new TextPayload("\n距离进入可触发期还有 "));
                             payloads.Add(new UIForegroundPayload((ushort)Plugin.Configuration.HighlightColor));
                             var minTime = DateTimeOffset.FromUnixTimeSeconds(result.expectMinTime);
                             var delta   = (minTime - DateTimeOffset.Now).TotalMinutes;

                             payloads.Add(new TextPayload($"{delta / 60:F0}小时{delta % 60:F0}分钟"));
                             payloads.Add(new UIForegroundPayload(0));

                             if (Plugin.Configuration.CoolDownNotificationSound)
                             {
                                 UIModule.PlayChatSoundEffect(6);
                                 UIModule.PlayChatSoundEffect(6);
                                 UIModule.PlayChatSoundEffect(6);
                             }
                         }

                         payloads.Add(new UIForegroundPayload(0));

                         Plugin.Print(payloads);
                         if (isSpawnable && Plugin.Configuration.OnlyFetchInDuration)
                             Plugin.Features.ShowHuntMap.FetchAndPrint();
                     }
                     catch (Exception e)
                     {
                         DalamudApi.PluginLog.Error(e, "Error when fetching hunt status");
                     }
                 });
    }
}