﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Game.Command;
using RankSSpawnHelper.Attributes;
using static Dalamud.Game.Command.CommandInfo;

namespace RankSSpawnHelper
{
    public class PluginCommandManager<THost> : IDisposable
    {
        private readonly THost host;
        private readonly (string, CommandInfo)[] pluginCommands;

        public PluginCommandManager(THost host)
        {
            this.host = host;

            pluginCommands = host.GetType()
                                 .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                                 .Where(method => method.GetCustomAttribute<CommandAttribute>() != null)
                                 .SelectMany(GetCommandInfoTuple)
                                 .ToArray();

            AddCommandHandlers();
        }

        public void Dispose()
        {
            RemoveCommandHandlers();
            GC.SuppressFinalize(this);
        }

        private void AddCommandHandlers()
        {
            foreach (var (command, commandInfo) in pluginCommands)
            {
                DalamudApi.CommandManager.AddHandler(command, commandInfo);
            }
        }

        private void RemoveCommandHandlers()
        {
            foreach (var (command, _) in pluginCommands)
            {
                DalamudApi.CommandManager.RemoveHandler(command);
            }
        }

        private IEnumerable<(string, CommandInfo)> GetCommandInfoTuple(MethodInfo method)
        {
            var handlerDelegate = (HandlerDelegate)Delegate.CreateDelegate(typeof(HandlerDelegate), host, method);

            var command         = handlerDelegate.Method.GetCustomAttribute<CommandAttribute>();
            var aliases         = handlerDelegate.Method.GetCustomAttribute<AliasesAttribute>();
            var helpMessage     = handlerDelegate.Method.GetCustomAttribute<HelpMessageAttribute>();
            var doNotShowInHelp = handlerDelegate.Method.GetCustomAttribute<DoNotShowInHelpAttribute>();

            var commandInfo = new CommandInfo(handlerDelegate)
                              {
                                  HelpMessage = helpMessage?.HelpMessage ?? string.Empty,
                                  ShowInHelp  = doNotShowInHelp == null
                              };

            // Create list of tuples that will be filled with one tuple per alias, in addition to the base command tuple.
            var commandInfoTuples = new List<(string, CommandInfo)> { (command!.Command, commandInfo) };
            if (aliases != null)
            {
                foreach (var alias in aliases.Aliases)
                {
                    commandInfoTuples.Add((alias, commandInfo));
                }
            }

            return commandInfoTuples;
        }
    }
}