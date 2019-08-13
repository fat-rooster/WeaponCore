﻿using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using static WeaponCore.Support.SubSystemDefinition.BlockTypes;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal static void AcquireTarget(Weapon w)
        {
            w.LastTargetCheck = 0;
            var target = w.NewTarget;
            var physics = MyAPIGateway.Physics;
            var weaponPos = w.Comp.MyPivotPos;
            var ai = w.Comp.Ai;
            var newTarget = false;

            foreach (var lp in ai.LiveProjectile)
            {
                if (Vector3D.Distance(lp.Position, weaponPos) > 750) continue;

                if (Weapon.ValidTrajectory(w, null, lp))
                {
                    var needsCast = false;
                    for (int i = 0; i < ai.Obstructions.Count; i++)
                    {
                        var obsSphere = ai.Obstructions[i].PositionComp.WorldVolume;
                        var dir = lp.Position - weaponPos;
                        var beam = new RayD(ref weaponPos, ref dir);
                        if (beam.Intersects(obsSphere) != null)
                        {
                            Log.Line("possible obscure");
                            needsCast = true;
                        }
                    }

                    if (needsCast)
                    {
                        IHitInfo hitInfo;
                        physics.CastRay(weaponPos, lp.Position, out hitInfo, 15, true);
                        if (hitInfo?.HitEntity == null)
                        {
                            double hitDist;
                            Vector3D.Distance(ref weaponPos, ref lp.Position, out hitDist);
                            var shortDist = hitDist;
                            var origDist = hitDist;
                            var topEntId = long.MaxValue;
                            target.Set(null, lp.Position, shortDist, origDist, topEntId, lp);
                            newTarget = true;
                            break;
                        }
                        Log.Line($"is obscured");
                    }
                }
                else Log.Line("not in view");
            }

            if (!newTarget && ai.MyGrid.CubeBlocks.Count < 5000)
            {
                for (int i = 0; i < ai.SortedTargets.Count; i++)
                {
                    var info = ai.SortedTargets[i];
                    if (info.Target == null || info.Target.MarkedForClose || !info.Target.InScene || Vector3D.DistanceSquared(info.EntInfo.Position, w.Comp.MyPivotPos) > w.System.MaxTrajectorySqr) continue;

                    if (w.TrackingAi)
                    {
                        if (!Weapon.ValidTrajectory(w, info.Target, null)) continue;
                    }
                    else if (!Weapon.ValidTrajectory(w, info.Target, null)) continue;

                    if (info.IsGrid)
                    {
                        if (!AcquireBlock(w.System, w.Comp.Ai, ref target, info, weaponPos, w)) continue;
                        newTarget = true;
                        break;
                    }

                    var targetPos = info.Target.PositionComp.WorldAABB.Center;
                    IHitInfo hitInfo;
                    physics.CastRay(weaponPos, targetPos, out hitInfo, 15, true);
                    if (hitInfo?.HitEntity == info.Target)
                    {
                        Log.Line($"{w.System.WeaponName} - found something");

                        double rayDist;
                        Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                        var shortDist = rayDist * (1 - hitInfo.Fraction);
                        var origDist = rayDist * hitInfo.Fraction;
                        var topEntId = info.Target.GetTopMostParent().EntityId;
                        target.Set(info.Target, hitInfo.Position, shortDist, origDist, topEntId);
                        newTarget = true;
                        break;
                    }
                }
            }

            if (newTarget)
            {
                var projectile = w.NewTarget.Projectile != null;
                var expiredProjectile = projectile && !ai.LiveProjectile.Contains(w.NewTarget.Projectile);
                var validProjectile = projectile && !expiredProjectile;
                if (expiredProjectile) w.NewTarget.Reset();
                w.Target.Expired = !validProjectile && (w.NewTarget.Entity == null || w.NewTarget.Entity.MarkedForClose);
                w.NewTarget.TransferTo(w.Target);
            }
            else
            {
                //Log.Line($"{w.System.WeaponName} - no valid target returned - oldTargetNull:{target.Entity == null} - oldTargetMarked:{target.Entity?.MarkedForClose} - checked: {w.Comp.Ai.SortedTargets.Count} - Total:{w.Comp.Ai.Targeting.TargetRoots.Count}");
                target.Reset();
                w.LastTargetCheck = 1;
                w.Target.Expired = true;
            }
        }

        internal static bool ReacquireTarget(Projectile p)
        {
            p.ChaseAge = p.Age;
            var physics = MyAPIGateway.Physics;
            var ai = p.T.Ai;
            var weaponPos = p.Position;
            var target = p.T.Target;
            for (int i = 0; i < ai.SortedTargets.Count; i++)
            {
                var info = ai.SortedTargets[i];
                if (info.Target == null || info.Target.MarkedForClose || !info.Target.InScene || Vector3D.DistanceSquared(info.EntInfo.Position, p.Position) > p.DistanceToTravelSqr) continue;

                if (info.IsGrid)
                {
                    if (!AcquireBlock(p.T.System, p.T.Ai, ref target, info, weaponPos)) continue;
                    return true;
                }

                var targetPos = info.Target.PositionComp.WorldAABB.Center;
                IHitInfo hitInfo;
                physics.CastRay(weaponPos, targetPos, out hitInfo, 15, true);
                if (hitInfo?.HitEntity == info.Target)
                {
                    Log.Line($"{p.T.System.WeaponName} - found something");

                    double rayDist;
                    Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                    var shortDist = rayDist * (1 - hitInfo.Fraction);
                    var origDist = rayDist * hitInfo.Fraction;
                    var topEntId = info.Target.GetTopMostParent().EntityId;
                    target.Set(info.Target, hitInfo.Position, shortDist, origDist, topEntId);
                    return true;
                }
            }
            //Log.Line($"{p.T.System.WeaponName} - no valid target returned - oldTargetNull:{target.Entity == null} - oldTargetMarked:{target.Entity?.MarkedForClose} - checked: {p.Ai.SortedTargets.Count} - Total:{p.Ai.Targeting.TargetRoots.Count}");
            target.Reset();
            return false;
        }

        private static bool AcquireBlock(WeaponSystem system, GridAi ai, ref Target target, TargetInfo info, Vector3D currentPos, Weapon w = null)
        {
            if (system.OrderedTargets)
            {
                var subSystems = system.Values.Targeting.SubSystems;
                foreach (var bt in subSystems.Systems)
                {
                    if (bt != Any && info.TypeDict[bt].Count > 0)
                    {
                        var subSystemList = info.TypeDict[bt];
                        if (subSystems.ClosestFirst)
                        {
                            if (bt != target.LastBlockType) target.Top5.Clear();
                            target.LastBlockType = bt;
                            UtilsStatic.GetClosestHitableBlockOfType(subSystemList, ref target, currentPos, w);
                            if (target.Entity != null) return true;
                        }
                        else if (FindRandomBlock(system, ai, ref target, currentPos, subSystemList, w != null)) return true;
                    }
                }
            }
            if (FindRandomBlock(system, ai, ref target, currentPos, info.TypeDict[Any], w != null)) return true;
            return false;
        }

        private static bool FindRandomBlock(WeaponSystem system, GridAi ai, ref Target target, Vector3D currentPos, List<MyCubeBlock> blockList, bool cast)
        {
            var totalBlocks = blockList.Count;
            var lastBlocks = system.Values.Targeting.TopBlocks;
            if (lastBlocks > 0 && totalBlocks < lastBlocks) lastBlocks = totalBlocks;
            int[] deck = null;
            if (lastBlocks > 0) deck = GetDeck(ref target.Deck, ref target.PrevDeckLength, 0, lastBlocks);
            var physics = MyAPIGateway.Physics;

            for (int i = 0; i < totalBlocks; i++)
            {
                var next = i;
                if (i < lastBlocks)
                    if (deck != null) next = deck[i];

                var block = blockList[next];
                if (block.MarkedForClose) continue;

                var blockPos = block.CubeGrid.GridIntegerToWorld(block.Position);
                double rayDist;
                if (cast)
                {
                    IHitInfo hitInfo;
                    physics.CastRay(currentPos, blockPos, out hitInfo, 15, true);

                    if (hitInfo?.HitEntity == null || hitInfo.HitEntity is MyVoxelBase || hitInfo.HitEntity == ai.MyGrid)
                        continue;

                    var hitGrid = hitInfo.HitEntity as MyCubeGrid;
                    if (hitGrid != null)
                    {
                        if (hitGrid.MarkedForClose || !hitGrid.InScene) continue;
                        bool enemy;

                        var bigOwners = hitGrid.BigOwners;
                        if (bigOwners.Count == 0) enemy = true;
                        else
                        {
                            var relationship = target.FiringCube.GetUserRelationToOwner(hitGrid.BigOwners[0]);
                            enemy = relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
                        }
                        if (!enemy)
                            continue;
                    }
                    Vector3D.Distance(ref currentPos, ref blockPos, out rayDist);
                    var shortDist = rayDist * (1 - hitInfo.Fraction);
                    var origDist = rayDist * hitInfo.Fraction;
                    var topEntId = block.GetTopMostParent().EntityId;
                    target.Set(block, hitInfo.Position, shortDist, origDist, topEntId);
                    return true;
                }
                Vector3D.Distance(ref currentPos, ref blockPos, out rayDist);
                target.Set(block, block.PositionComp.WorldAABB.Center, rayDist, rayDist, block.GetTopMostParent().EntityId);
                return true;
            }
            return false;
        }
    }
}
