﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.GridAi;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;
using static WeaponCore.Support.WeaponDefinition.TargetingDef.BlockTypes;
using static WeaponCore.CompStateValues;
namespace WeaponCore
{
    public partial class Session
    {
        internal bool UpdateLocalAiAndCockpit()
        {
            InGridAiBlock = false;
            ActiveControlBlock = ControlledEntity as MyCubeBlock;
           
            var cockPit = ControlledEntity as MyCockpit;
            if (cockPit != null && cockPit.EnableShipControl)
                ActiveCockPit = cockPit;
            else ActiveCockPit = null;
            
            long oldBlockId;
            var activeBlock = ActiveCockPit ?? ActiveControlBlock;
            if (activeBlock != null && ActiveControlBlock != null && GridToMasterAi.TryGetValue(activeBlock.CubeGrid, out TrackingAi))
            {
                InGridAiBlock = true;
                TrackingAi.Data.Repo.ControllingPlayers.TryGetValue(PlayerId, out oldBlockId);

                if (IsServer) TrackingAi.Construct.UpdateConstructsPlayers(ActiveControlBlock, PlayerId, true);
                if (HandlesInput && oldBlockId != ActiveControlBlock.EntityId)
                    SendActiveControlUpdate(TrackingAi, activeBlock, true);
            }
            else
            {
                if (TrackingAi != null)
                {
                    TrackingAi.Construct.Focus.ClientIsFocused(TrackingAi);

                    MyCubeBlock oldBlock;
                    if (TrackingAi.Data.Repo.ControllingPlayers.TryGetValue(PlayerId, out oldBlockId) && MyEntities.TryGetEntityById(oldBlockId, out oldBlock, true)) {

                        if (IsServer) TrackingAi.Construct.UpdateConstructsPlayers(ActiveControlBlock, PlayerId, false);
                        if (HandlesInput)
                            SendActiveControlUpdate(TrackingAi, oldBlock, false);
                    }
                }

                TrackingAi = null;
                ActiveCockPit = null;
                ActiveControlBlock = null;
            }
            return InGridAiBlock;
        }

        internal void EntityControlUpdate()
        {
            var lastControlledEnt = ControlledEntity;
            ControlledEntity = (MyEntity)MyAPIGateway.Session.ControlledObject;

            var entityChanged = lastControlledEnt != null && lastControlledEnt != ControlledEntity;

            if (entityChanged)
            {
                if (lastControlledEnt is MyCockpit || lastControlledEnt is MyRemoteControl)
                    PlayerControlAcquired(lastControlledEnt);

                if (ControlledEntity is MyCockpit || ControlledEntity is MyRemoteControl)
                    PlayerControlNotify(ControlledEntity);

                if (ControlledEntity is IMyGunBaseUser && !(lastControlledEnt is IMyGunBaseUser))
                {
                    var cube = (MyCubeBlock)ControlledEntity;
                    GridAi gridAi;
                    if (GridTargetingAIs.TryGetValue(cube.CubeGrid, out gridAi))
                    {
                        WeaponComponent comp;
                        if (gridAi.WeaponBase.TryGetValue(cube, out comp))
                        {
                            GunnerBlackList = true;
                            if (IsServer)
                            {
                                comp.Data.Repo.Base.State.PlayerId = PlayerId;
                                comp.Data.Repo.Base.State.Control = ControlMode.Camera;
                            }
                            ActiveControlBlock = (MyCubeBlock)ControlledEntity;
                            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, PlayerId, false);
                            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, PlayerId, false);
                            var controlStringMenu = MyAPIGateway.Input.GetControl(UiInput.MouseButtonMenu).GetGameControlEnum().String;
                            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMenu, PlayerId, false);

                            if (HandlesInput && MpActive)
                                SendPlayerControlRequest(comp, PlayerId, ControlMode.Camera);
                        }
                    }
                }
                else if (!(ControlledEntity is IMyGunBaseUser) && lastControlledEnt is IMyGunBaseUser)
                {
                    if (GunnerBlackList)
                    {
                        GunnerBlackList = false;
                        var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, PlayerId, true);
                        var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, PlayerId, true);
                        var controlStringMenu = MyAPIGateway.Input.GetControl(UiInput.MouseButtonMenu).GetGameControlEnum().String;
                        MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMenu, PlayerId, true);
                        var oldCube = lastControlledEnt as MyCubeBlock;
                        GridAi gridAi;
                        if (oldCube != null && GridTargetingAIs.TryGetValue(oldCube.CubeGrid, out gridAi))
                        {
                            WeaponComponent comp;
                            if (gridAi.WeaponBase.TryGetValue(oldCube, out comp))
                            {
                                if (IsServer)
                                {
                                    comp.Data.Repo.Base.State.PlayerId = -1;
                                    comp.Data.Repo.Base.State.Control = ControlMode.None;

                                }

                                if (HandlesInput && MpActive)
                                    SendPlayerControlRequest(comp, -1, ControlMode.None);

                                ActiveControlBlock = null;
                            }
                        }
                    }
                }
            }
        }

        private void FovChanged()
        {
            HudUi.NeedsUpdate = true;
            TargetUi.ResetCache();
        }

        private void ShowClientNotify(ClientNotifyPacket notify)
        {
            MyAPIGateway.Utilities.ShowNotification(notify.Message, notify.Duration > 0 ? notify.Duration : 1000, notify.Color == string.Empty ? "White" : notify.Color);
        }

        private readonly Color _restrictionAreaColor = new Color(128, 0, 128, 96);
        private readonly Color _uninitializedColor = new Color(255, 0, 0, 200);
        private BoundingSphereD _nearbyGridsTestSphere = new BoundingSphereD(Vector3D.Zero, 350);
        private readonly List<MyEntity> _gridsNearCamera = new List<MyEntity>();
        private readonly List<MyCubeBlock> _uninitializedBlocks = new List<MyCubeBlock>();
        private void DrawDisabledGuns()
        {
            if (Tick600 || Tick60 && QuickDisableGunsCheck) {

                QuickDisableGunsCheck = false;
                _nearbyGridsTestSphere.Center = CameraPos;
                _gridsNearCamera.Clear();
                _uninitializedBlocks.Clear();

                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref _nearbyGridsTestSphere, _gridsNearCamera);
                for (int i = _gridsNearCamera.Count - 1; i >= 0; i--)
                {
                    var grid = _gridsNearCamera[i] as MyCubeGrid;
                    if (grid?.Physics != null && !grid.MarkedForClose && !grid.IsPreview && !grid.Physics.IsPhantom) {
                        
                        var fatBlocks = grid.GetFatBlocks();
                        for (int j = 0; j < fatBlocks.Count; j++) {
                            
                            var block = fatBlocks[j];
                            if (block.IsFunctional && WeaponPlatforms.ContainsKey(block.BlockDefinition.Id)) {

                                GridAi gridAi;
                                if (!GridTargetingAIs.TryGetValue(block.CubeGrid, out gridAi) || !gridAi.WeaponBase.ContainsKey(block)) 
                                    _uninitializedBlocks.Add(block);
                            }
                        }
                    }

                }
            }

            for (int i = 0; i < _uninitializedBlocks.Count; i++) {
                
                var badBlock = _uninitializedBlocks[i];
                if (badBlock.InScene) {

                    var lookSphere = new BoundingSphereD(badBlock.PositionComp.WorldAABB.Center, 30f);
                    if (Camera.IsInFrustum(ref lookSphere)) {
                        MyOrientedBoundingBoxD blockBox;
                        SUtils.GetBlockOrientedBoundingBox(badBlock, out blockBox);
                        DsDebugDraw.DrawBox(blockBox, _uninitializedColor);
                    }
                }
            }
        }

        private void UpdatePlacer()
        {
            if (!Placer.Visible) Placer = null;
            if (!MyCubeBuilder.Static.DynamicMode && MyCubeBuilder.Static.HitInfo.HasValue)
            {
                var hit = MyCubeBuilder.Static.HitInfo.Value as IHitInfo;
                var grid = hit.HitEntity as MyCubeGrid;
                GridAi gridAi;
                if (grid != null && GridToMasterAi.TryGetValue(grid, out gridAi))
                {
                    if (MyCubeBuilder.Static.CurrentBlockDefinition != null)
                    {
                        var subtypeIdHash = MyCubeBuilder.Static.CurrentBlockDefinition.Id.SubtypeId;
                        WeaponCount weaponCount;
                        if (gridAi.WeaponCounter.TryGetValue(subtypeIdHash, out weaponCount))
                        {
                            if (weaponCount.Max > 0 && gridAi.Construct.GetWeaponCount(subtypeIdHash) >= weaponCount.Max)
                            {
                                MyCubeBuilder.Static.NotifyPlacementUnable();
                                MyCubeBuilder.Static.Deactivate();
                                return;
                            }
                        }

                        if (WeaponAreaRestrictions.ContainsKey(subtypeIdHash))
                        {
                            MyOrientedBoundingBoxD restrictedBox;
                            MyOrientedBoundingBoxD buildBox = MyCubeBuilder.Static.GetBuildBoundingBox();
                            BoundingSphereD restrictedSphere;
                            if (IsWeaponAreaRestricted(subtypeIdHash, buildBox, grid, 0, null, out restrictedBox, out restrictedSphere))
                            {
                                DsDebugDraw.DrawBox(buildBox, _uninitializedColor);
                            }

                            if (MyAPIGateway.Session.Config.HudState == 1)
                            {
                                if (restrictedBox.HalfExtent.AbsMax() > 0)
                                {
                                    DsDebugDraw.DrawBox(restrictedBox, _restrictionAreaColor);
                                }
                                if (restrictedSphere.Radius > 0)
                                {
                                    DsDebugDraw.DrawSphere(restrictedSphere, _restrictionAreaColor);
                                }
                                for (int i = 0; i < gridAi.Weapons.Count; i++)
                                {
                                    MyOrientedBoundingBoxD b;
                                    BoundingSphereD s;
                                    WeaponComponent Comp = gridAi.Weapons[i];
                                    MyOrientedBoundingBoxD blockBox;
                                    SUtils.GetBlockOrientedBoundingBox(Comp.MyCube, out blockBox);

                                    CalculateRestrictedShapes(Comp.MyCube.BlockDefinition.Id.SubtypeId, blockBox, out b, out s);
                                    
                                    if (s.Radius > 0)
                                    {
                                        DsDebugDraw.DrawSphere(s, _restrictionAreaColor);
                                    }
                                    if (b.HalfExtent.AbsMax() > 0)
                                    {
                                        DsDebugDraw.DrawBox(b, _restrictionAreaColor);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        internal void TargetSelection()
        {
            if ((UiInput.AltPressed && UiInput.ShiftReleased || TargetUi.DrawReticle && UiInput.ClientInputState.MouseButtonRight) && InGridAiBlock)
                TrackingAi.Construct.Focus.RequestReleaseActive(TrackingAi);

            if (UiInput.ActionKeyReleased && TrackingAi.Construct.Data.Repo.FocusData.HasFocus && InGridAiBlock)
                TrackingAi.Construct.Focus.RequestAddLock(TrackingAi);

            if (InGridAiBlock)
            {
                if ((TargetUi.DrawReticle || UiInput.FirstPersonView) && MyAPIGateway.Input.IsNewLeftMouseReleased())
                    TargetUi.SelectTarget();
                else
                {
                    if (UiInput.CurrentWheel != UiInput.PreviousWheel)
                        TargetUi.SelectNext();
                    else if (UiInput.LongShift || UiInput.ShiftReleased && !UiInput.LongShift) 
                        TrackingAi.Construct.Focus.RequestNextActive(UiInput.LongShift, TrackingAi);
                }
            }
        }

        internal void RemoveGps()
        {
            if (TargetGps != null)
            {
                if (TargetGps.ShowOnHud)
                {
                    MyAPIGateway.Session.GPS.RemoveLocalGps(TargetGps);
                    TargetGps.ShowOnHud = false;
                }

            }
        }

        internal void AddGps(Color color = default(Color))
        {
            if (TargetGps != null)
            {
                if (!TargetGps.ShowOnHud)
                {
                    TargetGps.ShowOnHud = true;
                    MyAPIGateway.Session.GPS.AddLocalGps(TargetGps);
                    if (color != default(Color))
                        MyVisualScriptLogicProvider.SetGPSColor(TargetGps?.Name, color);
                }
            }
        }

        internal void SetGpsInfo(Vector3D pos, string name, double dist = 0, Color color = default(Color))
        {
            if (TargetGps != null)
            {
                var newPos = dist > 0 ? pos + (Camera.WorldMatrix.Up * dist) : pos;
                TargetGps.Coords = newPos;
                TargetGps.Name = name;
                if (color != default(Color))
                    MyVisualScriptLogicProvider.SetGPSColor(TargetGps?.Name, color);
            }
        }

        internal bool CheckTarget(GridAi ai)
        {
            if (!ai.Construct.Focus.ClientIsFocused(ai)) return false;

            if (ai != TrackingAi)
            {
                TrackingAi = null;
                return false;
            }

            return ai.Construct.Data.Repo.FocusData.HasFocus;
        }

        internal void SetTarget(MyEntity entity, GridAi ai, Dictionary<MyEntity, float> masterTargets)
        {
            
            TrackingAi = ai;
            ai.Construct.Focus.RequestAddFocus(entity, ai);

            GridAi gridAi;
            TargetArmed = false;
            var grid = entity as MyCubeGrid;
            if (grid != null && GridToMasterAi.TryGetValue(grid, out gridAi))
            {
                TargetArmed = true;
            }
            else {

                float offenseRating;
                if (!masterTargets.TryGetValue(entity, out offenseRating)) return;
                ConcurrentDictionary<BlockTypes, ConcurrentCachingList<MyCubeBlock>> typeDict;

                var tGrid = entity as MyCubeGrid;
                if (tGrid != null && GridToBlockTypeMap.TryGetValue(tGrid, out typeDict)) {

                    ConcurrentCachingList<MyCubeBlock> fatList;
                    if (typeDict.TryGetValue(Offense, out fatList))
                        TargetArmed = fatList.Count > 0;
                    else TargetArmed = false;
                }
                else TargetArmed = false;
            }
        }
    }
}
