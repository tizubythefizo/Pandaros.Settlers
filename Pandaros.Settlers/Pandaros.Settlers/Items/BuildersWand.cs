﻿using BlockTypes.Builtin;
using Pandaros.Settlers.Entities;
using Pipliz;
using Pipliz.JSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Pandaros.Settlers.Items
{
    [ModLoader.ModManager]
    public static class BuildersWand
    {
        public enum WandMode
        {
            Horizontal = 0,
            Vertical = 1,
            TopAndBottomX = 2,
            TopAndBottomZ = 3
        }

        public static ItemTypesServer.ItemTypeRaw Item { get; private set; }
        public static ItemTypesServer.ItemTypeRaw Selector { get; private set; }
        public const int DURABILITY = 750;
        public const int WAND_MAX_RANGE = 75;
        public const int WAND_MAX_RANGE_MIN = -75;

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, GameLoader.NAMESPACE + ".Items.BuildersWand.Register")]
        public static void Register()
        {
            var planks = new InventoryItem(BuiltinBlocks.Planks, 1);
            var steel = new InventoryItem(BuiltinBlocks.SteelIngot, 1);
            var gold = new InventoryItem(BuiltinBlocks.GoldIngot, 1);
            var silver = new InventoryItem(BuiltinBlocks.SilverIngot, 1);
            var aether = new InventoryItem(Aether.Item.ItemIndex, 4);

            var recipe = new Recipe(Item.name,
                                    new List<InventoryItem>() { planks, aether },
                                    new InventoryItem(Item.ItemIndex, 1),
                                    50);

            //ItemTypesServer.LoadSortOrder(Item.name, GameLoader.GetNextItemSortIndex());
            RecipeStorage.AddOptionalLimitTypeRecipe(Jobs.ApothecaryRegister.JOB_NAME, recipe);
        }


        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, GameLoader.NAMESPACE + ".Items.BuildersWand.Add"), ModLoader.ModCallbackDependsOn("pipliz.blocknpcs.addlittypes")]
        public static void Add(Dictionary<string, ItemTypesServer.ItemTypeRaw> items)
        {
            var name = GameLoader.NAMESPACE + ".BuildersWand";
            var node = new JSONNode();
            node["icon"] = new JSONNode(GameLoader.ICON_FOLDER_PANDA + "/BuildersWand.png");
            node["isPlaceable"] = new JSONNode(false);
            node["maxStackSize"] = new JSONNode(10);
            Item = new ItemTypesServer.ItemTypeRaw(name, node);
            items.Add(name, Item);

            var seclectorName = GameLoader.NAMESPACE + ".AutoLoad.Selector";
            var selector = new JSONNode();
            selector["icon"] = new JSONNode(GameLoader.ICON_FOLDER_PANDA + "/Selector.png");
            selector["isPlaceable"] = new JSONNode(false);
            selector["isDestructible"] = new JSONNode(false);
            selector["mesh"] = new JSONNode(GameLoader.MESH_FOLDER_PANDA + "/Selector.ply");
            selector["destructionTime"] = new JSONNode(int.MaxValue);
            selector["sideall"] = new JSONNode("SELF");

            Selector = new ItemTypesServer.ItemTypeRaw(seclectorName, selector);
            items.Add(seclectorName, Selector);
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnPlayerClicked, GameLoader.NAMESPACE + ".Items.BuildersWand.PlayerClicked")]
        public static void PlayerClicked(Players.Player player, Pipliz.Box<Shared.PlayerClickedData> boxedData)
        {
            if (boxedData.item1.IsConsumed || boxedData.item1.typeSelected != Item.ItemIndex)
                return;

            var click = boxedData.item1;
            Shared.VoxelRayCastHit rayCastHit = click.rayCastHit;
            var ps = PlayerState.GetPlayerState(player);

            if (click.clickType != Shared.PlayerClickedData.ClickType.Right)
            {
                if (ps.BuildersWandPreview.Count != 0)
                {
                    foreach (var pos in ps.BuildersWandPreview)
                        if (World.TryGetTypeAt(pos, out var objType) && objType == Selector.ItemIndex)
                            ServerManager.TryChangeBlock(pos, BuiltinBlocks.Air);

                    ps.BuildersWandPreview.Clear();
                    ps.BuildersWandTarget = BuiltinBlocks.Air;
                }
                else
                {
                    ps.BuildersWandMode = ps.BuildersWandMode.Next();
                    PandaChat.Send(player, $"Wand mode set to {ps.BuildersWandMode}. Charge Left: {ps.BuildersWandCharge}", ChatColor.green);
                }
            }
            else
            {
                if (ps.BuildersWandPreview.Count != 0)
                {
                    var stockpile = Stockpile.GetStockPile(player);

                    foreach (var pos in ps.BuildersWandPreview)
                        if (stockpile.TryRemove(ps.BuildersWandTarget))
                        {
                            ps.BuildersWandCharge--;
                            ServerManager.TryChangeBlock(pos, ps.BuildersWandTarget);
                        }
                        else
                            ServerManager.TryChangeBlock(pos, BuiltinBlocks.Air);

                    ps.BuildersWandPreview.Clear();
                    ps.BuildersWandTarget = BuiltinBlocks.Air;

                    if (ps.BuildersWandCharge <= 0)
                    {
                        var inv = Inventory.GetInventory(player);
                        inv.TryRemove(Item.ItemIndex);
                        ps.BuildersWandCharge = DURABILITY + ps.BuildersWandMaxCharge;
                        PandaChat.Send(player, "Your Builders wand has Run out of energy and turns to dust in your hands.", ChatColor.red);
                    }
                }
                else
                {

                    var startingPos = click.VoxelHit;
                    ps.BuildersWandTarget = click.typeHit;

                    switch (ps.BuildersWandMode)
                    {
                        case WandMode.Horizontal:
                            switch (click.VoxelSideHit)
                            {
                                case VoxelSide.xMin:
                                    startingPos = click.VoxelHit.Add(-1, 0, 0);

                                    for (int i = 0; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(0, 0, i), ps, 1, 0, 0))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(0, 0, i), ps, 1, 0, 0))
                                            break;

                                    break;

                                case VoxelSide.xPlus:
                                    startingPos = click.VoxelHit.Add(1, 0, 0);

                                    for (int i = 0; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(0, 0, i), ps, -1, 0, 0))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(0, 0, i), ps, -1, 0, 0))
                                            break;

                                    break;

                                case VoxelSide.zMin:
                                    startingPos = click.VoxelHit.Add(0, 0, -1);

                                    for (int i = 0; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(i, 0, 0), ps, 0, 0, 1))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(i, 0, 0), ps, 0, 0, 1))
                                            break;

                                    break;

                                case VoxelSide.zPlus:
                                    startingPos = click.VoxelHit.Add(0, 0, 1);

                                    for (int i = 0; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(i, 0, 0), ps, 0, 0, -1))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(0, 0, i), ps, 0, 0, -1))
                                            break;

                                    break;

                                case VoxelSide.yMin:
                                    startingPos = click.VoxelHit.Add(0, -1, 0);

                                    for (int i = 0; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(i, 0, 0), ps, 0, 1, 0))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(i, 0, 0), ps, 0, 1, 0))
                                            break;

                                    for (int i = 1; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(0, 0, i), ps, 0, 1, 0))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(0, 0, i), ps, 0, 1, 0))
                                            break;

                                    break;

                                case VoxelSide.yPlus:
                                    startingPos = click.VoxelHit.Add(0, 1, 0);

                                    for (int i = 0; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(i, 0, 0), ps, 0, -1, 0))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(i, 0, 0), ps, 0, -1, 0))
                                            break;

                                    for (int i = 1; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(0, 0, i), ps, 0, -1, 0))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(0, 0, i), ps, 0, -1, 0))
                                            break;

                                    break;
                            }

                            break;

                        case WandMode.Vertical:
                            switch (click.VoxelSideHit)
                            {
                                case VoxelSide.xMin:
                                    startingPos = click.VoxelHit.Add(-1, 0, 0);

                                    for (int i = 0; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(0, i, 0), ps, 1, 0, 0))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(0, i, 0), ps, 1, 0, 0))
                                            break;

                                    break;

                                case VoxelSide.xPlus:
                                    startingPos = click.VoxelHit.Add(1, 0, 0);

                                    for (int i = 0; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(0, i, 0), ps, -1, 0, 0))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(0, i, 0), ps, -1, 0, 0))
                                            break;

                                    break;

                                case VoxelSide.zMin:
                                    startingPos = click.VoxelHit.Add(0, 0, -1);

                                    for (int i = 0; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(0, i, 0), ps, 0, 0, 1))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(0, i, 0), ps, 0, 0, 1))
                                            break;

                                    break;

                                case VoxelSide.zPlus:
                                    startingPos = click.VoxelHit.Add(0, 0, 1);

                                    for (int i = 0; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(0, i, 0), ps, 0, 0, -1))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(0, i, 0), ps, 0, 0, -1))
                                            break;

                                    break;

                                default:
                                    PandaChat.Send(player, $"Building on top or bottom of a block not valid for wand mode: {ps.BuildersWandMode}.", ChatColor.red);
                                    break;
                            }
                            break;

                        case WandMode.TopAndBottomX:
                            switch (click.VoxelSideHit)
                            {
                                case VoxelSide.yMin:
                                    startingPos = click.VoxelHit.Add(0, -1, 0);

                                    for (int i = 0; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(i, 0, 0), ps, 0, 1, 0))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(i, 0, 0), ps, 0, 1, 0))
                                            break;

                                    break;

                                case VoxelSide.yPlus:
                                    startingPos = click.VoxelHit.Add(0, 1, 0);

                                    for (int i = 0; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(i, 0, 0), ps, 0, -1, 0))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(i, 0, 0), ps, 0, -1, 0))
                                            break;

                                    break;
                                default:
                                    PandaChat.Send(player, $"Building on top or bottom of a block not valid for wand mode: {ps.BuildersWandMode}.", ChatColor.red);
                                    break;
                            }
                            break;

                        case WandMode.TopAndBottomZ:
                            switch (click.VoxelSideHit)
                            {
                                case VoxelSide.yMin:
                                    startingPos = click.VoxelHit.Add(0, -1, 0);

                                    for (int i = 0; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(0, 0, i), ps, 0, 1, 0))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(0, 0, i), ps, 0, 1, 0))
                                            break;

                                    break;

                                case VoxelSide.yPlus:
                                    startingPos = click.VoxelHit.Add(0, 1, 0);

                                    for (int i = 0; i < WAND_MAX_RANGE; i++)
                                        if (Layout(startingPos.Add(0, 0, i), ps, 0, -1, 0))
                                            break;

                                    for (int i = -1; i > WAND_MAX_RANGE_MIN; i--)
                                        if (Layout(startingPos.Add(0, 0, i), ps, 0, -1, 0))
                                            break;

                                    break;
                                default:
                                    PandaChat.Send(player, $"Building on top or bottom of a block not valid for wand mode: {ps.BuildersWandMode}.", ChatColor.red);
                                    break;
                            }
                            break;
                    }
                }
            }
        }

        public static bool Layout(Vector3Int potentialPos, PlayerState ps, int x, int y, int z)
        {
            bool brek = false;

            if (World.TryGetTypeAt(potentialPos.Add(x, y, z), out var itemBehind) && itemBehind != BuiltinBlocks.Air &&
                World.TryGetTypeAt(potentialPos, out var itemInPotentialPos) && itemInPotentialPos == BuiltinBlocks.Air)
            {
                ServerManager.TryChangeBlock(potentialPos, Selector.ItemIndex);
                ps.BuildersWandPreview.Add(potentialPos);
            }
            else
                brek = true;

            return brek;
        }
    }
}