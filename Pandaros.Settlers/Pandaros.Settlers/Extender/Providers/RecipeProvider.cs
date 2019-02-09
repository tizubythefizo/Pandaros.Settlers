﻿using Pandaros.Settlers.Items;
using Recipes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pandaros.Settlers.Extender.Providers
{
    public class RecipeProvider : IAfterWorldLoad
    {
        public List<Type> LoadedAssembalies { get; } = new List<Type>();

        public string InterfaceName => nameof(ICSRecipe);
        public Type ClassType => null;

        public void AfterWorldLoad()
        {
            StringBuilder sb = new StringBuilder();
            PandaLogger.Log(ChatColor.lime, "-------------------Recipes Loaded----------------------");
            var i = 0;

            foreach (var item in LoadedAssembalies)
            {
                if (Activator.CreateInstance(item) is ICSRecipe recipe &&
                    !string.IsNullOrEmpty(recipe.name))
                {
                    var requirements = new List<InventoryItem>();
                    var results = new List<ItemTypes.ItemTypeDrops>();
                    recipe.JsonSerialize();

                    foreach (var ri in recipe.requires)
                        if (ItemTypes.IndexLookup.TryGetIndex(ri.type, out var itemIndex))
                            requirements.Add(new InventoryItem(itemIndex, ri.amount));

                    foreach (var ri in recipe.results)
                        if (ItemTypes.IndexLookup.TryGetIndex(ri.type, out var itemIndex))
                            results.Add(new ItemTypes.ItemTypeDrops(itemIndex, ri.amount));

                    var newRecipe = new Recipe(recipe.name, requirements, results, recipe.defaultLimit, recipe.isOptional, (int)recipe.defaultPriority);

                    if (recipe.isOptional)
                        ServerManager.RecipeStorage.AddOptionalLimitTypeRecipe(recipe.Job, newRecipe);
                    else
                        ServerManager.RecipeStorage.AddDefaultLimitTypeRecipe(recipe.Job, newRecipe);

                    sb.Append($"{recipe.name}, ");
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