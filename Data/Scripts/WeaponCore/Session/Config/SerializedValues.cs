﻿using System;
using System.ComponentModel;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;

namespace WeaponCore
{


    [ProtoContract]
    public class CompStateValues
    {
        [ProtoMember(1)] public bool Online; //don't save
        [ProtoMember(2)] public int Heat; //don't save
        [ProtoMember(3)] public WeaponStateValues[] Weapons;
        [ProtoMember(4)] public bool ShootOn; //don't save
        [ProtoMember(5)] public bool ClickShoot; //don't save
        [ProtoMember(6)] public PlayerControl CurrentPlayerControl; //don't save
        [ProtoMember(7)] public float CurrentCharge; //save
        [ProtoMember(8)] public int Version = Session.VersionControl; //save
        [ProtoMember(9)] public string CurrentBlockGroup; //don't save
        [ProtoMember(10)] public bool OtherPlayerTrackingReticle; //don't save

        public void Sync(CompStateValues syncFrom, WeaponComponent comp)
        {
            Online = syncFrom.Online;
            Heat = syncFrom.Heat;
            ShootOn = syncFrom.ShootOn;
            ClickShoot = syncFrom.ClickShoot;
            CurrentPlayerControl = syncFrom.CurrentPlayerControl;
            CurrentCharge = syncFrom.CurrentCharge;
            CurrentBlockGroup = syncFrom.CurrentBlockGroup;
            OtherPlayerTrackingReticle = syncFrom.OtherPlayerTrackingReticle;

            for (int i = 0; i < syncFrom.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];
                var ws = Weapons[i];
                var sws = syncFrom.Weapons[i];
                ws.ShotsFired = sws.ShotsFired;
                ws.ManualShoot = sws.ManualShoot;
                ws.SingleShotCounter = sws.SingleShotCounter;

                //ws.Sync.Charging = sws.Sync.Charging;
                ws.Sync.CurrentAmmo = sws.Sync.CurrentAmmo;
                ws.Sync.CurrentCharge = sws.Sync.CurrentCharge;
                ws.Sync.CurrentMags = sws.Sync.CurrentMags;
                //ws.Sync.Heat = sws.Sync.Heat;
                //ws.Sync.Overheated = sws.Sync.Overheated;
                //ws.Sync.Reloading = sws.Sync.Reloading;
                ws.Sync.MagsLoaded = sws.Sync.MagsLoaded;
                ws.Sync.HasInventory = sws.Sync.HasInventory;

                w.MagsLoadedClient = sws.Sync.MagsLoaded;
            }
        }

        public void ResetToFreshLoadState()
        {
            Online = false;
            Heat = 0;
            CurrentPlayerControl.ControlType = ControlType.None;
            CurrentPlayerControl.PlayerId = -1;
            CurrentBlockGroup = string.Empty;
            OtherPlayerTrackingReticle = false;

            foreach (var w in Weapons)
            {
                w.ShotsFired = 0;
               // w.Sync.Charging = false;
                //w.Sync.Heat = 0;
               // w.Sync.Overheated = false;
                //w.Sync.Reloading = false;
                w.Sync.MagsLoaded = 0;
                w.Sync.HasInventory = w.Sync.CurrentMags > 0;
            }
        }

    }

    [ProtoContract]
    public class CompSettingsValues
    {
        [ProtoMember(1), DefaultValue(true)] public bool Guidance = true;
        [ProtoMember(2), DefaultValue(1)] public int Overload = 1;
        [ProtoMember(3)] public long Modes;
        [ProtoMember(4), DefaultValue(1)] public float DpsModifier = 1;
        [ProtoMember(5), DefaultValue(1)] public float RofModifier = 1;
        [ProtoMember(6)] public WeaponSettingsValues[] Weapons;
        [ProtoMember(7), DefaultValue(100)] public float Range = 100;
        [ProtoMember(8)] public GroupOverrides Overrides;
        [ProtoMember(9)] public int Version = Session.VersionControl;

        public CompSettingsValues()
        {
            Overrides = new GroupOverrides();
        }

        public void Sync(WeaponComponent comp, CompSettingsValues syncFrom)
        {
            Guidance = syncFrom.Guidance;
            Modes = syncFrom.Modes;
            
            Range = syncFrom.Range;

            foreach (var w in comp.Platform.Weapons)
                w.UpdateWeaponRange();

            Overrides.Sync(syncFrom.Overrides);

            if (Overload != syncFrom.Overload || Math.Abs(RofModifier - syncFrom.RofModifier) > 0.0001f || Math.Abs(DpsModifier - syncFrom.DpsModifier) > 0.0001f )
            {
                Overload = syncFrom.Overload;
                RofModifier = syncFrom.RofModifier;
                WepUi.SetDps(comp, syncFrom.DpsModifier, true);
            }
        }

    }

    [ProtoContract]
    public class WeaponStateValues
    {
        [ProtoMember(1)] public int ShotsFired; //don't know??
        [ProtoMember(2), DefaultValue(ManualShootActionState.ShootOff)] public ManualShootActionState ManualShoot = ManualShootActionState.ShootOff; // save
        [ProtoMember(3)] public int SingleShotCounter; // save
        [ProtoMember(4)] public WeaponSyncValues Sync;

    }

    [ProtoContract]
    public class WeaponSyncValues
    {
        //[ProtoMember(1)] public float Heat; // don't save
        [ProtoMember(2)] public int CurrentAmmo; //save
        [ProtoMember(3)] public float CurrentCharge; //save
        //[ProtoMember(4)] public bool Overheated; //don't save
        //[ProtoMember(5)] public bool Reloading; // don't save
        //[ProtoMember(6)] public bool Charging; // don't save
        [ProtoMember(7)] public int WeaponId; // save
        [ProtoMember(8)] public MyFixedPoint CurrentMags; // save
        [ProtoMember(9)] public int MagsLoaded; // save
        [ProtoMember(10)] public bool HasInventory; // save

        public void SetState (WeaponSyncValues sync, Weapon weapon)
        {
            //sync.Heat = Heat;
            sync.CurrentAmmo = CurrentAmmo;
            sync.CurrentMags = CurrentMags;
            sync.CurrentCharge = CurrentCharge;
            //sync.Overheated = Overheated;
            //sync.Reloading = Reloading;
            //sync.Charging = Charging;
            sync.MagsLoaded = MagsLoaded;
            sync.HasInventory = HasInventory;

            weapon.MagsLoadedClient = MagsLoaded;
        }
    }

    [ProtoContract]
    public class PlayerControl
    {
        [ProtoMember(1), DefaultValue(-1)] public long PlayerId = -1;
        [ProtoMember(2), DefaultValue(ControlType.None)] public ControlType ControlType = ControlType.None;

        public PlayerControl() { }

        public void Sync(PlayerControl syncFrom)
        {
            PlayerId = syncFrom.PlayerId;
            ControlType = syncFrom.ControlType;
        }
    }
    
    public enum ControlType
    {
        None,
        Ui,
        Toolbar,
        Camera        
    }

    [ProtoContract]
    public class WeaponSettingsValues
    {
        [ProtoMember(1)] public bool Enable = true;
        [ProtoMember(2)] public int AmmoTypeId;
    }

    [ProtoContract]
    public class WeaponTimings
    {
        [ProtoMember(1)] public uint ChargeDelayTicks;
        [ProtoMember(2)] public uint ChargeUntilTick;
        [ProtoMember(3)] public uint AnimationDelayTick;
        [ProtoMember(4)] public uint OffDelay;
        [ProtoMember(5)] public uint WeaponReadyTick;
        [ProtoMember(6)] public uint LastHeatUpdateTick;
        [ProtoMember(7)] public uint ReloadedTick;
        

        public WeaponTimings SyncOffsetServer(uint tick)
        {
            var offset = tick + Session.ServerTickOffset;

            return new WeaponTimings
            {
                ChargeDelayTicks = ChargeDelayTicks,
                AnimationDelayTick = AnimationDelayTick > offset ? AnimationDelayTick - offset : 0,
                ChargeUntilTick = ChargeUntilTick > offset ? ChargeUntilTick - offset : 0,
                OffDelay = OffDelay >= offset ? OffDelay - offset : 0,
                WeaponReadyTick = WeaponReadyTick > offset ? WeaponReadyTick - offset : 0,
                LastHeatUpdateTick = tick - LastHeatUpdateTick > 20 ? 0 : (tick - LastHeatUpdateTick) - offset,
                ReloadedTick = ReloadedTick > offset ? ReloadedTick - offset : 0,
            };

        }

        public WeaponTimings SyncOffsetClient(uint tick)
        {
            return new WeaponTimings
            {
                ChargeDelayTicks = ChargeDelayTicks,
                AnimationDelayTick = AnimationDelayTick > 0 ? AnimationDelayTick + tick : 0,
                ChargeUntilTick = ChargeUntilTick > 0 ? ChargeUntilTick + tick : 0,
                OffDelay = OffDelay > 0 ? OffDelay + tick : 0,
                WeaponReadyTick = WeaponReadyTick > 0 ? WeaponReadyTick + tick : 0,
                ReloadedTick = ReloadedTick,
            };
        }

        public void Sync(WeaponTimings syncFrom)
        {
            ChargeDelayTicks = syncFrom.ChargeDelayTicks;
            ChargeUntilTick = syncFrom.ChargeUntilTick;
            AnimationDelayTick = syncFrom.AnimationDelayTick;
            OffDelay = syncFrom.OffDelay;
            WeaponReadyTick = syncFrom.WeaponReadyTick;
            LastHeatUpdateTick = syncFrom.LastHeatUpdateTick;
            ReloadedTick = syncFrom.ReloadedTick;
        }
    }

    [ProtoContract]
    public class WeaponValues
    {
        [ProtoMember(1)] public TransferTarget[] Targets;
        [ProtoMember(2)] public WeaponTimings[] Timings;
        [ProtoMember(3)] public WeaponRandomGenerator[] WeaponRandom;
        [ProtoMember(4)] public uint[] MIds;

        public void Save(WeaponComponent comp)
        {
            if (comp.MyCube?.Storage == null) return;

            var sv = new WeaponValues {Targets = Targets, WeaponRandom = WeaponRandom, MIds = comp.MIds, Timings = new WeaponTimings[comp.Platform.Weapons.Length]};

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];
                sv.Timings[w.WeaponId] = w.Timings.SyncOffsetServer(comp.Session.Tick);
            }

            var binary = MyAPIGateway.Utilities.SerializeToBinary(sv);
            comp.MyCube.Storage[comp.Session.MpWeaponSyncGuid] = Convert.ToBase64String(binary);

        }

        public static void Load(WeaponComponent comp)
        {
            string rawData;
            if (comp.MyCube.Storage.TryGetValue(comp.Session.MpWeaponSyncGuid, out rawData))
            {
                var base64 = Convert.FromBase64String(rawData);
                try
                {
                    comp.WeaponValues = MyAPIGateway.Utilities.SerializeFromBinary<WeaponValues>(base64);

                    if (!comp.Session.IsClient || comp.WeaponValues.MIds == null || comp.WeaponValues.MIds?.Length != Enum.GetValues(typeof(PacketType)).Length)
                        comp.WeaponValues.MIds = new uint[Enum.GetValues(typeof(PacketType)).Length];

                    comp.MIds = comp.WeaponValues.MIds;
                    var timings = comp.WeaponValues.Timings;
                    var targets = comp.WeaponValues.Targets;

                    for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                    {
                        var w = comp.Platform.Weapons[i];
                        var rand = comp.WeaponValues.WeaponRandom[w.WeaponId];

                        if (comp.Session.IsServer)
                        {
                            timings[w.WeaponId] = new WeaponTimings();
                            targets[w.WeaponId] = new TransferTarget();
                            comp.WeaponValues.WeaponRandom[w.WeaponId] = new WeaponRandomGenerator();

                            rand.CurrentSeed = Guid.NewGuid().GetHashCode();
                            rand.AcquireRandom = new Random(rand.CurrentSeed);
                        }

                        var wTiming = comp.Session.IsServer ? timings[w.WeaponId] : timings[w.WeaponId].SyncOffsetClient(comp.Session.Tick);
                        rand.ClientProjectileRandom = new Random(rand.CurrentSeed);
                        rand.TurretRandom = new Random(rand.CurrentSeed);

                        for (int j = 0; j < rand.TurretCurrentCounter; j++)
                            rand.TurretRandom.Next();

                        for (int j = 0; j < rand.ClientProjectileCurrentCounter; j++)
                            rand.ClientProjectileRandom.Next();

                        comp.Session.FutureEvents.Schedule(o => { comp.Session.SyncWeapon(w, wTiming, ref w.State.Sync, false); }, null, 1);
                    }
                    return;
                }
                catch (Exception e)
                {
                    Log.Line($"Weapon Values Failed To load re-initing");
                }

            }            

            comp.WeaponValues = new WeaponValues
            {
                Targets = new TransferTarget[comp.Platform.Weapons.Length],
                Timings = new WeaponTimings[comp.Platform.Weapons.Length],
                WeaponRandom = new WeaponRandomGenerator[comp.Platform.Weapons.Length],
                MIds = new uint[Enum.GetValues(typeof(PacketType)).Length]
            };

            comp.MIds = comp.WeaponValues.MIds;
            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                var w = comp.Platform.Weapons[i];

                comp.WeaponValues.Targets[w.WeaponId] = new TransferTarget();
                w.Timings = comp.WeaponValues.Timings[w.WeaponId] = new WeaponTimings();
                comp.WeaponValues.WeaponRandom[w.WeaponId] = new WeaponRandomGenerator();

                var rand = comp.WeaponValues.WeaponRandom[w.WeaponId];
                rand.CurrentSeed = Guid.NewGuid().GetHashCode();
                rand.ClientProjectileRandom = new Random(rand.CurrentSeed);
                rand.TurretRandom = new Random(rand.CurrentSeed);
                rand.AcquireRandom = new Random(rand.CurrentSeed);

                comp.Session.FutureEvents.Schedule(o => { comp.Session.SyncWeapon(w, w.Timings, ref w.State.Sync, false); }, null, 1);
            }


        }

        public WeaponValues() { }
    }

    [ProtoContract]
    public class GroupOverrides
    {
        [ProtoMember(1), DefaultValue(true)] public bool Activate = true;
        [ProtoMember(2)] public bool Neutrals;
        [ProtoMember(3)] public bool Unowned;
        [ProtoMember(4)] public bool Friendly;
        [ProtoMember(5)] public bool TargetPainter;
        [ProtoMember(6)] public bool ManualControl;
        [ProtoMember(7)] public bool FocusTargets;
        [ProtoMember(8)] public bool FocusSubSystem;
        [ProtoMember(9)] public BlockTypes SubSystem = BlockTypes.Any;
        [ProtoMember(10), DefaultValue(true)] public bool Meteors;
        [ProtoMember(11), DefaultValue(true)] public bool Biologicals;
        [ProtoMember(12), DefaultValue(true)] public bool Projectiles;

        public GroupOverrides() { }

        public void Sync(GroupOverrides syncFrom)
        {
            Activate = syncFrom.Activate;
            Neutrals = syncFrom.Neutrals;
            Unowned = syncFrom.Unowned;
            Friendly = syncFrom.Friendly;
            TargetPainter = syncFrom.TargetPainter;
            ManualControl = syncFrom.ManualControl;
            FocusTargets = syncFrom.FocusTargets;
            FocusSubSystem = syncFrom.FocusSubSystem;
            SubSystem = syncFrom.SubSystem;
            Meteors = syncFrom.Meteors;
            Biologicals = syncFrom.Biologicals;
            Projectiles = syncFrom.Projectiles;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            var compared = (GroupOverrides)obj;

            return (
                compared.Activate.Equals(Activate) && 
                compared.Neutrals.Equals(Neutrals) && 
                compared.Unowned.Equals(Unowned) && 
                compared.Friendly.Equals(Friendly) && 
                compared.TargetPainter.Equals(TargetPainter) && 
                compared.ManualControl.Equals(ManualControl) && 
                compared.FocusTargets.Equals(FocusTargets) && 
                compared.FocusSubSystem.Equals(FocusSubSystem) && 
                compared.SubSystem.Equals(SubSystem) && 
                compared.Meteors.Equals(Meteors) && 
                compared.Biologicals.Equals(Biologicals) && 
                compared.Projectiles.Equals(Projectiles)
            );
        }

        protected bool Equals(GroupOverrides other)
        {
            return Activate == other.Activate && Neutrals == other.Neutrals && Unowned == other.Unowned && Friendly == other.Friendly && TargetPainter == other.TargetPainter && ManualControl == other.ManualControl && FocusTargets == other.FocusTargets && FocusSubSystem == other.FocusSubSystem && SubSystem == other.SubSystem && Meteors == other.Meteors && Biologicals == other.Biologicals && Projectiles == other.Projectiles;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Activate.GetHashCode();
                hashCode = (hashCode * 397) ^ Neutrals.GetHashCode();
                hashCode = (hashCode * 397) ^ Unowned.GetHashCode();
                hashCode = (hashCode * 397) ^ Friendly.GetHashCode();
                hashCode = (hashCode * 397) ^ TargetPainter.GetHashCode();
                hashCode = (hashCode * 397) ^ ManualControl.GetHashCode();
                hashCode = (hashCode * 397) ^ FocusTargets.GetHashCode();
                hashCode = (hashCode * 397) ^ FocusSubSystem.GetHashCode();
                hashCode = (hashCode * 397) ^ (int) SubSystem;
                hashCode = (hashCode * 397) ^ Meteors.GetHashCode();
                hashCode = (hashCode * 397) ^ Biologicals.GetHashCode();
                hashCode = (hashCode * 397) ^ Projectiles.GetHashCode();
                return hashCode;
            }
        }
    }

    [ProtoContract]
    public struct ControllingPlayersSync
    {
        [ProtoMember (1)] public PlayerToBlock[] PlayersToControlledBlock;
    }

    [ProtoContract]
    public struct PlayerToBlock
    {
        [ProtoMember(1)] public long PlayerId;
        [ProtoMember(2)] public long EntityId;
    }

    [ProtoContract]
    public class WeaponRandomGenerator
    {
        [ProtoMember(1)] public int TurretCurrentCounter;
        [ProtoMember(2)] public int ClientProjectileCurrentCounter;
        [ProtoMember(3)] public int CurrentSeed;
        public Random TurretRandom = new Random();
        public Random ClientProjectileRandom = new Random();
        public Random AcquireRandom = new Random();

        public enum RandomType
        {
            Deviation,
            ReAcquire,
            Acquire,
        }

        public WeaponRandomGenerator() { }

        public void Sync(WeaponRandomGenerator syncFrom)
        {
            CurrentSeed = syncFrom.CurrentSeed;

            TurretCurrentCounter = syncFrom.TurretCurrentCounter;
            ClientProjectileCurrentCounter = syncFrom.ClientProjectileCurrentCounter;

            TurretRandom = new Random(CurrentSeed);
            ClientProjectileRandom = new Random(CurrentSeed);
        }
    }
}
