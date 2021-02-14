﻿using System;
using Jakaria;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

namespace CoreSystems.Support
{
    public partial class Ai
    {
        internal void RequestDbUpdate()
        {
            if (TopEntity == null)
                return;
            
            using (TopEntity.Pin()) {
                if (TopEntity.MarkedForClose || !TopEntity.InScene)
                    return;

                if (IsGrid) {
                    var bigOwners = GridEntity.BigOwners;
                    AiOwner = bigOwners.Count > 0 ? bigOwners[0] : 0;
                }
            }

            ScanInProgress = true;

            GridVolume = TopEntity.PositionComp.WorldVolume;
            ScanVolume = GridVolume;
            ScanVolume.Radius = MaxTargetingRange;
            Session.DbsToUpdate.Add(new DbScan {Ai = this, Version = Version});
            TargetsUpdatedTick = Session.Tick;
        }

        internal void Scan()
        {
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref ScanVolume, _possibleTargets);
            NearByEntitiesTmp = _possibleTargets.Count;

            foreach (var grid in PrevSubGrids)
                RemSubGrids.Add((MyCubeGrid) grid);
            PrevSubGrids.Clear();
            for (int i = 0; i < NearByEntitiesTmp; i++) {

                var ent = _possibleTargets[i];
                using (ent.Pin()) {

                    if (ent is MyVoxelBase || ent.Physics == null || ent is MyFloatingObject || ent.MarkedForClose || !ent.InScene || ent.IsPreview || ent.Physics.IsPhantom) continue;

                    var grid = ent as MyCubeGrid;
                    if (grid != null && IsGrid && GridEntity.IsSameConstructAs(grid)) {
                        PrevSubGrids.Add(grid);
                        continue;
                    }

                    MyDetectedEntityInfo entInfo;
                    if (!CreateEntInfo(ent, AiOwner, out entInfo))
                    {
                        continue;
                    }

                    switch (entInfo.Relationship) {
                        case MyRelationsBetweenPlayerAndBlock.Owner:
                        case MyRelationsBetweenPlayerAndBlock.FactionShare:
                        case MyRelationsBetweenPlayerAndBlock.Friends:
                            continue;
                    }

                    if (grid != null) {

                        GridMap gridMap;
                        if (!Session.GridToInfoMap.TryGetValue(grid, out gridMap) || gridMap.Trash)
                            continue;

                        var allFat = gridMap.MyCubeBocks;
                        var fatCount = allFat.Count;

                        if (fatCount <= 0)
                            continue;

                        if (fatCount <= 20)  { // possible debris

                            var valid = false;
                            for (int j = 0; j < fatCount; j++) {
                                var fat = allFat[j];
                                if (fat is IMyTerminalBlock && fat.IsWorking) {
                                    valid = true;
                                    break;
                                }
                            }
                            if (!valid) continue;
                        }

                        int partCount;
                        Ai targetAi;
                        if (Session.GridAIs.TryGetValue(grid, out targetAi)) {
                            targetAi.TargetAisTmp.Add(this);
                            TargetAisTmp.Add(targetAi);
                            partCount = targetAi.Construct.BlockCount;
                        }
                        else 
                            partCount = gridMap.MostBlocks;

                        NewEntities.Add(new DetectInfo(Session, ent, entInfo, partCount, fatCount));
                        ValidGrids.Add(ent);
                    }
                    else NewEntities.Add(new DetectInfo(Session, ent, entInfo, 1, 0));
                }
            }
            FinalizeTargetDb();
            SubGridDetect();
        }

        private void FinalizeTargetDb()
        {
            MyPlanetTmp = MyGamePruningStructure.GetClosestPlanet(ScanVolume.Center);
            ObstructionsTmp.Clear();
            StaticsInRangeTmp.Clear();
            for (int i = 0; i < NearByEntitiesTmp; i++) {

                var ent = _possibleTargets[i];
                if (Session.ShieldApiLoaded && ent.DefinitionId?.SubtypeId == Session.ShieldHash && ent.Render.Visible) {

                    var shieldblock = Session.SApi.MatchEntToShieldFast(ent, false);
                    if (shieldblock != null)
                        NearByShieldsTmp.Add(new Shields { Id = ent.Hierarchy.ChildId, ShieldEnt = ent, ShieldBlock = (MyCubeBlock)shieldblock });
                }
                var voxel = ent as MyVoxelBase;
                var grid = ent as MyCubeGrid;
                var safeZone = ent as MySafeZone;

                var blockingThings = safeZone != null || ent.Physics != null && grid != null || voxel != null && voxel == voxel.RootVoxel;
                if (!blockingThings || voxel != null && (voxel.RootVoxel is MyPlanet || voxel.PositionComp.LocalVolume.Radius < 15)) continue;

                if (voxel != null || safeZone != null || ent.Physics.IsStatic)
                    StaticsInRangeTmp.Add(ent);

                GridMap map;
                if (grid != null && (PrevSubGrids.Contains(grid) || ValidGrids.Contains(ent) || grid.PositionComp.LocalVolume.Radius < 10 || Session.GridToInfoMap.TryGetValue(grid, out map) && map.Trash || grid.BigOwners.Count == 0) ) continue;

                ObstructionsTmp.Add(ent);
            }
            ValidGrids.Clear();
            _possibleTargets.Clear();
        }


        internal void NearByShield()
        {
            NearByFriendlyShields.Clear();
            for (int i = 0; i < NearByShieldsTmp.Count; i++) {

                var shield = NearByShieldsTmp[i];
                var shieldGrid = MyEntities.GetEntityByIdOrDefault(shield.Id) as MyCubeGrid;
                
                if (shieldGrid != null) {

                    if (shield.Id == TopEntity.EntityId || IsGrid && GridEntity.IsSameConstructAs(shieldGrid))  {
                        MyShield = shield.ShieldEnt;
                    }
                    else {
                        TargetInfo info;
                        var found = Targets.TryGetValue(shield.ShieldBlock.CubeGrid, out info);
                        var relation = found ? info.EntInfo.Relationship : shield.ShieldBlock.IDModule.GetUserRelationToOwner(AiOwner);
                        var friendly = relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare || relation == MyRelationsBetweenPlayerAndBlock.Friends;
                        
                        if (friendly) {
                            NearByFriendlyShields.Add(shield.ShieldEnt);
                            FriendlyShieldNear = true;
                        }
                        ShieldNear = true;
                    }
                }

            }
            NearByShieldsTmp.Clear();
        }

        internal void MyPlanetInfo(bool clear = false)
        {
            if (!clear) {

                MyPlanet = MyPlanetTmp;
                var gridVolume = TopEntity.PositionComp.WorldVolume;
                var gridRadius = gridVolume.Radius;
                var gridCenter = gridVolume.Center;
                var planetCenter = MyPlanet.PositionComp.WorldAABB.Center;
                ClosestPlanetSqr = double.MaxValue;

                if (new BoundingSphereD(planetCenter, MyPlanet.AtmosphereRadius + gridRadius).Intersects(gridVolume)) {

                    InPlanetGravity = true;
                    PlanetClosestPoint = MyPlanet.GetClosestSurfacePointGlobal(gridCenter);
                    ClosestPlanetCenter = planetCenter;
                    double pointDistSqr;
                    Vector3D.DistanceSquared(ref PlanetClosestPoint, ref gridCenter, out pointDistSqr);

                    pointDistSqr -= (gridRadius * gridRadius);
                    if (pointDistSqr < 0) pointDistSqr = 0;
                    ClosestPlanetSqr = pointDistSqr;
                    PlanetSurfaceInRange = pointDistSqr <= MaxTargetingRangeSqr;
                    TouchingWater = Session.WaterApiLoaded && GridTouchingWater();
                }
                else {
                    InPlanetGravity = false;
                    PlanetClosestPoint = MyPlanet.GetClosestSurfacePointGlobal(gridCenter);
                    ClosestPlanetCenter = planetCenter;
                    double pointDistSqr;
                    Vector3D.DistanceSquared(ref PlanetClosestPoint, ref gridCenter, out pointDistSqr);

                    pointDistSqr -= (gridRadius * gridRadius);
                    if (pointDistSqr < 0) pointDistSqr = 0;
                    ClosestPlanetSqr = pointDistSqr;
                    TouchingWater = false;
                }
            }
            else {
                MyPlanet = null;
                PlanetClosestPoint = Vector3D.Zero;
                PlanetSurfaceInRange = false;
                InPlanetGravity = false;
                ClosestPlanetSqr = double.MaxValue;
                TouchingWater = false;
            }
        }

        private bool GridTouchingWater()
        {
            Water water;
            if (Session.WaterMap.TryGetValue(MyPlanet, out water)) {
                WaterVolume = new BoundingSphereD(MyPlanet.PositionComp.WorldAABB.Center, water.radius + water.waveHeight);
                return new MyOrientedBoundingBoxD(TopEntity.PositionComp.LocalAABB, TopEntity.PositionComp.WorldMatrixRef).Intersects(ref WaterVolume);
            }
            return false;
        }

        internal void MyStaticInfo()
        {
            ClosestStaticSqr = double.MaxValue;
            StaticGridInRange = false;
            MyEntity closestEnt = null;
            var closestCenter = Vector3D.Zero;
            double closestDistSqr = double.MaxValue;
            CanShoot = true;
            for (int i = 0; i < StaticsInRange.Count; i++) {

                var ent = StaticsInRange[i];
                if (ent == null) continue;
                if (ent.MarkedForClose) continue;
                var safeZone = ent as MySafeZone;
                

                var staticCenter = ent.PositionComp.WorldAABB.Center;
                if (ent is MyCubeGrid) StaticGridInRange = true;

                double distSqr;
                Vector3D.DistanceSquared(ref staticCenter, ref ScanVolume.Center, out distSqr);
                if (distSqr < closestDistSqr) {
                    closestDistSqr = distSqr;
                    closestEnt = ent;
                    closestCenter = staticCenter;
                }

                if (CanShoot && safeZone != null && safeZone.Enabled) {

                    if (safeZone.PositionComp.WorldVolume.Contains(TopEntity.PositionComp.WorldVolume) != ContainmentType.Disjoint && ((Session.SafeZoneAction)safeZone.AllowedActions & Session.SafeZoneAction.Shooting) == 0)
                        CanShoot = !TouchingSafeZone(safeZone);
                }
            }

            if (closestEnt != null) {

                var dist = Vector3D.Distance(ScanVolume.Center, closestCenter);
                dist -= closestEnt.PositionComp.LocalVolume.Radius;
                dist -= ScanVolume.Radius;
                if (dist < 0) dist = 0;

                var distSqr = dist * dist;
                if (ClosestPlanetSqr < distSqr) distSqr = ClosestPlanetSqr;

                ClosestStaticSqr = distSqr;
            }
            else if (ClosestPlanetSqr < ClosestStaticSqr) ClosestStaticSqr = ClosestPlanetSqr;
        }

        private bool TouchingSafeZone(MySafeZone safeZone)
        {
            var myObb = new MyOrientedBoundingBoxD(TopEntity.PositionComp.LocalAABB, TopEntity.PositionComp.WorldMatrixRef);
            
            if (safeZone.Shape == MySafeZoneShape.Sphere) {
                var sphere = new BoundingSphereD(safeZone.PositionComp.WorldVolume.Center, safeZone.Radius);
                return myObb.Intersects(ref sphere);
            }

            return new MyOrientedBoundingBoxD(safeZone.PositionComp.LocalAABB, safeZone.PositionComp.WorldMatrixRef).Contains(ref myObb) != ContainmentType.Disjoint;
        }

        internal bool CreateEntInfo(MyEntity entity, long gridOwner, out MyDetectedEntityInfo entInfo)
        {

            try
            {
                MyRelationsBetweenPlayerAndBlock relationship = MyRelationsBetweenPlayerAndBlock.Neutral;
                if (entity == null)
                {
                    entInfo = new MyDetectedEntityInfo();
                    return false;
                }
                var grid = entity.GetTopMostParent() as MyCubeGrid;
                if (grid != null)
                {
                    if (!grid.DestructibleBlocks || grid.Immune || grid.GridGeneralDamageModifier <= 0)
                    {
                        entInfo = new MyDetectedEntityInfo();
                        return false;
                    }

                    var bigOwners = grid.BigOwners;
                    var topOwner = bigOwners.Count > 0 ? bigOwners[0] : long.MaxValue;

                    relationship = topOwner != long.MinValue ? MyIDModule.GetRelationPlayerBlock(gridOwner, topOwner, MyOwnershipShareModeEnum.Faction) : MyRelationsBetweenPlayerAndBlock.NoOwnership;

                    var type = grid.GridSizeEnum != MyCubeSize.Small ? MyDetectedEntityType.LargeGrid : MyDetectedEntityType.SmallGrid;
                    entInfo = new MyDetectedEntityInfo(grid.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship, new BoundingBoxD(), Session.Tick);
                    return true;
                }

                var myCharacter = entity as IMyCharacter;
                if (myCharacter != null)
                {
                    var type = !myCharacter.IsPlayer ? MyDetectedEntityType.CharacterOther : MyDetectedEntityType.CharacterHuman;

                    var getComponentOwner = entity as IMyComponentOwner<MyIDModule>;

                    long playerId;
                    MyIDModule targetIdModule;
                    if (getComponentOwner != null && getComponentOwner.GetComponent(out targetIdModule))
                        playerId = targetIdModule.Owner;
                    else {
                        var controllingId = myCharacter.ControllerInfo?.ControllingIdentityId;
                        playerId = controllingId ?? 0;
                    }
                    
                    relationship = MyIDModule.GetRelationPlayerBlock(gridOwner, playerId, MyOwnershipShareModeEnum.Faction);
                    
                    entInfo = new MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship, new BoundingBoxD(), Session.Tick);
                    return !myCharacter.IsDead && myCharacter.Integrity > 0;
                }

                var myPlanet = entity as MyPlanet;

                if (myPlanet != null)
                {
                    const MyDetectedEntityType type = MyDetectedEntityType.Planet;
                    entInfo = new MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship, new BoundingBoxD(), Session.Tick);
                    return true;
                }
                if (entity is MyVoxelMap)
                {
                    const MyDetectedEntityType type = MyDetectedEntityType.Asteroid;
                    entInfo = new MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, relationship, new BoundingBoxD(), Session.Tick);
                    return true;
                }
                if (entity is MyMeteor)
                {
                    const MyDetectedEntityType type = MyDetectedEntityType.Meteor;
                    entInfo = new MyDetectedEntityInfo(entity.EntityId, string.Empty, type, null, MatrixD.Zero, Vector3.Zero, MyRelationsBetweenPlayerAndBlock.Enemies, new BoundingBoxD(), Session.Tick);
                    return true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CreateEntInfo: {ex}", null, true); }
            
            entInfo = new MyDetectedEntityInfo();
            return false;
        }
    }
}
