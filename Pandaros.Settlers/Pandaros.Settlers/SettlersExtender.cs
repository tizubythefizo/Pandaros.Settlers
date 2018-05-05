﻿using Pandaros.Settlers.Items;
using Pandaros.Settlers.Items.Machines;
using Pandaros.Settlers.Managers;
using Pandaros.Settlers.Monsters.Bosses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Pandaros.Settlers
{
    [ModLoader.ModManager]
    class SettlersExtender
    {
        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterModsLoaded, GameLoader.NAMESPACE + ".Gameloader.AfterModsLoaded")]
        public static void AfterModsLoaded(List<ModLoader.ModDescription> list)
        {
            foreach (var mod in list.Where(m => m.HasAssembly))
            {
                try
                {
                    // Get all Types available in the assembly in an array
                    Type[] typeArray = mod.LoadedAssembly.GetTypes();

                    // Walk through each Type and list their Information
                    foreach (Type type in typeArray)
                    {
                        Type[] ifaces = type.GetInterfaces();

                        foreach (Type iface in ifaces)
                        {
                            switch (iface.Name)
                            {
                                case nameof(IMagicItem):
                                    IMagicItem magicItem = (IMagicItem)Activator.CreateInstance(type);
                                    break;

                                case nameof(IPandaBoss):
                                    IPandaBoss pandaBoss = (IPandaBoss)Activator.CreateInstance(type, new Server.AI.Path(), Players.GetPlayer(NetworkID.Server));
                                    break;

                                case nameof(IMachineSettings):
                                    IMachineSettings machineSettings = (IMachineSettings)Activator.CreateInstance(type);
                                    MachineManager.RegisterMachineType(machineSettings.Name, machineSettings);
                                    break;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Do not log it is not the correct type.
                }
            }
        }
    }
}
