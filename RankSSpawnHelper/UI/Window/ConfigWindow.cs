﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using RankSSpawnHelper.Managers.DataManagers;
using RankSSpawnHelper.Models;
using RankSSpawnHelper.Ui.Widgets;

namespace RankSSpawnHelper.Ui.Window;

public class ConfigWindow : Dalamud.Interface.Windowing.Window
{
    private const    ImGuiTableFlags TableFlags                    = ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp;
    private readonly string[]        _attemptMessageDisplayType    = { "不显示", "简单", "详细" };
    private readonly string[]        _attemptMessageFromServerType = { "关闭", "本大区", "本大区+其他大区" };

    private readonly List<ColorInfo> _colorInfos              = new();
    private readonly string[]        _expansions              = { "2.0", "3.0", "4.0", "5.0", "6.0" };
    private readonly string[]        _playerSearchDisplayType = { "关闭", "聊天框", "游戏界面", "都显示" };
    private readonly string[]        _spawnNotificationType   = { "关闭", "只在可触发时", "一直" };
    private readonly string[]        _tabNames                = { "计数", "查询S怪", "查询计数", "其他", "关于" };

    private ColorPickerType _colorPickerType = ColorPickerType.Failed;

    private List<string> _monsterNames;
    private int          _selectedExpansion;
    private int          _selectedInstance;
    private int          _selectedMonster;
    private int          _selectedTab;

    private string _serverUrl = string.Empty;
    private bool   _showColorPicker;

    private List<TrackerData> _trackerData              = new();
    private DateTime          _lastQueryTrackerDataTime = DateTime.UnixEpoch;

    public ConfigWindow() : base("SpawnHelper")
    {
        Initialize();
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(600, 400),
            MaximumSize = new(2000, 2000),
        };
    }

    public override void OnOpen()
    {
        base.OnOpen();
        _monsterNames ??= Plugin.Managers.Data.SRank.GetSRanksByExpansion((GameExpansion)_selectedExpansion);
    }

    private void DrawAfdian()
    {
        if (Plugin.Configuration.HideAfDian)
            return;

        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);
        if (ImGui.Button("爱发电 - 支持"))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName        = "https://afdian.net/a/YuuriChito"
                });
            }
            catch (Exception ex)
            {
                DalamudApi.PluginLog.Error($"{ex.Message}\n{ex.StackTrace}");
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ImGui.PopStyleColor(3);
    }

    public override void Draw()
    {
        Plugin.Managers.Font.NotoSan18.Push();
        DrawAfdian();

        ImGui.BeginGroup();

        ImGui.BeginChild("Child1##SpawnHelper", new(100, 0), true);

        for (var i = 0; i < _tabNames.Length; i++)
        {
            if (ImGui.Selectable(_tabNames[i] + "##SpawnHelper", i == _selectedTab))
            {
                _selectedTab = i;
            }
        }

        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("Child2##SpawnHelper", new(-1, -1), true);

        switch (_selectedTab)
        {
            case 0:
                DrawCounterTab();
                break;
            case 1:
                DrawQueryTab();
                break;
            case 2:
                DrawTrackerTab();
                break;
            case 3:
                DrawMiscTab();
                break;
            case 4:
                DrawAboutTab();
                break;
        }

        ImGui.EndChild();

        ImGui.EndGroup();

        Plugin.Managers.Font.NotoSan18.Pop();
    }

    public override void PostDraw()
    {
        DrawColorPicker();
    }

#if RELEASE || RELEASE_CN
    private string _proxyUrl = Plugin.Configuration.ProxyUrl;
#endif

    private void DrawCounterTab()
    {
        var trackKillCount = Plugin.Configuration.TrackKillCount;
        if (ImGui.Checkbox("启用", ref trackKillCount))
        {
            Plugin.Configuration.TrackKillCount = trackKillCount;
            Plugin.Windows.CounterWindow.IsOpen = trackKillCount;
            Plugin.Configuration.Save();
        }

        ImGui.SameLine();
        ImGui.Text("连接状态:");
        ImGui.SameLine();
        ImGui.TextColored(Plugin.Managers.Socket.Main.Connected() ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed, Plugin.Managers.Socket.Main.Connected() ? "Connected" : "Disconnected");

#if RELEASE || RELEASE_CN
        ImGui.SameLine();
        if (ImGui.Button("重新连接"))
            Plugin.Managers.Socket.Main.Reconnect();
#endif

        Widget.BeginFramedGroup("计数窗口");
        {
            var noTitle = Plugin.Configuration.TrackerWindowNoTitle;
            if (ImGui.Checkbox("无标题", ref noTitle))
            {
                Plugin.Configuration.TrackerWindowNoTitle = noTitle;
                Plugin.Configuration.Save();
            }

            ImGui.SameLine();
            var noBackground = Plugin.Configuration.TrackerWindowNoBackground;
            if (ImGui.Checkbox("无背景", ref noBackground))
            {
                Plugin.Configuration.TrackerWindowNoBackground = noBackground;
                Plugin.Configuration.Save();
            }

            ImGui.SameLine();
            var autoResize = Plugin.Configuration.TrackerAutoResize;
            if (ImGui.Checkbox("自动调整大小", ref autoResize))
            {
                Plugin.Configuration.TrackerAutoResize = autoResize;
                Plugin.Configuration.Save();
            }
        }
        Widget.EndFramedGroup();

        Widget.BeginFramedGroup("其他");
        {
            var showCurrentInstance = Plugin.Configuration.TrackerShowCurrentInstance;
            if (ImGui.Checkbox("只显示当前区域", ref showCurrentInstance))
            {
                Plugin.Configuration.TrackerShowCurrentInstance = showCurrentInstance;
                Plugin.Windows.CounterWindow.IsOpen             = Plugin.Features.Counter.GetLocalTrackers().ContainsKey(Plugin.Managers.Data.Player.GetCurrentTerritory());
                Plugin.Configuration.Save();
            }

            var clearThreshold = Plugin.Configuration.TrackerClearThreshold;
            ImGui.Text("x 分钟内没更新自动清除相关计数");
            ImGui.SetNextItemWidth(178);
#if DEBUG || DEBUG_CN
            if (ImGui.SliderFloat("##在多少分钟后没更新就自动清除计数", ref clearThreshold, 1f, 60f, "%.2f分钟"))
#else
            if (ImGui.SliderFloat("##在多少分钟后没更新就自动清除计数", ref clearThreshold, 30f, 60f, "%.2f分钟"))
#endif
            {
                Plugin.Configuration.TrackerClearThreshold = clearThreshold;
                Plugin.Configuration.Save();
            }

            var weeEaCounter = Plugin.Configuration.WeeEaCounter;
            if (ImGui.Checkbox("小异亚计数", ref weeEaCounter))
            {
                Plugin.Configuration.WeeEaCounter = weeEaCounter;
                Plugin.Windows.WeeEaWindow.IsOpen = weeEaCounter && DalamudApi.ClientState.TerritoryType == 960;
                Plugin.Configuration.Save();
            }
        }
        Widget.EndFramedGroup();

#if DEBUG || DEBUG_CN
        ImGui.SetNextItemWidth(200);
        ImGui.InputText("服务器链接", ref _serverUrl, 256);

        if (ImGui.Button("连接")) Plugin.Managers.Socket.Main.Connect(_serverUrl);
        ImGui.SameLine();
        if (ImGui.Button("连接到临时服务器")) Plugin.Managers.Socket.Main.Connect("wss://nuko.me/ws");
        ImGui.SameLine();
        if (ImGui.Button("断开连接")) Plugin.Managers.Socket.Main.Disconnect();
#endif

#if RELEASE || RELEASE_CN
        Widget.BeginFramedGroup("代理设置");
        ImGui.TextColored(ImGuiColors.DPSRed, "连不上的时候再用!!!!");

        ImGui.TextUnformatted("使用方法: {代理类型}://127.0.0.1:{代理端口}, 比如 http://127.0.0.1:7890.");
        ImGui.TextUnformatted("如果不知道怎么填请查看你所使用的代理设置.");
        ImGui.TextUnformatted("Clash(图标是猫的)默认是http,端口7890, Shadowsocks(小飞机)默认是socks5,端口1080");
        ImGui.TextUnformatted("请根据自己的实际情况填写,上述以及默认给出来的链接仅供参考!!!!!");
        ImGui.NewLine();

        var useProxy = Plugin.Configuration.UseProxy;
        if (ImGui.Checkbox("使用代理连接到服务器", ref useProxy))
        {
            Plugin.Configuration.UseProxy = useProxy;
            Plugin.Configuration.Save();
        }

        if (ImGui.InputTextWithHint("##proxyURL", "代理链接", ref _proxyUrl, 256))
        {
            Plugin.Configuration.ProxyUrl = _proxyUrl;
        }

        if (ImGui.Button("保存并重新连接"))
        {
            Plugin.Configuration.Save();
            Plugin.Managers.Socket.Main.Reconnect();
        }

        Widget.EndFramedGroup();
#endif

        DrawTrackerTable();
    }

    private static void DrawTrackerTable()
    {
        var tracker = Plugin.Features.Counter.GetLocalTrackers();

        if (tracker.Count == 0)
            return;

        // 从FFLogsViewer那边拿过来的, credit goes to Aireil <3333
        if (ImGui.BeginTable("##农怪计数表格", 6, TableFlags, new(-1, -1)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("所在服务器");
            ImGui.TableSetupColumn("开始时间");
            ImGui.TableSetupColumn("##计数①");
            ImGui.TableSetupColumn("##计数②");
            ImGui.TableSetupColumn("##计数③");
            ImGui.TableSetupColumn("##删除计数清除", ImGuiTableColumnFlags.WidthFixed, 3 * ImGui.GetFrameHeight());
            ImGui.TableHeadersRow();

            foreach (var (mainKey, mainValue) in Plugin.Features.Counter.GetLocalTrackers())
            {
                ImGui.PushID($"##农怪表格{mainKey}");

                ImGui.TableNextRow();

                ImGui.TableNextColumn();

                var displayText = mainKey.Replace("@", " ");

                ImGui.SetNextItemWidth(ImGui.CalcTextSize(displayText).X);
                ImGui.Text(displayText);

                ImGui.TableNextColumn();
                var time = DateTimeOffset.FromUnixTimeSeconds(mainValue.startTime).LocalDateTime;
                displayText = $"{time.Month}-{time.Day} / {time.ToShortTimeString()}";
                ImGui.SetNextItemWidth(ImGui.CalcTextSize(displayText).X);
                ImGui.Text(displayText);

                ImGui.TableNextColumn();
                var i = 0;

                foreach (var (subKey, subValue) in mainValue.counter)
                {
                    ImGui.Text($"{subKey}: {subValue}");
                    ImGui.TableNextColumn();
                    i++;
                }

                for (; i < 3; i++)
                {
                    ImGui.Text("-");
                    ImGui.TableNextColumn();
                }

                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2, ImGui.GetStyle().FramePadding.Y));
                if (ImGui.Button("\xF12D")) Plugin.Features.Counter.RemoveInstance(mainKey);
                ImGui.PopStyleVar();
                ImGui.PopFont();

                ImGui.PopID();
            }

            ImGui.EndTable();
        }
    }

    private void DrawQueryTab()
    {
        if (ImGui.Combo("版本", ref _selectedExpansion, _expansions, _expansions.Length))
        {
            _monsterNames    = Plugin.Managers.Data.SRank.GetSRanksByExpansion((GameExpansion)_selectedExpansion);
            _selectedMonster = 0;
        }

        ImGui.Combo("S怪", ref _selectedMonster, _monsterNames.ToArray(), _monsterNames.Count);
        if (ImGui.InputInt("几线", ref _selectedInstance, 1)) _selectedInstance = Math.Clamp(_selectedInstance, 0, 3);

        ImGui.SameLine();

        {
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button(FontAwesomeIcon.Search.ToIconString()))
                Plugin.Managers.Data.SRank.FetchData(Plugin.Managers.Data.GetServers(), _monsterNames[_selectedMonster], _selectedInstance);
            ImGui.PopFont();
        }

        if (Plugin.Managers.Data.SRank.GetErrorMessage() != string.Empty)
            ImGui.TextColored(ImGuiColors.DPSRed, Plugin.Managers.Data.SRank.GetErrorMessage());
        else
        {
            if (Plugin.Managers.Data.SRank.GetFetchStatus() == FetchStatus.Fetching)
                ImGui.Text("正在获取数据");
            else if (Plugin.Managers.Data.SRank.GetFetchStatus() == FetchStatus.Success)
            {
                var huntStatus = Plugin.Managers.Data.SRank.GetHuntStatus();
                if (huntStatus == null || huntStatus.Count == 0)
                {
                    ImGui.TextColored(ImGuiColors.DPSRed, "成功获取数据但无效??");
                    return;
                }

                var canShowHuntMap = Plugin.Features.ShowHuntMap.CanShowHuntMapWithMonsterName(huntStatus[0].localizedName);

                foreach (var status in huntStatus)
                {
                    var currentInstance = $"{status.worldName}@{status.localizedName}{(status.instance == 0 ? "" : $" {status.instance}线")}";
                    ImGui.Text($"{currentInstance} 的状态:");

                    ImGui.Text("\t是否丢失:");
                    ImGui.SameLine();
                    ImGui.TextColored(status.missing ? ImGuiColors.DPSRed : ImGuiColors.ParsedGreen, status.missing ? "是" : "否");

                    var now     = DateTimeOffset.Now;
                    var minTime = DateTimeOffset.FromUnixTimeSeconds(status.expectMinTime).AddMinutes(-30);
                    var maxTime = DateTimeOffset.FromUnixTimeSeconds(status.expectMaxTime);
                    if (minTime > now)
                    {
                        var delta = minTime - now;
                        ImGui.Text($"\t距离进入可触发时间还有: {delta.Hours + delta.Days * 24:D2}小时{delta.Minutes:D2}分{delta.Seconds:D2}秒");
                    }
                    else
                    {
                        var percentage = 100 * ((now.ToUnixTimeSeconds() - status.expectMinTime) / (double)(status.expectMaxTime - status.expectMinTime));
                        ImGui.Text("\t当前可触发的概率为:");
                        ImGui.SameLine();
                        ImGui.TextColored(percentage > 100.0 ? ImGuiColors.ParsedBlue : ImGuiColors.ParsedGreen, $"{percentage:F2}%%");
                        // ReSharper disable once InvertIf
                        if (now < maxTime)
                        {
                            var delta = maxTime - now;
                            ImGui.Text($"\t距离进入强制期还有: {delta.Hours + delta.Days * 24:D2}小时{delta.Minutes:D2}分{delta.Seconds:D2}秒");
                        }
                    }

                    if (!canShowHuntMap)
                        continue;

                    ImGui.SameLine();

                    if (ImGui.Button($"查看触发点##{status.worldName}"))
                    {
                        Task.Run(async () =>
                                 {
                                     var huntMap = await Plugin.Managers.Data.SRank.FetchHuntMap(status.worldName, status.localizedName, status.instance);
                                     if (huntMap == null || huntMap.spawnPoints.Count == 0)
                                     {
                                         DalamudApi.PluginLog.Debug("huntMap == null || huntMap.spawnPoints.Count == 0 from QueryTab");
                                         return;
                                     }

                                     var texture = Plugin.Features.ShowHuntMap.GeTextureWithMonsterName(status.localizedName);
                                     if (texture == null)
                                     {
                                         var territoryType = Plugin.Features.ShowHuntMap.GetTerritoryTypeByHuntName(status.localizedName);
                                         if (territoryType == null)
                                             return;

                                         Plugin.Managers.Data.MapTexture.AddMapTexture(territoryType.RowId, territoryType.Map.Value);
                                     }

                                     Plugin.Windows.HuntMapWindow.SetCurrentMap(texture, huntMap.spawnPoints, currentInstance);
                                     Plugin.Windows.HuntMapWindow.IsOpen = true;
                                 });
                    }
                }
            }
        }
    }

    private void DrawMiscTab()
    {
        Widget.BeginFramedGroup("消息提示");
        {
            ImGui.Text("触发概率提醒");
            var spawnNotificationType = (int)Plugin.Configuration.SpawnNotificationType;
            for (var i = 0; i < _spawnNotificationType.Length; i++)
            {
                if (ImGui.RadioButton(_spawnNotificationType[i] + "##_spawnNotificationType", ref spawnNotificationType, i))
                {
                    Plugin.Configuration.SpawnNotificationType = (SpawnNotificationType)spawnNotificationType;
                    Plugin.Configuration.Save();
                }

                if (i == _spawnNotificationType.Length - 1)
                    break;

                ImGui.SameLine();
            }

            if (Plugin.Configuration.SpawnNotificationType == SpawnNotificationType.Full)
            {
                ImGui.SameLine();
                var coolDownNotificationSound = Plugin.Configuration.CoolDownNotificationSound;
                if (ImGui.Checkbox("不在触发期时播放提示音", ref coolDownNotificationSound))
                {
                    Plugin.Configuration.CoolDownNotificationSound = coolDownNotificationSound;
                    Plugin.Configuration.Save();
                }
            }

            ImGui.Text("接收来自服务器的触发消息");
            var attemptMessageFromServer = (int)Plugin.Configuration.AttemptMessageFromServer;
            for (var i = 0; i < _attemptMessageFromServerType.Length; i++)
            {
                if (ImGui.RadioButton(_attemptMessageFromServerType[i] + "##_attemptMessageFromServerType", ref attemptMessageFromServer, i))
                {
                    Plugin.Configuration.AttemptMessageFromServer = (AttemptMessageFromServerType)attemptMessageFromServer;
                    Plugin.Configuration.Save();
                }

                if (i == _attemptMessageFromServerType.Length - 1)
                    break;

                ImGui.SameLine();
            }

            if (Plugin.Configuration.AttemptMessageFromServer != AttemptMessageFromServerType.Off)
            {
                ImGui.SameLine();
                var showMessageInDungenos = Plugin.Configuration.ShowAttemptMessageInDungeons;
                if (ImGui.Checkbox("在副本里显示消息", ref showMessageInDungenos))
                {
                    Plugin.Configuration.ShowAttemptMessageInDungeons = showMessageInDungenos;
                    Plugin.Configuration.Save();
                }
            }
        }
        Widget.EndFramedGroup();

        Widget.BeginFramedGroup("触发消息");
        {
            var attemptMessageType = (int)Plugin.Configuration.AttemptMessage;
            for (var i = 0; i < _attemptMessageDisplayType.Length; i++)
            {
                if (ImGui.RadioButton(_attemptMessageDisplayType[i], ref attemptMessageType, i))
                {
                    Plugin.Configuration.AttemptMessage = (AttemptMessageType)attemptMessageType;
                    Plugin.Configuration.Save();
                }

                if (i == _attemptMessageDisplayType.Length - 1)
                    break;

                ImGui.SameLine();
            }

            ImGui.BeginGroup();
            {
                ImGui.Text("失败消息颜色");
                ImGui.SameLine();
                if (ImGui.ColorButton("##触发失败", GetColor(Plugin.Configuration.FailedMessageColor)))
                {
                    _showColorPicker = true;
                    _colorPickerType = ColorPickerType.Failed;
                }

                ImGui.Text("成功消息颜色");
                ImGui.SameLine();
                if (ImGui.ColorButton("##触发成功", GetColor(Plugin.Configuration.SpawnedMessageColor)))
                {
                    _showColorPicker = true;
                    _colorPickerType = ColorPickerType.Spawned;
                }

                ImGui.Text("    关键词颜色");
                ImGui.SameLine();
                if (ImGui.ColorButton("##高亮", GetColor(Plugin.Configuration.HighlightColor)))
                {
                    _showColorPicker = true;
                    _colorPickerType = ColorPickerType.Highlighted;
                }

                ImGui.EndGroup();
            }
            ImGui.SameLine();

            ImGui.BeginGroup();
            {
                ImGui.TextColored(GetColor(Plugin.Configuration.FailedMessageColor), "寄啦，救命啊");
                ImGui.TextColored(GetColor(Plugin.Configuration.SpawnedMessageColor), "出货啦");
                ImGui.TextColored(GetColor(Plugin.Configuration.HighlightColor), "你为什么偷着乐");
            }
            ImGui.EndGroup();
        }
        Widget.EndFramedGroup();

        ImGui.SameLine();
        Widget.BeginFramedGroup("服务器重启时间");

        var timestamp = Plugin.Managers.Data.GetServerRestartTimeRaw();
        if (timestamp > 0)
        {
            ImGui.Text("时间戳:");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, $"{timestamp}");
            var datetime = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime.ToLocalTime();
            ImGui.TextColored(ImGuiColors.ParsedGreen, $"{datetime.ToShortDateString()} {datetime.ToShortTimeString()}");

            if (ImGui.Button("复制"))
            {
                ImGui.SetClipboardText(DalamudApi.ClientState.LocalPlayer != null
                                           ? $"{DalamudApi.ClientState.LocalPlayer!.CurrentWorld.GameData!.DataCenter.Value!.Name.RawString}的重启时间: {datetime.ToShortDateString()} {datetime.ToShortTimeString()} / 时间戳: {timestamp} / 时区: {TimeZoneInfo.Local.BaseUtcOffset.TotalHours}"
                                           : $"重启时间: {datetime.ToShortDateString()} {datetime.ToShortTimeString()} / 时间戳: {timestamp} / 时区: {TimeZoneInfo.Local.BaseUtcOffset.TotalHours}");
            }
        }
        else
        {
            ImGui.Text("没有获取到重启时间");
            ImGui.Text("如何获取:");
            ImGui.Text("打开队员招募即可.\n如果已打开,点有招募的分类");
        }

        Widget.EndFramedGroup();

        Widget.BeginFramedGroup("其他");
        {
            var showInstance = Plugin.Configuration.ShowInstance;
            if (ImGui.Checkbox("在基本情报栏显示几线", ref showInstance))
            {
                Plugin.Configuration.ShowInstance = showInstance;
                Plugin.Configuration.Save();
            }

            var autoShowHuntMap = Plugin.Configuration.AutoShowHuntMap;
            if (ImGui.Checkbox("自动获取点位列表", ref autoShowHuntMap))
            {
                Plugin.Configuration.AutoShowHuntMap = autoShowHuntMap;
                Plugin.Configuration.Save();
            }

            if (Plugin.Configuration.AutoShowHuntMap && Plugin.Configuration.SpawnNotificationType > 0)
            {
                ImGui.SameLine();
                var onlyFetchInDuration = Plugin.Configuration.OnlyFetchInDuration;
                if (ImGui.Checkbox("只在触发期内获取", ref onlyFetchInDuration))
                {
                    Plugin.Configuration.OnlyFetchInDuration = onlyFetchInDuration;
                    Plugin.Configuration.Save();
                }
            }

            var hideAfdian = Plugin.Configuration.HideAfDian;
            if (ImGui.Checkbox("隐藏爱发电按钮", ref hideAfdian))
            {
                Plugin.Configuration.HideAfDian = hideAfdian;
                Plugin.Configuration.Save();
            }

            ImGui.Text("玩家搜索显示类型");
            var playerSearchDispalyType = (int)Plugin.Configuration.PlayerSearchDispalyType;
            for (var i = 0; i < _playerSearchDisplayType.Length; i++)
            {
                if (ImGui.RadioButton(_playerSearchDisplayType[i] + "##__playerSearchDisplayType", ref playerSearchDispalyType, i))
                {
                    Plugin.Configuration.PlayerSearchDispalyType = (PlayerSearchDispalyType)playerSearchDispalyType;
                    Plugin.Configuration.Save();
                }

                if (i == _playerSearchDisplayType.Length - 1)
                    break;

                ImGui.SameLine();
            }

            var playerSearchTip = Plugin.Configuration.PlayerSearchTip;
            if (ImGui.Checkbox("显示玩家搜索提示", ref playerSearchTip))
            {
                Plugin.Configuration.PlayerSearchTip = playerSearchTip;
                Plugin.Configuration.Save();
            }
        }
        Widget.EndFramedGroup();
    }

    private void DrawAboutTab()
    {
        ImGui.Text("在使用本插件的联网功能时,将会收集以下信息:\n" +
                   "    - 游戏ID以及所属服务器, 如 Deez Nuts@Siren\n" +
                   "    - 当前所在区域, 如: 海猫茶屋@叹息海@0\n" +
                   "    - 计数的数量&名字, 如: 矮人棉: 50, 彷徨之物: 12\n" +
                   "    - 开始计数的时间戳, 如: 1667166428\n");
    }

    private void DrawColorPicker()
    {
        if (!_showColorPicker)
            return;

        ImGui.SetNextWindowSize(new(320, 360));

        var type = _colorPickerType switch
                   {
                       ColorPickerType.Failed      => "给触发失败消息",
                       ColorPickerType.Highlighted => "给关键词",
                       ColorPickerType.Spawned     => "给触发成功消息",
                       _                           => ""
                   };

        if (!ImGui.Begin(type + "选个颜色呗", ref _showColorPicker, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoResize)) return;

        ImGui.Columns(10, "##colorcolumns", false);

        foreach (var info in _colorInfos)
        {
            if (ImGui.ColorButton("##" + info.RowId, GetColor(info.RowId)))
            {
                switch (_colorPickerType)
                {
                    case ColorPickerType.Failed:
                        Plugin.Configuration.FailedMessageColor = info.RowId;
                        break;
                    case ColorPickerType.Highlighted:
                        Plugin.Configuration.HighlightColor = info.RowId;
                        break;
                    case ColorPickerType.Spawned:
                        Plugin.Configuration.SpawnedMessageColor = info.RowId;
                        break;
                }

                Plugin.Configuration.Save();
                _showColorPicker = false;
            }

            ImGui.NextColumn();
        }

        ImGui.Columns(1);
        ImGui.End();
    }

    private void DrawTrackerTab()
    {
        if (ImGui.Button("点我获取数据"))
        {
            if (DateTime.Now - _lastQueryTrackerDataTime >= TimeSpan.FromSeconds(30) && Plugin.Managers.Socket.Main.Connected())
            {
                Plugin.Managers.Socket.Main.SendMessage(new GetTrackerList
                {
                    Type       = "GetTrackerList",
                    ServerList = Plugin.Managers.Data.GetServers(),
                });
                _lastQueryTrackerDataTime = DateTime.Now;
            }
        }

        ImGui.SameLine();

        {
            var temp = "";
            if (_lastQueryTrackerDataTime - DateTime.Now > TimeSpan.FromSeconds(0))
                temp = $"还要等 {(_lastQueryTrackerDataTime - DateTime.Now).Seconds} 秒才可以请求";
            ImGui.TextUnformatted($"上一次请求时间: {_lastQueryTrackerDataTime.ToLocalTime()} {temp}");
        }
        if (_trackerData.Count == 0)
        {
            ImGui.TextUnformatted("目前还没有数据");
            return;
        }

        foreach (var data in _trackerData)
        {
            var counter = "";
            foreach (var i in data.CounterData)
            {
                var isItem = data.TerritoryId is 814 or 400 or 961 or 813;
                var keyName = isItem ? Plugin.Managers.Data.GetItemName(i.Key) :
                              data.TerritoryId == 621 ? "扔垃圾" : Plugin.Managers.Data.GetNpcName(i.Key);
                counter += $"{keyName}: {i.Value} ";
            }

            ImGui.TextUnformatted($"{Plugin.Managers.Data.FormatInstance(data.WorldId, data.TerritoryId, data.InstanceId)}: 上一次更新时间: {DateTimeOffset.FromUnixTimeSeconds(data.LastUpdateTime).DateTime.ToLocalTime()}\n        {counter}");
        }
    }

    private Vector4 GetColor(uint id)
    {
        try
        {
            var bytes = BitConverter.GetBytes(_colorInfos.Find(info => info.RowId == id).Color);
            return new((float)bytes[3] / 255, (float)bytes[2] / 255, (float)bytes[1] / 255, (float)bytes[0] / 255);
        }
        catch (Exception)
        {
            return new(0f, 0f, 0f, 0f);
        }
    }

    private void Initialize()
    {
        var colorSheet = DalamudApi.DataManager.Excel.GetSheet<UIColor>();
        foreach (var color in colorSheet)
        {
            var result = _colorInfos.Find(info => info.Color == color.UIForeground);
            if (result == null)
            {
                _colorInfos.Add(new()
                {
                    RowId = color.RowId,
                    Color = color.UIForeground
                });
            }
        }
    }

    public void SetTrackerData(List<TrackerData> data)
    {
        _trackerData = data;
    }

    private enum ColorPickerType
    {
        Failed,
        Spawned,
        Highlighted
    }

    internal class ColorInfo
    {
        internal uint RowId { get; set; }
        internal uint Color { get; set; }
    }
}