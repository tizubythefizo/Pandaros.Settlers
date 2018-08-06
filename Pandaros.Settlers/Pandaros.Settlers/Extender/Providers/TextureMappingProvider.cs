﻿using Pandaros.Settlers.Items;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pandaros.Settlers.Extender.Providers
{
    public class TextureMappingProvider : ISettlersExtension
    {
        public List<Type> LoadedAssembalies { get; } = new List<Type>();

        public string InterfaceName => nameof(ICSTextureMapping);
        public Type ClassType => null;

        public void AfterAddingBaseTypes(Dictionary<string, ItemTypesServer.ItemTypeRaw> itemTypes)
        {
            
        }

        public void AfterItemTypesDefined()
        {
           
        }

        public void AfterSelectedWorld()
        {
            StringBuilder sb = new StringBuilder();
            PandaLogger.Log(ChatColor.lime, "-------------------Texture Mapping Loaded----------------------");

            foreach (var item in LoadedAssembalies)
            {
                if (Activator.CreateInstance(item) is ICSTextureMapping texture &&
                    !string.IsNullOrEmpty(texture.Name))
                {
                    ItemTypesServer.SetTextureMapping(texture.Name, new ItemTypesServer.TextureMapping(texture.ToJsonNode()));
                    sb.Append($"{texture.Name}, ");
                }
            }

            PandaLogger.Log(ChatColor.lime, sb.ToString());
            PandaLogger.Log(ChatColor.lime, "---------------------------------------------------------");
        }

        public void AfterWorldLoad()
        {

        }

        public void OnAddResearchables()
        {

        }
    }
}
