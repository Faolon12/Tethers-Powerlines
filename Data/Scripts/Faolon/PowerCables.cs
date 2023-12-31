using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.Game;
using VRage.Common.Utils;
using VRageMath;
using VRage;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Library.Utils;
using VRage.Game.ModAPI;
using Sandbox.Game.EntityComponents;
using VRage.Input;
using Sandbox.Game.GameSystems;
using VRage.Game.VisualScripting;
using Sandbox.Game.World;
using Sandbox.Game.Components;
using VRageRender.Animations;
using SpaceEngineers.Game.ModAPI;
using ProtoBuf;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using Sandbox.ModAPI.Weapons;
using System.ComponentModel.Design;
using Sandbox.Game.GameSystems.Conveyors;
using Sandbox.Engine.Multiplayer;

using FaolonTether;

// Heavy Industry FIX provided by Twertoon. Big thanks man!

//Dummy name: cable_attach_point

//GUID: 73bb9141-0a4e-4c5a-93ae-f0d9ff23c043
/*

And all public members in C# are PascalCased
arguments are camelCased
*/

namespace FaolonTether.PowerCables
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false, "ChargingStation", "TransformerPylon", "PowerlinePillar", "PowerSockets", "ConveyorHoseAttachment")]
    public class PowerCable : MyGameLogicComponent
    {
        // =====
        // @VARS
        // =====
        // ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        // EXTERNAL CLASSES
        Lib Lib = new Lib();

        // SAVE DATA VARS
        public static long ModId = 2648152224;
        public static ushort NetworkId = 58936;
        public static Guid SETTINGS_GUID = new Guid("73bb9141-0a4e-4c5a-93ae-f0d9ff23c043");
        //public const int SETTINGS_CHANGED_COUNTDOWN = (60 * 1) / 10; // div by 10 because it runs in update10
        public const int SETTINGS_CHANGED_COUNTDOWN = (60 * 1) / 10;
        int syncCountdown;
        public readonly PowerCableBlockSettings Settings = new PowerCableBlockSettings();
        PowerCableMod Mod => PowerCableMod.Instance;

        // INTEGRAL
        IMyTerminalBlock block;
        int STATE = 0;
        Lib.raycast_data hitblock;
        int retryFindBlock = 60;

        // ushort int = 0;

        // GLOBALS
        public IMyTerminalBlock SourceBlock;
        public static Dictionary<long, IMyTerminalBlock> InteractedSourceBlock = new Dictionary<long, IMyTerminalBlock>(); // Block we're pulling the cable from.
        public static List<IMyTerminalBlock> InteractedBlocks = new List<IMyTerminalBlock>();
        public static List<long> Interacters = new List<long>();

        // FLAG VARS
        public bool IsServer;
        public bool IsDedicated;
        //public static bool SetupComplete   = false;
        //public static bool ControlsCreated = false;
        //public static bool ActionsCreated  = false;

        // HIGHLIGHT
        public const int   HIGHLIGHT_PULSE     = 300;
        public const float InteractionDistance = 3.5f;
        bool Interactable = false;
        Color color;
        int thick;

        // CABLES & HOSES DESIGN
        Vector4 col = Color.DarkGray;
        Vector4 colRed = Color.Red;
        Vector4 colGreen = Color.Green;
        MyStringId cable_vis;
        MyStringId cable_vis_Green;
        MyStringId cable_vis_Red;
        MyStringId cable_hose;
        float hand_line_thickness = 0.005f;
        float line_thickness = 0.05f;

        // CABLE & HOSES
        float DynamicGridMaxCableDistance = 20f;
        float DynamicSmallGridMaxCableDistance = 50f;
        float DynamicLargeGridMaxCableDistance = 100f;
        float StaticGridMaxCableDistance = 200f;

        // CONNECTION DATA
        int DummyCount = 0;

        Vector3D DummyAttachPoint = new Vector3D(0, 0, 0); // This block's attach start point.

        Dictionary<string, IMyModelDummy> ModelDummy = new Dictionary<string, IMyModelDummy>();

        GridLinkTypeEnum LinkType = GridLinkTypeEnum.Logical;
        GridLinkTypeEnum LinkTypeTwo = GridLinkTypeEnum.Logical; // OTHER TYPES DON'T WORK SO IT'S SET TO ONE THAT DOESN'T WREAK HAVOC.

        long PlayerCount = 0;
        List<IMyPlayer> PlayerList = new List<IMyPlayer>();

        // OTHER

        // ================
        // SAVED PROPERTIES
        // ================

        // Var to store and save the ID of the block we connect to.
        public long ConnectedBlockId
        {
            get { return Settings.ConnectedBlockId; }
            set
            {
                Settings.ConnectedBlockId = value;

                SettingsChanged();
            }
        }

        // Var to store and save the cable attach location of the block we connect to.
        public Vector3D ConnectedBlockAttachLocation
        {
            get { return Settings.ConnectedBlockAttachLocation; }
            set
            {
                Settings.ConnectedBlockAttachLocation = value;

                SettingsChanged();
            }
        }

        // Var to store and save the ID of the connection between this block and block we connect to.
        public long ConnectionId
        {
            get { return Settings.ConnectionId; }
            set
            {
                Settings.ConnectionId = value;

                SettingsChanged();
            }
        }

        public IMyTerminalBlock ConnectedBlock;
        public Vector3D ConnectedBlockRelativeAttachLocation;

        private readonly HashSet<MyObjectBuilderType> allowedBlockTypes = new HashSet<MyObjectBuilderType>()
        {
            typeof(MyObjectBuilder_InteriorLight)
        };

        // ====
        // @???
        // ====
        // ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        // Might as well make it interesting.
        bool SelfAware = true;
        string[] rand_notification_self_attach_question = {
            "INFINITE POWAAAAAAAAAA...",
            "Trying to create an infinite power loop?",
            "Are you trying to place a cable on myself?",
            "Did you just?",
            "Trying it on me?",
            "A powerline leading to itself?",
            "Are you sure?",
            "You're really trying to blow out a fuse, aren't ya?",
        };
        string[] rand_notification_self_attach_acknowledge = {
            "How radical.",
            "Really?",
            "Well...",
            "What kind of engineer are you?",
            "Is that what you want?",
            "You'll short the damn thing.",
            "You better call an electrician.",
            "I see you know your way with cabling. I'm being sarcastic btw.",
        };
        string[] rand_notification_self_attach_special = {
            "You know this could lead to creating a black hole, right?",
            "Wanna end the universe?",
            "YOU'LL KILL US ALL!",
            "You'll blow us up.",
        };
        string[] rand_notification_self_attach_response = {
            "DENIED",
            "So... DENIED",
            "No.",
            "Nope.",
            "Can't do that.",
            "Won't do that.",
            "Can't allow that.",
        };

        // ========
        // @METHODS
        // ========
        // ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        static PowerCable GetLogic(IMyTerminalBlock block) => block?.GameLogic?.GetAs<PowerCable>();

        // =====
        // @INIT
        // =====
        // ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = Entity as IMyTerminalBlock;
            if (block == null)
                return;

            // Get server status.
            // Because try manage most things on server and only do visuals on clients.
            IsServer = MyAPIGateway.Multiplayer.IsServer;
            IsDedicated = MyAPIGateway.Utilities.IsDedicated;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;
        }

        // ======
        // @CLOSE
        // ======
        // ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        public override void Close()
        {
            // Cleanup.
            if (block != null)
            {
                /*
                if (MyAPIGateway.Multiplayer.IsServer && block.BlockDefinition.SubtypeId == "PowerlinePillar")
                    block.CubeGrid.OnBlockAdded -= GridBlockAdded;
                */

                if (ConnectedBlock != null)
                {
                    if (block.IsInSameLogicalGroupAs(ConnectedBlock))
                    {
                        if (block.BlockDefinition.SubtypeId == "ConveyorHoseAttachment" && ConnectedBlock.BlockDefinition.SubtypeId == "ConveyorHoseAttachment")
                        {
                            MyCubeGrid.BreakGridGroupLink(LinkTypeTwo, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                            MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Electrical, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                        }
                        else
                        {
                            MyCubeGrid.BreakGridGroupLink(LinkType, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                            MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Electrical, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                        }
                        ConnectedBlock = null;
                    }
                }

                block = null;
                NeedsUpdate = MyEntityUpdateEnum.NONE;
            }
        }

        // ======
        // @FUNCTIONS
        // ======
        // ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        // Restrict block placement to a few block types that make most sense to be placed onto a powerline pillar.
        // Not yet implemented.
        private void GridBlockAdded(IMySlimBlock obj)
        {
            try
            {
                /*
                var blockCube = block as MyCubeBlock;
                var blockSize = blockCube.SlimBlock.BlockDefinition.Size;
                var blockPos = block.SlimBlock.Position + blockSize.Z;
                */

                var blockPos = block.SlimBlock.Position + (block.SlimBlock.Position.Z + 6) + (block.SlimBlock.Position.Y - 1);
                var placedBlock = block.CubeGrid.GetCubeBlock(blockPos);

                var def = obj.BlockDefinition;

                if (obj == placedBlock && !allowedBlockTypes.Contains(def.Id.TypeId))
                {
                    MyAPIGateway.Utilities.ShowNotification("Can't place '" + def.DisplayNameText + "'.", 5000);
                    obj.CubeGrid.RemoveBlock(obj);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        // =====
        // @ONCE
        // =====
        // ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                /*
                if (MyAPIGateway.Session?.Player == null)
                {
                    NeedsUpdate = MyEntityUpdateEnum.NONE;
                    return;
                }
                */

                // Check if block is fully loaded in/not a projection.
                if (block.CubeGrid?.Physics == null)
                    return;

                /*
                if (MyAPIGateway.Multiplayer.IsServer && block.BlockDefinition.SubtypeId == "PowerlinePillar")
                    block.CubeGrid.OnBlockAdded += GridBlockAdded;
                */

                // Save/load terminal settings. Took from Digi :P <3
                //Settings.AutoRollEnabled = false;

                Settings.ConnectedBlockId = -1;
                Settings.ConnectedBlockAttachLocation = Vector3D.Zero;
                Settings.ConnectionId = -1;

                var result = LoadSettings();
                Log.Info("BeforeFrame: " + block.EntityId + " - " + result + " - " + Settings.ConnectedBlockId);

                SaveSettings();

                // Check if this block has it's ID saved, which means we made a connection.
                // Retrieve the other block and the location of the attach.
                if (ConnectedBlockId != -1)
                {
                    var cb = (IMyTerminalBlock)MyAPIGateway.Entities.GetEntityById(Settings.ConnectedBlockId);
                    if (cb == null && retryFindBlock > 0)
                    {
                        retryFindBlock--;
                        NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                        Log.Info("BeforeFrame retry: " + block.EntityId);
                        return;
                    }

                    ConnectedBlock = cb;
                    ConnectedBlockRelativeAttachLocation = Settings.ConnectedBlockAttachLocation;
                }

                // Make sure we actually have a saved block to connect to.
                if (ConnectedBlock != null)
                {
                    if (!block.IsInSameLogicalGroupAs(ConnectedBlock))
                    {
                        // Check if it's a conveyor block for conevyor connection, otherwise make it a logical connection. (CONV. CONN. DOESN'T WORK SO IT'S LOCIGAL TO AVOID ISSUES)
                        if (block.BlockDefinition.SubtypeId == "ConveyorHoseAttachment" && ConnectedBlock.BlockDefinition.SubtypeId == "ConveyorHoseAttachment")
                        {
                            MyCubeGrid.CreateGridGroupLink(LinkTypeTwo, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                            MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Electrical, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                        }
                        else
                        {
                            MyCubeGrid.CreateGridGroupLink(LinkType, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                            MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Electrical, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                        }
                    }
                }
                else
                {
                    ConnectedBlockId = -1;
                }

                PlayerCount = MyAPIGateway.Players.Count;
                if (PlayerCount > 0)
                    MyAPIGateway.Players.GetPlayers(PlayerList);

                //IMyTerminalBlock someBlock = block.CubeGrid.
                //public IMyTerminalBlock AttachPointStartBlock;          // Stores the first interacted block.
                //public long AttachPointStartBlockId;
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            // Setup the highlight visuals.
            MyEnvironmentDefinition envDef = MyDefinitionManager.Static.EnvironmentDefinition;
            color = envDef.ContourHighlightColor;
            thick = (int)envDef.ContourHighlightThickness;

            // Setup the cable visuals.
            cable_vis = MyStringId.GetOrCompute("cable");
            cable_vis_Green = MyStringId.GetOrCompute("cableGreen");
            cable_vis_Red = MyStringId.GetOrCompute("cableRed");
            cable_hose = MyStringId.GetOrCompute("cable");

            DummyAttachPoint = Lib.GetDummyRelativeLocation(block);

            /*
            DummyCount = block.Model.GetDummies(ModelDummy);
            if (DummyCount > 1)
            {
                // TODO Add logic for multiple dummies per block. For now, staticaly only takes the first one.
                if (ModelDummy.ContainsKey("cable_attach_point_1"))
                {
                    Vector3D DummyLoc = ModelDummy["cable_attach_point_1"].Matrix.Translation;
                    Vector3D worldPosition = Vector3D.Transform(DummyLoc, block.WorldMatrix);
                    DummyAttachPoint = worldPosition;
                }
                else if (ModelDummy.ContainsKey("cable_attach_point"))
                {
                    Vector3D DummyLoc = ModelDummy["cable_attach_point"].Matrix.Translation;
                    Vector3D worldPosition = Vector3D.Transform(DummyLoc, block.WorldMatrix);
                    DummyAttachPoint = worldPosition;
                }
            }
            else
            {
                if (ModelDummy.ContainsKey("cable_attach_point"))
                {
                    Vector3D DummyLoc = ModelDummy["cable_attach_point"].Matrix.Translation;
                    Vector3D worldPosition = Vector3D.Transform(DummyLoc, block.WorldMatrix);
                    DummyAttachPoint = worldPosition;
                }
            }

            ModelDummy.Clear();
            */

            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }

        // =======
        // @BEFORE
        // =======
        // ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        public override void UpdateBeforeSimulation()
        {
            try
            {
                SyncSettings();

                if (!block.IsFunctional)
                 return;

                if (MyAPIGateway.Session?.Player?.Character == null)
                    return;

                // Only allow one block to be interactable at a time. If we're interacting with another block, ignore this block completely.
                /*
                if (SourceBlock != null && SourceBlock != block)
                {
                    return;
                }
                */

                if (InteractedBlocks.Contains(block) || Interacters.Contains(MyAPIGateway.Session.Player.IdentityId))
                {
                    //MyAPIGateway.Utilities.ShowNotification("Already in use.");
                    return;
                }

                MatrixD PLayerCharHeadMatrix = MyAPIGateway.Session.Player.Character.GetHeadMatrix(true);
                IMyEntity tool = MyAPIGateway.Session.Player.Character.EquippedTool;

                hitblock = Lib.RayCastGetHitBlock(PLayerCharHeadMatrix);

                var Highlight = false;
                if (hitblock != null && hitblock.hit_block != null)
                {
                    double distance = Vector3D.DistanceSquared(PLayerCharHeadMatrix.Translation, hitblock.hit_location);
                    if (distance <= InteractionDistance * InteractionDistance)
                    {
                        if (hitblock.hit_block == block.SlimBlock)
                        {
                            if (block.CubeGrid.Physics != null && block.IsFunctional && tool == null)
                                Highlight = true;
                        }
                    }
                }

                if (Highlight)
                {
                    if (!Interactable)
                    {
                        Interactable = true;
                        Sandbox.Game.MyVisualScriptLogicProvider.SetHighlightLocal(block.Name, thick, HIGHLIGHT_PULSE, color, playerId: MyAPIGateway.Session.Player.IdentityId);
                    }
                }
                else
                {
                    if (Interactable)
                    {
                        Interactable = false;
                        Sandbox.Game.MyVisualScriptLogicProvider.SetHighlightLocal(block.Name, -1, HIGHLIGHT_PULSE, color, playerId: MyAPIGateway.Session.Player.IdentityId);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        // ======
        // @AFTER
        // ======
        // ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        public override void UpdateAfterSimulation()
        {
            try
            {
                if (MyAPIGateway.Session?.Player?.Character == null)
                    return;

                IMyEntity tool = MyAPIGateway.Session.Player.Character.EquippedTool;

                // Are we in a cable placement state?
                if (STATE == 1)
                {
                    // Are we broken or deleted?
                    if (!block.IsFunctional || !Lib.IsBlockValid(block) || tool != null)
                    {
                        if (InteractedBlocks.Contains(block))
                        {
                            InteractedBlocks.Remove(block);
                            if (Interacters.Contains(MyAPIGateway.Session.Player.IdentityId))
                                Interacters.Remove(MyAPIGateway.Session.Player.IdentityId);
                        }

                        // Cancel everything.
                        SourceBlock = null; // We're not interacting with anything anymore, set source to null.
                        STATE = 0; // Set state to idle placement.
                        return;
                    }
                }

                if (MyAPIGateway.Input.IsNewRightMouseReleased() && !MyAPIGateway.Gui.IsCursorVisible && !MyAPIGateway.Gui.ChatEntryVisible)
                {
                    if (STATE == 1)
                    {
                        if (InteractedBlocks.Contains(block))
                        {
                            InteractedBlocks.Remove(block);
                            if (Interacters.Contains(MyAPIGateway.Session.Player.IdentityId))
                                Interacters.Remove(MyAPIGateway.Session.Player.IdentityId);
                        }

                        SourceBlock = null; // We're not interacting with anything anymore, set source to null.
                        STATE = 0; // Set state to idle placement.
                        MyAPIGateway.Utilities.ShowNotification("Cancel."); // Me: Sup? Block: ... <-
                    }
                    else
                    {
                        if (Interactable)
                        {
                            if (ConnectedBlock != null)
                            {
                                //MyCubeGrid.BreakGridGroupLink(LinkType, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                if (block.BlockDefinition.SubtypeId == "ConveyorHoseAttachment" && ConnectedBlock.BlockDefinition.SubtypeId == "ConveyorHoseAttachment")
                                {
                                    MyCubeGrid.BreakGridGroupLink(LinkTypeTwo, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                    MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Electrical, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                }
                                else
                                {
                                    MyCubeGrid.BreakGridGroupLink(LinkType, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                    MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Electrical, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                }
                                ConnectedBlock = null;
                            }

                            if (InteractedBlocks.Contains(block))
                            {
                                InteractedBlocks.Remove(block);
                                if (Interacters.Contains(MyAPIGateway.Session.Player.IdentityId))
                                    Interacters.Remove(MyAPIGateway.Session.Player.IdentityId);
                            }

                            SourceBlock = null; // We're not interacting with anything anymore, set source to null.
                            STATE = 0; // Set state to idle placement.

                            ConnectedBlockId = -1;
                            MyAPIGateway.Utilities.ShowNotification("Remove connections."); // Me: Sup? Block: ... <-
                        }
                    }
                    return;
                }

                if (MyAPIGateway.Input.IsNewLeftMouseReleased() && !MyAPIGateway.Gui.IsCursorVisible && !MyAPIGateway.Gui.ChatEntryVisible)
                {
                    if (STATE == 0)
                    {
                        if (!Interactable) // Skip if we're not the one interactable.
                            return;

                        if (!InteractedBlocks.Contains(block))
                        {
                            InteractedBlocks.Add(block);
                            if (!Interacters.Contains(MyAPIGateway.Session.Player.IdentityId))
                                Interacters.Add(MyAPIGateway.Session.Player.IdentityId);
                        }

                        SourceBlock = block; // Set the source block to this block.
                        STATE = 1; // Set state to cable placement.
                        MyAPIGateway.Utilities.ShowNotification("Cable placement."); // Me: Tell me what you're up to. Block: ... <-

                        //@@@
                        //var dummyCount = Lib.GetDummyWorldLocation(block, "")

                        DummyAttachPoint = Lib.GetDummyRelativeLocation(block);
                    }
                    else if (STATE == 1)
                    {
                        MatrixD PLayerCharHeadMatrix = MyAPIGateway.Session.Player.Character.GetHeadMatrix(true);

                        hitblock = Lib.RayCastGetHitBlock(PLayerCharHeadMatrix);

                        if (hitblock != null && hitblock.hit_block != null)
                        {
                            IMyTerminalBlock endBlock = hitblock.hit_block.FatBlock as IMyTerminalBlock;

                            if (endBlock == null)
                                return;

                            double distance = Vector3D.DistanceSquared(PLayerCharHeadMatrix.Translation, hitblock.hit_location);
                            if (distance <= InteractionDistance * InteractionDistance)
                            {
                                if (hitblock.hit_block == block.SlimBlock)
                                {
                                    if (SelfAware)
                                    {
                                        int rand_index = MyUtils.GetRandomInt(rand_notification_self_attach_question.Length);

                                        string text = rand_notification_self_attach_question[rand_index];
                                        MyAPIGateway.Utilities.ShowNotification(text, 5000);

                                        if (rand_index < 2)
                                        {
                                            text = rand_notification_self_attach_special[MyUtils.GetRandomInt(rand_notification_self_attach_special.Length)];
                                            MyAPIGateway.Utilities.ShowNotification(text, 5000);
                                        }
                                        else
                                        {
                                            text = rand_notification_self_attach_acknowledge[MyUtils.GetRandomInt(rand_notification_self_attach_acknowledge.Length)];
                                            MyAPIGateway.Utilities.ShowNotification(text, 5000);
                                        }

                                        text = rand_notification_self_attach_response[MyUtils.GetRandomInt(rand_notification_self_attach_response.Length)];
                                        MyAPIGateway.Utilities.ShowNotification(text, 5000); // Me: Can i? Block: ... <-
                                    }
                                    else
                                    {
                                        MyAPIGateway.Utilities.ShowNotification("Can't attach cable on itself.", 5000); // Me: Can i? Block: ... <-
                                    }
                                }
                                else if (hitblock.hit_block.CubeGrid == block.SlimBlock.CubeGrid)
                                {
                                    MyAPIGateway.Utilities.ShowNotification("Can't attach cable on block that is part of the same grid.", 5000); // Me: Can i? Block: ... <-
                                }
                                else
                                {
                                    // LOGIC TO ATTACH CABLE
                                    if (!block.IsInSameLogicalGroupAs(endBlock))
                                    {
                                        if (!endBlock.IsFunctional || endBlock.MarkedForClose || endBlock.Closed)
                                        {
                                            MyAPIGateway.Utilities.ShowNotification("Can't attach cable on broken blocks.", 5000); // Me: Can i? Block: ... <-
                                        }
                                        else
                                        {
                                            var DummyAttachEndPoint = Lib.GetDummyRelativeLocation(endBlock, hitblock);

                                            /*
                                            var DummyAttachEndPoint = Vector3D.Zero;
                                            ModelDummy.Clear();
                                            var DummyCount = endBlock.Model.GetDummies(ModelDummy);
                                            if (ModelDummy.ContainsKey("cable_attach_point"))
                                            {
                                                Vector3D DummyLoc = ModelDummy["cable_attach_point"].Matrix.Translation;
                                                DummyAttachEndPoint = DummyLoc;
                                            }
                                            else if (ModelDummy.ContainsKey("cable_attach_point_1"))
                                            {
                                                Vector3D DummyLoc = ModelDummy["cable_attach_point_1"].Matrix.Translation;
                                                DummyAttachEndPoint = DummyLoc;
                                            }
                                            else
                                            {
                                                Vector3D worldDirection = hitblock.hit_location - endBlock.WorldMatrix.Translation;
                                                Vector3D bodyPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(endBlock.WorldMatrix));

                                                DummyAttachEndPoint = bodyPosition;
                                            }
                                            */

                                            Vector3D startAttach = Vector3D.Transform(DummyAttachPoint, block.WorldMatrix);
                                            Vector3D endAttach = Vector3D.Transform(DummyAttachEndPoint, endBlock.WorldMatrix);
                                            distance = Vector3D.DistanceSquared(startAttach, endAttach);
                                            bool tooFar = false;

                                            //@@if (distance > )
                                            if (block.CubeGrid.IsStatic && endBlock.CubeGrid.IsStatic)
                                            {
                                                if (distance > StaticGridMaxCableDistance * StaticGridMaxCableDistance)
                                                    tooFar = true;
                                            }
                                            else if (block.CubeGrid.GridSizeEnum == MyCubeSize.Large && endBlock.CubeGrid.GridSizeEnum == MyCubeSize.Large)
                                            {
                                                if (distance > DynamicLargeGridMaxCableDistance * DynamicLargeGridMaxCableDistance)
                                                    tooFar = true;
                                            }
                                            else if (block.CubeGrid.GridSizeEnum == MyCubeSize.Small && endBlock.CubeGrid.GridSizeEnum == MyCubeSize.Small)
                                            {
                                                if (distance > DynamicGridMaxCableDistance * DynamicGridMaxCableDistance)
                                                    tooFar = true;
                                            }
                                            else
                                            {
                                                if (distance > DynamicSmallGridMaxCableDistance * DynamicSmallGridMaxCableDistance)
                                                    tooFar = true;
                                            }

                                            if (tooFar)
                                            {
                                                MyAPIGateway.Utilities.ShowNotification("Too far away.", 5000);
                                            }
                                            else
                                            {
                                                if (ConnectedBlock != null && block.IsInSameLogicalGroupAs(ConnectedBlock))
                                                {
                                                    //MyCubeGrid.BreakGridGroupLink(LinkType, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                                    if (block.BlockDefinition.SubtypeId == "ConveyorHoseAttachment" && ConnectedBlock.BlockDefinition.SubtypeId == "ConveyorHoseAttachment")
                                                    {
                                                        MyCubeGrid.BreakGridGroupLink(LinkTypeTwo, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                                        MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Electrical, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                                    }
                                                    else
                                                    {
                                                        MyCubeGrid.BreakGridGroupLink(LinkType, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                                        MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Electrical, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                                    }
                                                }

                                                ConnectedBlockId = endBlock.EntityId;
                                                ConnectedBlockAttachLocation = DummyAttachEndPoint;
                                                ConnectedBlock = endBlock;

                                                //MyCubeGrid.CreateGridGroupLink(LinkType, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                                if (block.BlockDefinition.SubtypeId == "ConveyorHoseAttachment" && ConnectedBlock.BlockDefinition.SubtypeId == "ConveyorHoseAttachment")
                                                {
                                                    MyCubeGrid.CreateGridGroupLink(LinkTypeTwo, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                                    MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Electrical, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                                    MyAPIGateway.Utilities.ShowNotification("Mechanical connection.", 5000);
                                                }
                                                else
                                                {
                                                    MyCubeGrid.CreateGridGroupLink(LinkType, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                                    MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Electrical, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                                    MyAPIGateway.Utilities.ShowNotification("Logical connection.", 5000);
                                                }

                                                //MyAPIGateway.Utilities.ShowNotification("Cable connected.", 5000);

                                                if (InteractedBlocks.Contains(block))
                                                {
                                                    InteractedBlocks.Remove(block);
                                                    if (Interacters.Contains(MyAPIGateway.Session.Player.IdentityId))
                                                        Interacters.Remove(MyAPIGateway.Session.Player.IdentityId);
                                                }

                                                SourceBlock = null; // We're not interacting with anything anymore, set source to null.
                                                STATE = 0; // Set state to idle placement.

                                                //MyAPIGateway.Utilities.ShowNotification("Connected grids:\n" + block.CubeGrid.ToString() + "\n" + endBlock.CubeGrid.ToString(), 5000);
                                            }
                                        }

                                    }
                                    else
                                    {
                                        if (ConnectedBlock != null && block.IsInSameLogicalGroupAs(ConnectedBlock))
                                        {
                                            //MyCubeGrid.BreakGridGroupLink(LinkType, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                            if (block.BlockDefinition.SubtypeId == "ConveyorHoseAttachment" && ConnectedBlock.BlockDefinition.SubtypeId == "ConveyorHoseAttachment")
                                            {
                                                MyCubeGrid.BreakGridGroupLink(LinkTypeTwo, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                                MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Electrical, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                            }
                                            else
                                            {
                                                MyCubeGrid.BreakGridGroupLink(LinkType, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                                MyCubeGrid.CreateGridGroupLink(GridLinkTypeEnum.Electrical, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                                            }

                                            ConnectedBlockId = -1;
                                            ConnectedBlock = null;

                                            if (InteractedBlocks.Contains(block))
                                            {
                                                InteractedBlocks.Remove(block);
                                                if (Interacters.Contains(MyAPIGateway.Session.Player.IdentityId))
                                                    Interacters.Remove(MyAPIGateway.Session.Player.IdentityId);
                                            }

                                            SourceBlock = null; // We're not interacting with anything anymore, set source to null.
                                            STATE = 0; // Set state to idle placement.

                                            MyAPIGateway.Utilities.ShowNotification("Disconnected.", 5000);
                                        }

                                        MyAPIGateway.Utilities.ShowNotification("Already connected with another tether.", 5000);
                                        //MyAPIGateway.Utilities.ShowNotification("Disconnected grids:\n" + block.CubeGrid.ToString() + "\n" + endBlock.CubeGrid.ToString(), 5000);
                                    }
                                }
                            }
                        }
                    }
                }

                // DRAW CABLE PLACEMENT
                if (STATE == 1)
                {
                    Vector3D startAttach = Vector3D.Transform(DummyAttachPoint, block.WorldMatrix);
                    MatrixD PLayerCharHeadMatrix = MyAPIGateway.Session.Player.Character.GetHeadMatrix(true);

                    hitblock = Lib.RayCastGetHitBlock(PLayerCharHeadMatrix);

                    if (hitblock != null && hitblock.hit_block != null)
                    {
                        double distance = Vector3D.DistanceSquared(PLayerCharHeadMatrix.Translation, hitblock.hit_location);
                        if (distance <= InteractionDistance * InteractionDistance)
                        {
                            if (hitblock.hit_block != block.SlimBlock)
                            {
                                IMyTerminalBlock endBlock = hitblock.hit_block.FatBlock as IMyTerminalBlock;
                                if (endBlock == null || endBlock.CubeGrid == block.CubeGrid || block.IsInSameLogicalGroupAs(endBlock))
                                {
                                    MySimpleObjectDraw.DrawLine(startAttach, hitblock.hit_location,
                                    cable_vis_Red, ref colRed, line_thickness, BlendTypeEnum.Standard);
                                }
                                else
                                {
                                    MySimpleObjectDraw.DrawLine(startAttach, hitblock.hit_location,
                                    cable_vis_Green, ref colGreen, line_thickness, BlendTypeEnum.Standard);
                                }
                            }
                            else
                            {
                                MySimpleObjectDraw.DrawLine(startAttach, hitblock.hit_location,
                                cable_vis_Red, ref colRed, line_thickness, BlendTypeEnum.Standard);
                            }
                        }
                        else
                        {
                            MySimpleObjectDraw.DrawLine(startAttach, PLayerCharHeadMatrix.Translation + PLayerCharHeadMatrix.Forward * 0.5f,
                            cable_vis, ref col, hand_line_thickness, BlendTypeEnum.Standard);
                        }
                    }
                    else
                    {
                        MySimpleObjectDraw.DrawLine(startAttach, PLayerCharHeadMatrix.Translation + PLayerCharHeadMatrix.Forward * 0.5f,
                        cable_vis, ref col, hand_line_thickness, BlendTypeEnum.Standard);
                    }
                }

                // DRAW CABLE TO CONNECTED BLOCK
                if (ConnectedBlock != null)
                {
                    Vector3D startAttach = Vector3D.Transform(DummyAttachPoint, block.WorldMatrix);
                    Vector3D endAttach = Vector3D.Transform(ConnectedBlockAttachLocation, ConnectedBlock.WorldMatrix);
                    double distance = Vector3D.DistanceSquared(startAttach, endAttach);
                    bool breakConnection = false;
                    if (block.CubeGrid.IsStatic && ConnectedBlock.CubeGrid.IsStatic)
                    {
                        if (distance > StaticGridMaxCableDistance * StaticGridMaxCableDistance)
                            breakConnection = true;
                    }
                    else
                    {
                        if (distance > DynamicGridMaxCableDistance * StaticGridMaxCableDistance)
                            breakConnection = true;
                    }

                    // Break connection if any of the blocks isn't functional, deleted or too far out.
                    // The connection could remain even when block isn't functional but since the cable would
                    // most certainly be floating somewhere where seemingly no physical attachment would be possible on the construction models.
                    if (breakConnection || !ConnectedBlock.IsFunctional || ConnectedBlock.MarkedForClose || ConnectedBlock.Closed || !block.IsFunctional || block.MarkedForClose || block.Closed)
                    {
                        //MyCubeGrid.BreakGridGroupLink(LinkType, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                        if (block.BlockDefinition.SubtypeId == "ConveyorHoseAttachment" && ConnectedBlock.BlockDefinition.SubtypeId == "ConveyorHoseAttachment")
                        {
                            MyCubeGrid.BreakGridGroupLink(LinkTypeTwo, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                            MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Electrical, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                        }
                        else
                        {
                            MyCubeGrid.BreakGridGroupLink(LinkType, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                            MyCubeGrid.BreakGridGroupLink(GridLinkTypeEnum.Electrical, block.EntityId, (MyCubeGrid)block.CubeGrid, (MyCubeGrid)ConnectedBlock.CubeGrid);
                        }
                        ConnectedBlock = null;
                        return;
                    }

                    // Player count check for player distance.
                    if (PlayerCount != MyAPIGateway.Players.Count)
                    {
                        PlayerCount = MyAPIGateway.Players.Count;
                        if (PlayerCount > 0)
                        {
                            PlayerList.Clear();
                            MyAPIGateway.Players.GetPlayers(PlayerList);
                        }
                    }

                    // Check if the local player is close enough to render the cable.
                    bool playerClose = false;
                    var localPlayer = MyAPIGateway.Session?.Player;
                    if (localPlayer != null)
                    {
                        double localPlayerDistance = Vector3D.Distance(localPlayer.GetPosition(), block.WorldMatrix.Translation);
                        playerClose = localPlayerDistance <= 3000;
                    }

                    if (!MyAPIGateway.Utilities.IsDedicated && playerClose)
                    {
                        var drawCable = cable_vis;
                        var drawCableThickness = line_thickness;

                        // Additional conditions for specific cable types.
                        if (block.BlockDefinition.SubtypeId == "ConveyorHoseAttachment" && ConnectedBlock.BlockDefinition.SubtypeId == "ConveyorHoseAttachment")
                        {
                            drawCable = cable_hose;
                            drawCableThickness = 0.15f;
                        }

                        // Draw the cable line.
                        MySimpleObjectDraw.DrawLine(startAttach, endAttach, drawCable, ref col, drawCableThickness, BlendTypeEnum.Standard);
                    }

                }
              //      int++;
              //  if (int > ushort.MaxValue)
              //      int = 0;
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        // ===============
        // @LOAD SAVE DATA
        // ===============
        // ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        // =================
        // @Digi's FUNCTIONS
        // =================

        // @Digi storage and serialization
        bool LoadSettings()
        {
            if (block.Storage == null)
                return false;

            string rawData;
            if (!block.Storage.TryGetValue(SETTINGS_GUID, out rawData))
                return false;

            try
            {
                var loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<PowerCableBlockSettings>(Convert.FromBase64String(rawData));

                if (loadedSettings != null)
                {
                    //Settings.AutoRollEnabled = loadedSettings.AutoRollEnabled;
                    //Settings.cable_draw = loadedSettings.cable_draw;
                    Settings.ConnectedBlockId = loadedSettings.ConnectedBlockId;
                    Settings.ConnectedBlockAttachLocation = loadedSettings.ConnectedBlockAttachLocation;
                    Settings.ConnectionId = loadedSettings.ConnectionId;
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.Error($"Error loading settings!\n{e}");
            }

            return false;
        }

        // @Digi storage and serialization
        void SaveSettings()
        {
            if (block == null)
                return; // called too soon or after it was already closed, ignore

            if (Settings == null)
                throw new NullReferenceException($"Settings == null on entId={Entity?.EntityId}; modInstance={PowerCableMod.Instance != null}");

            if (MyAPIGateway.Utilities == null)
                throw new NullReferenceException($"MyAPIGateway.Utilities == null; entId={Entity?.EntityId}; modInstance={PowerCableMod.Instance != null}");

            if (block.Storage == null)
                block.Storage = new MyModStorageComponent();

            block.Storage.SetValue(SETTINGS_GUID, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(Settings)));
        }

        // @Digi storage and serialization
        void SettingsChanged()
        {
            if (syncCountdown == 0)
                syncCountdown = SETTINGS_CHANGED_COUNTDOWN;
        }

        // @Digi storage and serialization
        void SyncSettings()
        {
            if (syncCountdown > 0 && --syncCountdown <= 0)
            {
                SaveSettings();

                Mod.CachedPacketSettings.Send(block.EntityId, Settings);
            }
        }

        // @Digi storage and serialization
        public override bool IsSerialized()
        {
            // called when the game iterates components to check if they should be serialized, before they're actually serialized.
            // this does not only include saving but also streaming and blueprinting.
            // NOTE for this to work reliably the MyModStorageComponent needs to already exist in this block with at least one element.

            try
            {
                SaveSettings();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            return base.IsSerialized();
        }
    }
}