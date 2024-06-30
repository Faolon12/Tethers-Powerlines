using FaolonTether;
using ParallelTasks;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.Game.GameSystems.Conveyors;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.ModAPI.Weapons;
using SENetworkAPI;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRage.Common.Utils;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.VisualScripting;
using VRage.GameServices;
using VRage.Input;
using VRage.Library.Utils;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRageRender.Animations;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

// Heavy Industry FIX provided by Twertoon. Big thanks man!

//Dummy name: cable_attach_point

//GUID: 73bb9141-0a4e-4c5a-93ae-f0d9ff23c043
/*

And all public members in C# are PascalCased
arguments are camelCased
*/

namespace FaolonTether
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false, "ChargingStation", "TransformerPylon", "PowerlinePillar", "PowerSockets", "ConveyorHoseAttachment", "LFemaleCStation", "SMaleCStation")]
    public class PowerlinePole : MyGameLogicComponent
    {
        public static readonly Guid SETTINGS_GUID = new Guid("73bb9141-0a4e-4c5a-93ae-f0d9ff23c043");

        public MyCubeGrid Grid;
        public IMyTerminalBlock ModBlock;

        public static List<PowerlineLink> Links = new List<PowerlineLink>();

        public Vector3D DummyAttachPoint = new Vector3D(0, 0, 0); // This block's attach start point.
        private Dictionary<string, IMyModelDummy> ModelDummy = new Dictionary<string, IMyModelDummy>();

        private static GridLinkTypeEnum LinkType = GridLinkTypeEnum.Logical;

        MyStringId cable_vis;

        NetSync<PowerlineLink> requestAttach;
        NetSync<PowerlineLink> requestDetach;


        /// <summary>
        /// The initialize method for the tether block
        /// 
        /// Since blocks are initilized in order some blocks on the grid may not be created yet
        /// Because of this we are not able to initialize any values that depend on other blocks in the grid
        /// </summary>
        /// <param name="objectBuilder">data used to contruct the block object. it can be useful but is generally not used</param>
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            ModBlock = Entity as IMyTerminalBlock;
            Grid = (MyCubeGrid)ModBlock.CubeGrid;

            requestAttach = new NetSync<PowerlineLink>(Entity, TransferType.Both, new PowerlineLink());
            requestDetach = new NetSync<PowerlineLink>(Entity, TransferType.Both, new PowerlineLink());
            requestAttach.ValueChanged += AttachLink;
            requestDetach.ValueChanged += DetachLink;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
            MyLog.Default.Info($"[Tether] Init completed for block {ModBlock.EntityId}");
        }

        public override bool IsSerialized()
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return base.IsSerialized();

            lock (Links)
            {
                PowerlineLinks links = new PowerlineLinks();
                links.Links = Links;

                MyModStorageComponentBase storage = Tools.GetStorage(Entity);
                if (storage.ContainsKey(SETTINGS_GUID))
                {
                    storage[SETTINGS_GUID] = MyAPIGateway.Utilities.SerializeToXML(links);
                }
                else
                {
                    storage.Add(SETTINGS_GUID, MyAPIGateway.Utilities.SerializeToXML(links));
                }
            }

            return base.IsSerialized();
        }

        public override void MarkForClose()
        {
            UnlinkAllConnections();
            base.MarkForClose();
        }

        public void AttachLink(PowerlineLink o, PowerlineLink n)
        {
            MyLog.Default.Info("[Tether] recieved request to attach");
            lock (Links)
            {
                n.LoadPrep();

                PowerlineLink link = null;

                for (int i = 0; i < Links.Count; i++)
                {
                    PowerlineLink l = Links[i];
                    if (l.PoleAGridName == n.PoleAGridName && l.PoleAPosition == n.PoleAPosition &&
                        l.PoleBGridName == n.PoleBGridName && l.PoleBPosition == n.PoleBPosition)
                    {
                        link = l;
                        break;
                    }
                }

                if (link == null) 
                {
                    ConnectGrids(n.PoleA.Entity.EntityId, n.PoleA.Grid, n.PoleB.Grid);
                    Links.Add(n);
                    MyLog.Default.Info($"[Tether] attached pole: {n.PoleA.Entity.EntityId} to {n.PoleB.Entity.EntityId}");
                }
            }
        }

        public void DetachLink(PowerlineLink o, PowerlineLink n)
        {
            MyLog.Default.Info("[Tether] recieved request to detach");
            lock (Links)
            {
                n.LoadPrep();

                for (int i = 0; i < Links.Count; i++) 
                {
                    PowerlineLink l = Links[i];
                    if (l.PoleAGridName == n.PoleAGridName && l.PoleAPosition == n.PoleAPosition &&
                        l.PoleBGridName == n.PoleBGridName && l.PoleBPosition == n.PoleBPosition) 
                    {
                        DisconnectGrids(n.PoleA.Entity.EntityId, n.PoleA.Grid, n.PoleB.Grid);
                        Links.Remove(l);
                        MyLog.Default.Info($"[Tether] removed pole: {n.PoleA.Entity.EntityId} to {n.PoleB.Entity.EntityId}");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// A function that is called once before the next simulation frame when the flag is set
        /// NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME
        /// 
        /// Some processes need to trigger after all grids and blocks are fully loaded into the world.
        /// Setting the flag in the init function allows us to finish initializing certain data before the main loop starts
        /// </summary>
        public override void UpdateOnceBeforeFrame()
        {
            // stop if this block is a projection
            if (ModBlock.CubeGrid?.Physics == null)
                return;

            // Setup the cable visuals.
            cable_vis = MyStringId.GetOrCompute("cable");

            DummyAttachPoint = Tools.GetDummyRelativeLocation(ModBlock);

            MyModStorageComponentBase storage = Tools.GetStorage(Entity);
            if (storage.ContainsKey(SETTINGS_GUID))
            {
                MyLog.Default.Info($"{storage[SETTINGS_GUID]}");
                try
                {
                    PowerlineLinks links = MyAPIGateway.Utilities.SerializeFromXML<PowerlineLinks>(storage[SETTINGS_GUID]);

                    foreach (PowerlineLink l in links.Links)
                    {
                        l.LoadPrep();
                    }

                    lock (Links)
                    {
                        Links = links.Links;
                    }
                }
                catch (Exception e)
                {
                    MyLog.Default.Info($"{e}");
                }
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return;

            lock (Links)
            {
                for (int i = 0; i < Links.Count; i++)
                {
                    PowerlineLink l = Links[i];

                    if (l.PoleA == null || l.PoleB == null) continue;

                    if (l.PoleA == this && !IsInRange(l.PoleA, l.PoleB))
                    {
                        requestDetach.SetValue(l, SyncType.Broadcast);
                    }
                }
            }
        }

        /// <summary>
        /// This is the main update method
        /// </summary>
        public override void UpdateAfterSimulation()
        {
            if (ModBlock.CubeGrid?.Physics == null)
                return;

            if (MyAPIGateway.Session?.Player?.Character == null)
                return;

            if (!Grid.IsStatic)
            {
                DummyAttachPoint = Tools.GetDummyRelativeLocation(ModBlock);
            }

            DrawCable();
        }


        /// <summary>
        /// Performs cleanup before the block is removed from the scene
        /// 
        /// In our case we need to decuple any grids that are linked via this block
        /// </summary>
        public override void Close()
        {
            UnlinkAllConnections();
        }

        public void UnlinkAllConnections()
        {
            lock (Links)
            {
                long id = ModBlock.EntityId;

                PowerlinePole p = null;
                for (int i = 0; i < Links.Count; i++)
                {
                    PowerlineLink l = Links[i];
                    if (l.PoleA == this)
                    {
                        p = l.PoleB;
                    }
                    else if (l.PoleB == this)
                    {
                        p = l.PoleA;
                    }

                    if (p != null)
                    {
                        requestDetach.SetValue(l, SyncType.Broadcast);
                    }
                }
            }
        }

        public static int LinkCount(MyCubeGrid a, MyCubeGrid b)
        {
            int linkCount = 0;
            foreach (PowerlineLink l in Links)
            {
                if ((l.PoleA.Grid == a || l.PoleB.Grid == a) &&
                    (l.PoleA.Grid == b || l.PoleB.Grid == b))
                {
                    linkCount++;
                }
            }

            return linkCount;
        }

        public static void ConnectGrids(long id, MyCubeGrid a, MyCubeGrid b)
        {
            int linkCount = LinkCount(a, b);
            if (linkCount > 0)
            {
                MyLog.Default.Info($"[Tether] ConnectGrids: there are {linkCount} connections between {a.EntityId} and {b.EntityId}. skipping link.");
                return;
            }

            if (!a.IsInSameLogicalGroupAs(b))
            {
                MyCubeGrid.CreateGridGroupLink(LinkType, id, a, b);
                MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Electrical, id, a, b);
                MyLog.Default.Info($"[Tether] ConnectGrids: grids {a.EntityId} and {b.EntityId} are now connected");
            }
        }

        public static void DisconnectGrids(long id, MyCubeGrid a, MyCubeGrid b)
        {

            if (a.IsInSameLogicalGroupAs(b))
            {
                int linkCount = LinkCount(a, b);
                if (linkCount == 1)
                {
                    MyCubeGrid.BreakGridGroupLink(LinkType, id, a, b);
                    MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Electrical, id, a, b);
                    MyLog.Default.Info($"[Tether] DisconnectGrids: grids {a.EntityId} and {b.EntityId} are disconnected");
                }
                else
                {
                    MyLog.Default.Info($"[Tether] DisconnectGrids: grids {a.EntityId} and {b.EntityId} have {linkCount} links. it will not be disconnect");
                }
            }
            else
            {
                MyLog.Default.Info($"[Tether] DisconnectGrids: grids {a.EntityId} and {b.EntityId} are not in the same group");
            }

        }



        public void ConnectionPoles(PowerlinePole targetPole, IMyPlayer player)
        {
            if (targetPole == this)
            {
                MyAPIGateway.Utilities.ShowNotification("Cannot attach cable to itself", 5000);
                return;
            }

            foreach (PowerlineLink l in Links)
            {
                if ((l.PoleA == this || l.PoleB == this) &&
                    (l.PoleA == targetPole || l.PoleB == targetPole))
                {
                    MyAPIGateway.Utilities.ShowNotification("Connection already exists", 5000);
                    return;
                }
            }

            if (!IsInRange(this, targetPole))
            {
                MyAPIGateway.Utilities.ShowNotification("Powerlines are too far apart", 5000);
                return;
            }

            MyRelationsBetweenPlayerAndBlock relation = ModBlock.GetUserRelationToOwner(player.IdentityId);

            if (!(relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare))
            {
                MyAPIGateway.Utilities.ShowNotification("Cannot modify powerlines that are not owned by you or your faction", 5000);
                return;
            }

            PowerlineLink link = PowerlineLink.Generate(this, targetPole);
            requestAttach.SetValue(link, SyncType.Broadcast);
        }

        /// <summary>
        /// Disconnects a line from this pole and returns the power pole that line was connected too
        /// </summary>
        public PowerlinePole DisconnectPoles(long playerId)
        {
            MyRelationsBetweenPlayerAndBlock relation = ModBlock.GetUserRelationToOwner(playerId);

            if (!(relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare))
            {
                MyAPIGateway.Utilities.ShowNotification("Cannot modify powerlines that are not owned by you or your faction", 5000);
                return null;
            }

            PowerlinePole pole = null;
            lock (Links)
            {
                for (int i = Links.Count - 1; i >= 0; i--)
                {
                    PowerlineLink l = Links[i];
                    if (l.PoleA == this)
                    {
                        pole = l.PoleB;
                    }
                    else if (l.PoleB == this)
                    {
                        pole = l.PoleA;
                    }

                    if (pole != null)
                    {
                        requestDetach.SetValue(l, SyncType.Broadcast);
                        return pole;
                    }
                }
            }

            return null;
        }

        public static bool IsInRange(PowerlinePole a, PowerlinePole b)
        {
            IMyTerminalBlock blockA = a.ModBlock;
            IMyTerminalBlock blockB = b.ModBlock;

            Vector3 pos1 = Tools.GetDummyRelativeLocation(blockA);
            Vector3 pos2 = Tools.GetDummyRelativeLocation(blockB);
            double distance = (pos1 - pos2).LengthSquared();

            if (blockA.CubeGrid.IsStatic && blockB.CubeGrid.IsStatic)
            {
                return distance < Settings.Instance.MaxCableDistanceStaticToStatic * Settings.Instance.MaxCableDistanceStaticToStatic;
            }
            else if (blockA.CubeGrid.GridSizeEnum == MyCubeSize.Small && blockB.CubeGrid.GridSizeEnum == MyCubeSize.Small)
            {
                return distance < Settings.Instance.MaxCableDistanceSmallToSmall * Settings.Instance.MaxCableDistanceSmallToSmall;
            }
            else if (blockA.CubeGrid.GridSizeEnum == MyCubeSize.Large && blockB.CubeGrid.GridSizeEnum == MyCubeSize.Large)
            {
                return distance < Settings.Instance.MaxCableDistanceLargeToLarge * Settings.Instance.MaxCableDistanceLargeToLarge;
            }
            else if (blockA.CubeGrid.GridSizeEnum != blockB.CubeGrid.GridSizeEnum)
            {
                return distance < Settings.Instance.MaxCableDistanceSmallToLarge * Settings.Instance.MaxCableDistanceSmallToLarge;
            }

            return false;
        }


        private void DrawCable()
        {
            if (MyAPIGateway.Utilities.IsDedicated) return;

            for (int i = 0; i < Links.Count; i++)
            {
                PowerlineLink l = Links[i];
                if (l.PoleA != this || l.PoleB == null) continue;

                // Check if the local player is close enough to render the cable.
                bool playerClose = false;
                var localPlayer = MyAPIGateway.Session?.Player;
                if (localPlayer != null)
                {
                    double localPlayerDistance = Vector3D.DistanceSquared(localPlayer.GetPosition(), ModBlock.WorldMatrix.Translation);
                    playerClose = localPlayerDistance <= Settings.Instance.PlayerDrawDistance * Settings.Instance.PlayerDrawDistance;
                }

                if (playerClose)
                {
                    Vector4 color = Color.DarkGray;
                    float thickness = 0.05f;

                    // Draw the cable line.
                    MySimpleObjectDraw.DrawLine(l.PoleA.DummyAttachPoint, l.PoleB.DummyAttachPoint, cable_vis, ref color, thickness, BlendTypeEnum.Standard);
                }
            }
        }
    }
}