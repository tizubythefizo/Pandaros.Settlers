﻿using BlockTypes;
using NetworkUI;
using NetworkUI.Items;
using Pandaros.Settlers.NBT;
using Pandaros.Settlers.Items;
using Pandaros.Settlers.Research;
using Pipliz;
using Pipliz.JSON;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AreaJobTracker;

namespace Pandaros.Settlers.Jobs.Construction
{
    public enum SchematicClickType
    {
        Build,
        Archetect
    }

    public class SchematicTool : CSType
    {
        public static string NAME = GameLoader.NAMESPACE + ".SchematicTool";
        public override string Name => NAME;
        public override string icon => GameLoader.ICON_PATH + "Schematics.png";
        public override bool? isPlaceable => false;
        public override int? maxStackSize => 1;
        public override StaticItems.StaticItem StaticItemSettings => new StaticItems.StaticItem()
        {
            Name = GameLoader.NAMESPACE + ".SchematicTool",
            RequiredScience = PandaResearch.GetResearchKey("Architect")
        };
    }

    public class SchematicToolResearch : IPandaResearch
    {
        public Dictionary<ushort, int> RequiredItems => new Dictionary<ushort, int>()
        {
            { BuiltinBlocks.ScienceBagColony, 1 },
            { BuiltinBlocks.ScienceBagBasic, 3 },
            { BuiltinBlocks.ScienceBagAdvanced, 1 }
        };

        public int NumberOfLevels => 1;
        public float BaseValue => 0.05f;
        public List<string> Dependancies => new List<string>()
            {
                ColonyBuiltIn.Research.Builder,
                ColonyBuiltIn.Research.ScienceBagAdvanced,
                ColonyBuiltIn.Research.ScienceBagColony
            };

        public int BaseIterationCount => 300;
        public bool AddLevelToName => false;
        public string Name => "Architect";

        public void OnRegister()
        {

        }

        public void ResearchComplete(object sender, ResearchCompleteEventArgs e)
        {
            if (ItemTypes.IndexLookup.IndexLookupTable.TryGetItem(Name, out var item))
                e.Manager.Colony.Stockpile.Add(item.ItemIndex);
        }
    }

    [ModLoader.ModManager]
    public class SchematicMenu
    {
        private static readonly List<Schematic.Rotation> _rotation = new List<Schematic.Rotation>()
        {
            Schematic.Rotation.Front,
            Schematic.Rotation.Right,
            Schematic.Rotation.Back,
            Schematic.Rotation.Left
        };

        private static readonly string Selected_Schematic = GameLoader.NAMESPACE + ".SelectedSchematic";

        private static Dictionary<Players.Player, Tuple<SchematicClickType, string, Schematic.Rotation>> _awaitingClick = new Dictionary<Players.Player, Tuple<SchematicClickType, string, Schematic.Rotation>>();

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnPlayerClicked, GameLoader.NAMESPACE + ".Jobs.Construction.SchematicMenu.OpenMenu")]
        public static void OpenMenu(Players.Player player, Box<PlayerClickedData> boxedData)
        {
            if (ItemTypes.IndexLookup.TryGetIndex(SchematicTool.NAME, out var schematicItem) &&
                boxedData.item1.typeSelected == schematicItem)
            {
                if (player.ActiveColony == null)
                {
                    PandaChat.Send(player, "Cannot open Schematic tool when not in range of a colony.", ChatColor.red);
                    return;
                }

                if (!_awaitingClick.ContainsKey(player))
                {
                    SendMainMenu(player);
                }
                else
                {
                    var tuple = _awaitingClick[player];
                    _awaitingClick.Remove(player);

                    switch (tuple.Item1)
                    {
                        case SchematicClickType.Build:
                            Vector3Int location = boxedData.item1.rayCastHit.voxelHit.Add(0, 1, 0);
                            var args = new JSONNode();
                            args.SetAs("constructionType", GameLoader.NAMESPACE + ".SchematicBuilder");
                            args.SetAs(SchematicBuilderLoader.NAME + ".SchematicName", tuple.Item2);
                            args.SetAs(SchematicBuilderLoader.NAME + ".Rotation", tuple.Item3);

                            if (SchematicReader.TryGetSchematic(tuple.Item2, player.ActiveColony.ColonyID, location, out var schematic))
                            {
                                if (tuple.Item3 >= Schematic.Rotation.Right)
                                    schematic.Rotate();

                                if (tuple.Item3 >= Schematic.Rotation.Back)
                                    schematic.Rotate();

                                if (tuple.Item3 >= Schematic.Rotation.Left)
                                    schematic.Rotate();

                                var maxSize = location.Add(schematic.XMax, schematic.YMax, schematic.ZMax);
                                AreaJobTracker.CreateNewAreaJob("pipliz.constructionarea", args, player.ActiveColony, location, maxSize);
                                AreaJobTracker.SendData(player);
                            }

                            break;

                        case SchematicClickType.Archetect:

                            break;
                    }
                }
            }
        }

        private static void SendMainMenu(Players.Player player)
        {
            NetworkMenu menu = new NetworkMenu();
            menu.LocalStorage.SetAs("header", "Schematic Menu");
            List<FileInfo> options = SchematicReader.GetSchematics(player);

            menu.Items.Add(new DropDown(new LabelData("Schematic", UnityEngine.Color.black), Selected_Schematic, options.Select(fi => fi.Name.Replace(".schematic", "")).ToList()));
            menu.Items.Add(new ButtonCallback(GameLoader.NAMESPACE + ".ShowBuildDetails", new LabelData("Details", UnityEngine.Color.black, UnityEngine.TextAnchor.MiddleCenter)));
            menu.LocalStorage.SetAs(Selected_Schematic, 0);
            menu.Items.Add(new ButtonCallback(GameLoader.NAMESPACE + ".SetArchitectArea", new LabelData("Save Schematic", UnityEngine.Color.black, UnityEngine.TextAnchor.MiddleCenter)));

            NetworkMenuManager.SendServerPopup(player, menu);
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnSendAreaHighlights, GameLoader.NAMESPACE + ".Jobs.Construction.SchematicMenu.OnSendAreaHighlights")]
        static void OnSendAreaHighlights(Players.Player goal, List<AreaHighlight> list, List<ushort> showWhileHoldingTypes)
        {
            showWhileHoldingTypes.Add(BuiltinBlocks.Bed);

            if (ItemTypes.IndexLookup.IndexLookupTable.TryGetItem(SchematicTool.NAME, out var item))
                showWhileHoldingTypes.Add(item.ItemIndex);
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnPlayerPushedNetworkUIButton, GameLoader.NAMESPACE + ".Jobs.Construction.SchematicMenu.PressButton")]
        public static void PressButton(ButtonPressCallbackData data)
        {
            switch (data.ButtonIdentifier)
            {
                case GameLoader.NAMESPACE + ".SetArchitectArea":
                    
                    NetworkMenuManager.CloseServerPopup(data.Player);
                    break;

                case GameLoader.NAMESPACE + ".ShowMainMenu":
                    SendMainMenu(data.Player);
                    break;

                case GameLoader.NAMESPACE + ".ShowBuildDetails":
                    List<FileInfo> options = SchematicReader.GetSchematics(data.Player);
                    var index = data.Storage.GetAs<int>(Selected_Schematic);

                    if (options.Count > index)
                    {
                        var selectedSchematic = options[index];

                        if (SchematicReader.TryGetSchematicMetadata(selectedSchematic.Name, data.Player.ActiveColony.ColonyID, out SchematicMetadata schematicMetadata))
                        {
                            if (schematicMetadata.Blocks.Count == 1 && schematicMetadata.Blocks.ContainsKey(BuiltinBlocks.Air))
                                PandaChat.Send(data.Player, "Unable to validate schematic. Schematic is all air. Cannot place area.", ChatColor.red);
                            {
                                NetworkMenu menu = new NetworkMenu();
                                menu.Width = 600;
                                menu.Height = 600;
                                menu.LocalStorage.SetAs("header", selectedSchematic.Name + " Details");

                                menu.Items.Add(new Label(new LabelData("Height: " + schematicMetadata.MaxY, UnityEngine.Color.black)));
                                menu.Items.Add(new Label(new LabelData("Width: " + schematicMetadata.MaxZ, UnityEngine.Color.black)));
                                menu.Items.Add(new Label(new LabelData("Length: " + schematicMetadata.MaxX, UnityEngine.Color.black)));
                                menu.LocalStorage.SetAs(Selected_Schematic, selectedSchematic.Name);

                                foreach (var kvp in schematicMetadata.Blocks)
                                {
                                    var item = ItemTypes.GetType(kvp.Key);

                                    List<IItem> items = new List<IItem>();
                                    items.Add(new ItemIcon(kvp.Key));
                                    items.Add(new Label(new LabelData(item.Name, UnityEngine.Color.black, UnityEngine.TextAnchor.MiddleLeft, 18, LabelData.ELocalizationType.Type)));
                                    items.Add(new Label(new LabelData(" x " + kvp.Value.Count, UnityEngine.Color.black)));
                                    menu.Items.Add(new HorizontalGrid(items, 200));
                                }

                                menu.Items.Add(new DropDown(new LabelData("Rotation", UnityEngine.Color.black), Selected_Schematic + ".Rotation", _rotation.Select(r => r.ToString()).ToList()));
                                menu.Items.Add(new HorizontalSplit(new ButtonCallback(GameLoader.NAMESPACE + ".ShowMainMenu", new LabelData("Back", UnityEngine.Color.black, UnityEngine.TextAnchor.MiddleCenter)),
                                                                   new ButtonCallback(GameLoader.NAMESPACE + ".SetBuildArea", new LabelData("Build", UnityEngine.Color.black, UnityEngine.TextAnchor.MiddleCenter))));
                                menu.LocalStorage.SetAs(Selected_Schematic + ".Rotation", 0);

                                NetworkMenuManager.SendServerPopup(data.Player, menu);
                            }
                        }
                    }

                    break;

                case GameLoader.NAMESPACE + ".SetBuildArea":
                    var scem = data.Storage.GetAs<string>(Selected_Schematic);
                    var rotation = data.Storage.GetAs<int>(Selected_Schematic + ".Rotation");

                    PandaLogger.Log("Schematic: {0}", scem);

                    if (SchematicReader.TryGetSchematicMetadata(scem, data.Player.ActiveColony.ColonyID, out SchematicMetadata metadata))
                    {
                        if (metadata.Blocks.Count == 1 && metadata.Blocks.ContainsKey(BuiltinBlocks.Air))
                            PandaChat.Send(data.Player, "Unable to validate schematic. Schematic is all air. Cannot place area.", ChatColor.red);
                        {
                            _awaitingClick[data.Player] = Tuple.Create(SchematicClickType.Build, scem, _rotation[rotation]);
                            PandaChat.Send(data.Player, "Right click on the top of a block to place the scematic. This will be the front left corner.");
                            NetworkMenuManager.CloseServerPopup(data.Player);
                        }
                    }
        
                    break;
            }
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnSendAreaHighlights, GameLoader.NAMESPACE + ".Jobs.Construction.SchematicMenu.AreaHighlighted")]
        public static void AreaHighlighted(Players.Player player, List<AreaJobTracker.AreaHighlight> list, List<ushort> showWhileHoldingTypes)
        {
            if (ItemTypes.IndexLookup.TryGetIndex(SchematicTool.NAME, out var schematicItem))
                showWhileHoldingTypes.Add(schematicItem);
        }
    }
}
