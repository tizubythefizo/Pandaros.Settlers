﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MeshedObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pandaros.Settlers.Items;
using Pandaros.Settlers.Items.ConnectedBlocks;
using Pandaros.Settlers.Items.Transportation;
using Pandaros.Settlers.Jobs.Roaming;
using Pandaros.Settlers.Models;
using Pandaros.Settlers.Server;
using Pipliz.Collections;
using Shared;
using Transport;
using UnityEngine;

namespace Pandaros.Settlers.Transportation
{
    public class TrainTransport : TransportManager.ITransportVehicle
    {
        public int Delay { get; set; } = 1000;
        public float ManaCostPerBlock { get; set; } = .001f;
        public ICSType TrainType { get; set; }

        MeshedVehicleDescription _meshedVehicleDescription;
        AnimationManager.AnimatedObject _animatedObject;
        Players.Player _player;
        Vector3 _position;
        Vector3 _prevPosition;
        ItemId _trainId;
        int _idealHeightFromTrack = 3;
        Pipliz.Vector3Int _trackPosition = Pipliz.Vector3Int.zero;
        static TrackCalculationType _trackCalculationType = new TrackCalculationType();
        float _energy = 1f;
        double _trainMoveTime;
        double _minStopNextTime;
        bool _removed;

        public static bool TryCreateFromSave(TransportSave transportSave, out TrainTransport trainTransport)
        {
            trainTransport = null;

            if (transportSave.type == _trackCalculationType.name && 
                Train.TrainAnimations.ContainsKey(transportSave.itemName) && 
                Train.TrainTypes.ContainsKey(transportSave.itemName))
            {
                trainTransport = new TrainTransport(transportSave);
                return true;
            }

            return false;
        }

        public TrainTransport(TransportSave transportSave)
        {
            if (transportSave.type == _trackCalculationType.name)
            {
                _position = transportSave.position;
                _prevPosition = transportSave.prevPos;
                _trainId = transportSave.itemName;
                _trackPosition = transportSave.trackPos;
                TrainType = Train.TrainTypes[transportSave.itemName];
                _animatedObject = Train.TrainAnimations[transportSave.itemName];
                _meshedVehicleDescription = new MeshedVehicleDescription(new ClientMeshedObject(_animatedObject.ObjType, new MeshedObjectID(transportSave.meshid)), TrainType.TrainConfiguration.playerSeatOffset, TrainType.TrainConfiguration.AllowPlayerToEditBlocksWhileRiding);
                _idealHeightFromTrack = TrainType.TrainConfiguration.IdealHeightFromTrack;
                _energy = transportSave.energy;
                Delay = TrainType.TrainConfiguration.MoveTimePerBlockMs;
                ManaCostPerBlock = TrainType.TrainConfiguration.ManaCostPerBlock;

                if (!string.IsNullOrEmpty(transportSave.player))
                {
                    _player = Players.GetPlayer(NetworkID.Parse(transportSave.player));
                    MeshedObjectManager.Attach(_player, _meshedVehicleDescription);
                }
            }
        }
       
        public TrainTransport(Vector3 position, AnimationManager.AnimatedObject animatedObject, ICSType trainType)
        {
            _meshedVehicleDescription = new MeshedVehicleDescription(new ClientMeshedObject(animatedObject.ObjType), trainType.TrainConfiguration.playerSeatOffset, trainType.TrainConfiguration.AllowPlayerToEditBlocksWhileRiding);
            _animatedObject = animatedObject;
            _position = position;
            TrainType = trainType;
            _trainId = ItemId.GetItemId(trainType.name);
            _idealHeightFromTrack = TrainType.TrainConfiguration.IdealHeightFromTrack;
            Delay = TrainType.TrainConfiguration.MoveTimePerBlockMs;
            ManaCostPerBlock = TrainType.TrainConfiguration.ManaCostPerBlock;
        }

        public int GetDelayMillisecondsToNextUpdate()
        {
            return Delay;
        }

        public bool MatchesMeshID(int id)
        {
            return _meshedVehicleDescription.Object.ObjectID.ID == id;
        }

        public void OnClicked(Players.Player sender, PlayerClickedData click)
        {
            int countConnected = Players.CountConnected;
            Players.Player player = null;

            for (int index = 0; index < countConnected; ++index)
            {
                Players.Player connectedByIndex = Players.GetConnectedByIndex(index);
                MeshedVehicleDescription description;

                if (connectedByIndex != null && 
                    MeshedObjectManager.TryGetVehicle(connectedByIndex, out description) && 
                    description.Object.ObjectID.ID == _meshedVehicleDescription.Object.ObjectID.ID)
                    player = connectedByIndex;
            }

            if (click.ClickType == PlayerClickedData.EClickType.Right)
            {
                if (player == null)
                {
                    _player = sender;
                    MeshedObjectManager.Attach(sender, _meshedVehicleDescription);
                }
                else
                {
                    _player = null;
                    MeshedObjectManager.Detach(player);
                }
            }
            else
            {
                if (click.ClickType != PlayerClickedData.EClickType.Left ||
                    player != null)
                    return;

                if (Train.TrainTransports.TryGetValue(TrainType.ConnectedBlock.BlockType, out var trainTransportsList) && trainTransportsList.Contains(this))
                    trainTransportsList.Remove(this);

                _removed = true;
                _meshedVehicleDescription.Object.SendRemoval(_position, _animatedObject.ObjSettings);
                sender.Inventory.TryAdd(_trainId, 1, -1, true);
            }
        }

        public void ProcessInputs(Players.Player player, Pipliz.Collections.SortedList<EInputKey, float> keyTimes, float deltaTime)
        {
            
        }

        public JObject Save()
        {
            TransportSave transportSave = new TransportSave();
            transportSave.meshid = _meshedVehicleDescription.Object.ObjectID.ID;
            transportSave.type = _trackCalculationType.name;
            transportSave.BlockType = TrainType.ConnectedBlock.BlockType;
            transportSave.position = new SerializableVector3(_position);
            transportSave.prevPos = new SerializableVector3(_prevPosition);
            transportSave.trackPos = new SerializableVector3(_trackPosition);
            transportSave.itemName = _trainId.Name;
            transportSave.energy = _energy;

            if (_player != null)
                transportSave.player = _player.ID.ToString();

            return JObject.FromObject(transportSave);
        }

        public TransportManager.ETransportUpdateResult Update()
        {
            if (_removed)
                return TransportManager.ETransportUpdateResult.Remove;

            var currentPositionInt = new Pipliz.Vector3Int(_position);
            var heightFromTrack = _idealHeightFromTrack;

            if (_trackPosition == Pipliz.Vector3Int.zero)
                for(int i = -1; i > _idealHeightFromTrack * -1; i--)
                {
                    var trackPos = currentPositionInt.Add(0, i, 0);
                    if (World.TryGetTypeAt(trackPos, out ItemTypes.ItemType possibleTrack) &&
                        ConnectedBlockSystem.BlockLookup.TryGetValue(possibleTrack.Name, out var track) &&
                        track.ConnectedBlock.CalculationType == _trackCalculationType.name && 
                        TrainType.ConnectedBlock.BlockType == track.ConnectedBlock.BlockType)
                    {
                        heightFromTrack = i * -1;
                        _trackPosition = trackPos;
                        break; 
                    }
                }

            if (heightFromTrack != _idealHeightFromTrack)
            {
                _position = currentPositionInt.Add(0, heightFromTrack, 0).Vector;
                _meshedVehicleDescription.Object.SendMoveToInterpolated(_position, Quaternion.identity, (float)GetDelayMillisecondsToNextUpdate() / 1000f, _animatedObject.ObjSettings);
            }
            else if (_trackPosition != Pipliz.Vector3Int.zero)
            {
                bool moved = false;
                ICSType trainStation = null;

                if (_trainMoveTime < TimeCycle.TotalHours)
                {
                    if (_minStopNextTime < TimeCycle.TotalHours)
                        foreach (var stationSide in _trackCalculationType.AvailableBlockSides)
                        {
                            var stationCheck = _trackPosition.GetBlockOffset(stationSide);

                            if (World.TryGetTypeAt(stationCheck, out ItemTypes.ItemType possibleStation) &&
                                ItemCache.CSItems.TryGetValue(possibleStation.Name, out var station) &&
                                station.TrainStationSettings != null &&
                                station.TrainStationSettings.BlockType == TrainType.ConnectedBlock.BlockType)
                            {
                                trainStation = station;

                                foreach (var kvp in RoamingJobManager.Objectives.Values)
                                {
                                    if (kvp.TryGetValue(trainStation.TrainStationSettings.ObjectiveCategory, out var locDic) &&
                                        locDic.TryGetValue(stationCheck, out var roamingJobState))
                                    {
                                        var manaNeeded = RoamingJobState.DEFAULT_MAX - _energy;

                                        var existing = roamingJobState.GetActionEnergy(GameLoader.NAMESPACE + ".ManaTankRefill");

                                        if (existing > 0)
                                        {
                                            if (existing >= manaNeeded)
                                            {
                                                roamingJobState.SubtractFromActionEnergy(GameLoader.NAMESPACE + ".ManaTankRefill", manaNeeded);
                                                _energy = RoamingJobState.DEFAULT_MAX;
                                            }
                                            else
                                            {
                                                roamingJobState.SubtractFromActionEnergy(GameLoader.NAMESPACE + ".ManaTankRefill", existing);
                                                _energy += existing;
                                            }

                                            Indicator.SendIconIndicatorNear(stationCheck.Add(0, 1, 0).Vector, new IndicatorState(10, SettlersBuiltIn.ItemTypes.MANA.Name));
                                            _minStopNextTime = TimeCycle.TotalHours + 2;
                                            _trainMoveTime = TimeCycle.TotalHours + 1;
                                            existing = roamingJobState.GetActionEnergy(GameLoader.NAMESPACE + ".ManaTankRefill");
                                        }
                                        else
                                        {
                                            Indicator.SendIconIndicatorNear(stationCheck.Add(0, 1, 0).Vector, new IndicatorState(10, SettlersBuiltIn.ItemTypes.MANA.Name, true, false));
                                        }
                                        break;
                                    }
                                }
                            }
                        }

                    if (trainStation == null && _energy > 0)
                        foreach (var side in _trackCalculationType.AvailableBlockSides)
                        {
                            var searchSide = _trackPosition.GetBlockOffset(side);
                            var proposePos = currentPositionInt.GetBlockOffset(side).Vector;

                            if (World.TryGetTypeAt(searchSide, out ItemTypes.ItemType possibleTrack) &&
                                ConnectedBlockSystem.BlockLookup.TryGetValue(possibleTrack.Name, out var track) &&
                                track.ConnectedBlock.CalculationType == _trackCalculationType.name &&
                                TrainType.ConnectedBlock.BlockType == track.ConnectedBlock.BlockType &&
                                proposePos != _prevPosition)
                            {
                                _prevPosition = _position;
                                _trackPosition = searchSide;
                                _position = currentPositionInt.GetBlockOffset(side).Vector;
                                _meshedVehicleDescription.Object.SendMoveToInterpolated(_position, Quaternion.identity, (float)GetDelayMillisecondsToNextUpdate() / 1000f, _animatedObject.ObjSettings);
                                _energy -= ManaCostPerBlock;

                                if (_energy < 0)
                                    _energy = 0;

                                moved = true;
                                break;
                            }
                        }
                }
                
                if (!moved)
                    _meshedVehicleDescription.Object.SendMoveToInterpolated(_position, Quaternion.identity, (float)GetDelayMillisecondsToNextUpdate() / 1000f, _animatedObject.ObjSettings);

                if (!moved && _energy > 0)
                    _prevPosition = Vector3.zero;

                if (!moved && _energy <= 0)
                {
                    _trainMoveTime = 0;
                    Indicator.SendIconIndicatorNear(new Pipliz.Vector3Int(_position).Add(0, 2, 0).Vector, new IndicatorState((float)GetDelayMillisecondsToNextUpdate() / 1000f, SettlersBuiltIn.ItemTypes.MANA.Name, true, false));
                }
            }

            return TransportManager.ETransportUpdateResult.KeepUpdating;
        }
    }
}