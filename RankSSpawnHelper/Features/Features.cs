﻿using System;

namespace RankSSpawnHelper.Features;

internal class Features : IDisposable
{
    public Counter           Counter           = new();
    public SearchCounter     SearchCounter     = new();
    public ShowHuntMap       ShowHuntMap       = new();
    public ShowInstance      ShowInstance      = new();
    public SpawnNotification SpawnNotification = new();

    public void Dispose()
    {
        Counter.Dispose();
        SpawnNotification.Dispose();
        ShowInstance.Dispose();
        ShowHuntMap.Dispose();
        SearchCounter.Dispose();
    }
}