﻿using Pandaros.Settlers.Items;
using Pandaros.Settlers.Jobs.Roaming;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pandaros.Settlers.Extender.Providers
{
    public class ConnectedBlockCalculationTypeProvider : IAfterItemTypesDefined
    {
        public List<Type> LoadedAssembalies { get; } = new List<Type>();

        public string InterfaceName => nameof(IConnectedBlockCalculationType);
        public Type ClassType => null;

        public void AfterItemTypesDefined()
        {
            StringBuilder sb = new StringBuilder();
            PandaLogger.Log(ChatColor.lime, "-------------------Connected Block CalculationType Loaded----------------------");
            var i = 0;

            foreach (var s in LoadedAssembalies)
            {
                if (Activator.CreateInstance(s) is IConnectedBlockCalculationType connectedBlockCalcType &&
                    !string.IsNullOrEmpty(connectedBlockCalcType.name))
                {
                    sb.Append($"{connectedBlockCalcType.name}, ");
                    ConnectedBlockCalculator.CalculationTypes[connectedBlockCalcType.name] = connectedBlockCalcType;
                    i++;

                    if (i > 5)
                    {
                        sb.Append("</color>");
                        i = 0;
                        sb.AppendLine();
                        sb.Append("<color=lime>");
                    }
                }
            }

            PandaLogger.Log(ChatColor.lime, sb.ToString());
            PandaLogger.Log(ChatColor.lime, "---------------------------------------------------------");
        }
    }
}