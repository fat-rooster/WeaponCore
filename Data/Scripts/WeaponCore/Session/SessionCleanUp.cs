﻿using System;
using VRage.Collections;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {

        private void DeferedUpBlockTypeCleanUp(bool force = false)
        {
            foreach (var clean in BlockTypeCleanUp)
            {
                if (force || Tick - clean.RequestTick > 120)
                {
                    foreach (var item in clean.Collection)
                        ConcurrentListPool.Return(item.Value);
                    clean.Collection.Clear();

                    BlockTypePool.Return(clean.Collection);

                    DeferedTypeCleaning removed;
                    BlockTypeCleanUp.TryDequeue(out removed);
                }
            }
        }

        internal void PurgeAll()
        {
            PurgedAll = true;
            FutureEvents.Purge((int)Tick);
            
            
            foreach (var comp in CompsToStart)
                if (comp?.Platform != null)
                    CloseComps(comp.MyCube);

            foreach (var readd in CompReAdds)
            {
                if (!readd.Ai.Closed) readd.Ai.AiForceClose();
                if (readd.Comp?.Platform != null)
                {
                    CloseComps(readd.Comp.MyCube);
                }
            }

            foreach (var comp in CompsDelayed)
            {
                if (comp?.Platform != null)
                    CloseComps(comp.MyCube);
            }

            foreach (var gridAi in DelayedAiClean)
            {
                if (!gridAi.Closed)
                    gridAi.AiForceClose();
            }

            PlatFormPool.Clean();
            CompsToStart.ClearImmediate();
            DelayedAiClean.ClearImmediate();

            CompsDelayed.Clear();
            CompReAdds.Clear();
            GridAiPool.Clean();


            PurgeTerminalSystem(this);
            HudUi.Purge();
            TerminalMon.Purge();
            foreach (var reports in Reporter.ReportData.Values)
            {
                foreach (var report in reports)
                {
                    report.Clean();
                    Reporter.ReportPool.Return(report);
                }
                reports.Clear();
            }
            Reporter.ReportData.Clear();
            Reporter.ReportPool.Clean();

            PacketsToClient.Clear();
            PacketsToServer.Clear();

            AcqManager.Clean();

            CleanSounds(true);

            foreach (var e in Emitters)
                e.StopSound(true);
            foreach (var e in Av.HitEmitters)
                e.StopSound(true);
            foreach (var e in Av.FireEmitters)
                e.StopSound(true);
            foreach (var e in Av.TravelEmitters)
                e.StopSound(true);

            Emitters.Clear();
            Av.HitEmitters.Clear();
            Av.FireEmitters.Clear();
            Av.TravelEmitters.Clear();

            foreach (var item in EffectedCubes)
            {
                var cubeid = item.Key;
                var blockInfo = item.Value;
                var functBlock = blockInfo.FunctBlock;

                if (functBlock == null || functBlock.MarkedForClose)
                {
                    _effectPurge.Enqueue(cubeid);
                    continue;
                }

                functBlock.EnabledChanged -= ForceDisable;
                functBlock.Enabled = blockInfo.FirstState;
                functBlock.SetDamageEffect(false);
                if (HandlesInput)
                    functBlock.AppendingCustomInfo -= blockInfo.AppendCustomInfo;
                _effectPurge.Enqueue(cubeid);
            }

            while (_effectPurge.Count != 0)
            {
                EffectedCubes.Remove(_effectPurge.Dequeue());
            }

            Av.Glows.Clear();
            Av.AvShotPool.Clean();

            DeferedUpBlockTypeCleanUp(true);
            BlockTypeCleanUp.Clear();

            foreach (var map in GridToInfoMap.Keys)
                RemoveGridFromMap(map);

            GridToInfoMap.Clear();
            GridMapPool.Clean();
            
            DirtyGridsTmp.Clear();

            foreach (var structure in WeaponPlatforms.Values)
            {
                foreach (var system in structure.WeaponSystems)
                {
                    system.Value.PreFirePairs.Clear();
                    system.Value.FireWhenDonePairs.Clear();
                    system.Value.FirePerShotPairs.Clear();
                    system.Value.RotatePairs.Clear();
                    system.Value.ReloadPairs.Clear();
                    foreach (var ammo in system.Value.AmmoTypes) {
                        ammo.AmmoDef.Const.PrimeEntityPool?.Clean();
                        ammo.AmmoDef.Const.HitDefaultSoundPairs.Clear();
                        ammo.AmmoDef.Const.HitVoxelSoundPairs.Clear();
                        ammo.AmmoDef.Const.HitShieldSoundPairs.Clear();
                        ammo.AmmoDef.Const.HitFloatingSoundPairs.Clear();
                        ammo.AmmoDef.Const.HitPlayerSoundPairs.Clear();
                        ammo.AmmoDef.Const.TravelSoundPairs.Clear();
                        ammo.AmmoDef.Const.CustomSoundPairs.Clear();
                    }
                }

                structure.WeaponSystems.Clear();
            }
            WeaponPlatforms.Clear();

            foreach (var gridToMap in GridToBlockTypeMap)
            {
                foreach (var map in gridToMap.Value)
                {
                    ConcurrentListPool.Return(map.Value);
                }
                gridToMap.Value.Clear();
                BlockTypePool.Return(gridToMap.Value);
            }
            GridToBlockTypeMap.Clear();

            foreach(var playerGrids in PlayerEntityIdInRange)
                playerGrids.Value.Clear();

            PlayerEntityIdInRange.Clear();
            DirtyGridInfos.Clear();

            DsUtil.Purge();
            DsUtil2.Purge();

            ShootingWeapons.Clear();
            WeaponToPullAmmo.Clear();
            AmmoToPullQueue.Clear();
            ChargingWeaponsIndexer.Clear();
            WeaponsToRemoveAmmoIndexer.Clear();
            ChargingWeapons.Clear();
            Hits.Clear();
            HomingWeapons.Clear();
            GridToMasterAi.Clear();
            Players.Clear();
            IdToCompMap.Clear();
            AllArmorBaseDefinitions.Clear();
            HeavyArmorBaseDefinitions.Clear();
            AllArmorBaseDefinitions.Clear();
            AcquireTargets.Clear();
            LargeBlockSphereDb.Clear();
            SmallBlockSphereDb.Clear();
            AnimationsToProcess.Clear();
            _subTypeIdToWeaponDefs.Clear();
            WeaponDefinitions.Clear();
            SlimsSortedList.Clear();
            _destroyedSlims.Clear();
            _destroyedSlimsClient.Clear();
            _slimHealthClient.Clear();
            _slimsSet.Clear();
            _turretDefinitions.Clear();
            _tmpNearByBlocks.Clear();

            foreach (var av in Av.AvShots) {
                av.GlowSteps.Clear();
                Av.AvShotPool.Return(av);
            }
            Av.AvShotPool.Clean();
            Av.AvBarrels1.Clear();
            Av.AvBarrels2.Clear();
            Av.AvShots.Clear();
            Av.HitSounds.Clear();

            foreach (var errorpkt in ClientSideErrorPkt)
                errorpkt.Packet.CleanUp();
            ClientSideErrorPkt.Clear();

            GridEffectPool.Clean();
            GridEffectsPool.Clean();
            BlockTypePool.Clean();
            ConcurrentListPool.Clean();

            TargetInfoPool.Clean();
            PacketObjPool.Clean();

            InventoryMoveRequestPool.Clean();
            WeaponCoreBlockDefs.Clear();
            VanillaIds.Clear();
            VanillaCoreIds.Clear();
            WeaponCoreFixedBlockDefs.Clear();
            WeaponCoreTurretBlockDefs.Clear();
            VoxelCaches.Clear();
            ArmorCubes.Clear();

            foreach (var p in Projectiles.ProjectilePool)
                p.Info?.AvShot?.AmmoEffect?.Stop();

            Projectiles.ShrapnelToSpawn.Clear();
            Projectiles.ShrapnelPool.Clean();
            Projectiles.FragmentPool.Clean();
            Projectiles.ActiveProjetiles.Clear();
            Projectiles.ProjectilePool.Clear();
            Projectiles.HitEntityPool.Clean();
            Projectiles.VirtInfoPool.Clean();

            DbsToUpdate.Clear();
            GridTargetingAIs.Clear();

            DsUtil = null;
            DsUtil2 = null;
            SlimsSortedList = null;
            Settings = null;
            StallReporter = null;
            TerminalMon = null;
            Physics = null;
            Camera = null;
            Projectiles = null;
            TrackingAi = null;
            UiInput = null;
            TargetUi = null;
            Placer = null;
            TargetGps = null;
            SApi.Unload();
            SApi = null;
            Api = null;
            ApiServer = null;
            Reporter = null;
            WeaponDefinitions = null;
            AnimationsToProcess = null;
            ProjectileTree.Clear();
            ProjectileTree = null;
            Av = null;
            HudUi = null;
            AllDefinitions = null;
            SoundDefinitions = null;
            ActiveCockPit = null;
            ActiveControlBlock = null;
            ControlledEntity = null;
            TmpStorage = null;
        }
    }
}
