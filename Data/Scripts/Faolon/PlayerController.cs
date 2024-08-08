using Sandbox.Definitions;
using Sandbox.ModAPI;
using SENetworkAPI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Network;
using VRage.Utils;
using VRageMath;
using static VRageRender.MyBillboard;

namespace FaolonTether
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class PlayerController : MySessionComponentBase
    {
        public enum PlayerState { Idle, Interacting }

        private PlayerState State = PlayerState.Idle;

        private PowerlinePole InteractionObject = null;
        private string highlightedName = string.Empty;

        // Draw Variables
        public const int HIGHLIGHT_PULSE = 300;
        VRageMath.Color color;
        int thick;
        MyStringId cable_vis;

        public const ushort NetworkId = 58936;

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            if (!NetworkAPI.IsInitialized)
            {
                NetworkAPI.Init(NetworkId, "Tether");
            }

            NetworkAPI.LogNetworkTraffic = true;

            // Setup the highlight visuals.
            MyEnvironmentDefinition envDef = MyDefinitionManager.Static.EnvironmentDefinition;
            color = envDef.ContourHighlightColor;
            thick = (int)envDef.ContourHighlightThickness;

            // Setup the cable visuals.
            cable_vis = MyStringId.GetOrCompute("cable");
        }

        public override void UpdateAfterSimulation()
        {
            // client side only check, with an active player in the scene
            if (MyAPIGateway.Utilities.IsDedicated ||
                MyAPIGateway.Session.Player?.Character == null) return;


            if (highlightedName != string.Empty)
            {
                Sandbox.Game.MyVisualScriptLogicProvider.SetHighlightLocal(highlightedName, -1, HIGHLIGHT_PULSE, color, playerId: MyAPIGateway.Session.Player.IdentityId);
                highlightedName = string.Empty;
            }

            DrawCable();

            if (Tools.IsPlayerInMenus()) return;

            if (MyAPIGateway.Session?.Player?.Character == null)
                return;

            IMyEntity tool = MyAPIGateway.Session.Player.Character.EquippedTool;

            if (tool != null)
            {
                MyLog.Default.Info("[Tether] Player is holding a tool, skipping interaction.");
                return;
            }

            bool leftClick = MyAPIGateway.Input.IsNewLeftMouseReleased();
            bool rightClick = MyAPIGateway.Input.IsNewRightMouseReleased();

            if (rightClick && State == PlayerState.Interacting)
            {
                MyLog.Default.Info($"[Tether] Right Click - State: {State}");
                Cancel();
            }

             MatrixD playerMatrix = MyAPIGateway.Session.Player.Character.GetHeadMatrix(true);
            Tools.RaycastData hit = Tools.RayCastGetHitBlock(playerMatrix);

            // stop if there is nothing to interact with
            if (hit.Block == null || hit.Block.FatBlock == null) return;

            // stop if the object is not a power pole
            PowerlinePole pole = hit.Block.FatBlock.GameLogic.GetAs<PowerlinePole>();
            if (pole == null) return;

            // stop if the object is to far away to interact with
            double distance = Vector3D.DistanceSquared(playerMatrix.Translation, hit.Position);
            float interactionDistance = Settings.Instance.InteractionDistance * Settings.Instance.InteractionDistance;
            if (distance > interactionDistance) return;

            if (highlightedName == string.Empty) 
            {
                highlightedName = pole.ModBlock.Name;
                Sandbox.Game.MyVisualScriptLogicProvider.SetHighlightLocal(highlightedName, thick, HIGHLIGHT_PULSE, color, playerId: MyAPIGateway.Session.Player.IdentityId);
            }

            if (leftClick)
            {
                MyLog.Default.Info($"[Tether] Left Click - State: {State}");
                switch (State)
                {
                    case PlayerState.Idle:
                        Select(pole);
                        break;
                    case PlayerState.Interacting:
                        Connect(pole);
                        break;
                }
            }
            else if (rightClick)
            {
                MyLog.Default.Info($"[Tether] Right Click - State: {State}");
                switch (State)
                {
                    case PlayerState.Idle:
                        Disconnect(pole);
                        break;
                    case PlayerState.Interacting:
                        Cancel();
                        break;
                }
            }
        }

        private void DrawCable()
        {
            if (State != PlayerState.Interacting || InteractionObject == null) return;

            PowerlinePole target = null;
            Vector4 color = VRageMath.Color.DarkGray;
            Vector3D endpoint;

            MatrixD playerMatrix = MyAPIGateway.Session.Player.Character.GetHeadMatrix(true);
            Tools.RaycastData hit = Tools.RayCastGetHitBlock(playerMatrix);

            double distance = Vector3D.DistanceSquared(playerMatrix.Translation, hit.Position);
            float interactionDistance = Settings.Instance.InteractionDistance * Settings.Instance.InteractionDistance;

            if (hit.Block != null &&
                hit.Block.FatBlock != null &&
                (target = hit.Block.FatBlock.GameLogic.GetAs<PowerlinePole>()) != null &&
                distance < interactionDistance)
            {
                endpoint = target.DummyAttachPoint;
            }
            else
            {
                endpoint = playerMatrix.Translation + (playerMatrix.Forward * Settings.Instance.InteractionDistance);
            }

            float lineThickness = 0.05f;

            // Log the positions for debugging
            MyLog.Default.Info($"[Tether] InteractionObject Position: {InteractionObject.DummyAttachPoint}");
            MyLog.Default.Info($"[Tether] Endpoint Position: {endpoint}");
            MyLog.Default.Info($"[Tether] Player Position: {playerMatrix.Translation}");

            // Transform positions to player-relative coordinates
            Vector3D relativeInteractionObjectPosition = InteractionObject.DummyAttachPoint - playerMatrix.Translation;
            Vector3D relativeEndpoint = endpoint - playerMatrix.Translation;

            // Log the relative positions for debugging
            MyLog.Default.Info($"[Tether] Relative InteractionObject Position: {relativeInteractionObjectPosition}");
            MyLog.Default.Info($"[Tether] Relative Endpoint Position: {relativeEndpoint}");

            // Draw the cable line using world coordinates directly to avoid issues
            MySimpleObjectDraw.DrawLine(InteractionObject.DummyAttachPoint, endpoint, cable_vis, ref color, lineThickness, BlendTypeEnum.Standard);
        }

        private void Select(PowerlinePole pole)
        {
            State = PlayerState.Interacting;
            InteractionObject = pole;

            MyLog.Default.Info($"[Tether] Select - State: {State}, Interacting: {((InteractionObject == null) ? "null" : InteractionObject.Entity.EntityId.ToString())}");
        }

        private void Connect(PowerlinePole pole)
        {
            if (InteractionObject == null)
            {
                MyLog.Default.Info($"[{Settings.ModName}] cable connection could not occure because the interaction object is null");
                Cancel();
                return;
            }

            InteractionObject.ConnectionPoles(pole, MyAPIGateway.Session.Player);
            Cancel();

            MyLog.Default.Info($"[Tether] Connect - State: {State}, Interacting: {((InteractionObject == null) ? "null" : InteractionObject.Entity.EntityId.ToString())}");
        }

        private void Disconnect(PowerlinePole pole)
        {
            State = PlayerState.Interacting;
            InteractionObject = pole.DisconnectPoles(MyAPIGateway.Session.Player.IdentityId);

            MyLog.Default.Info($"[Tether] Disconnect - State: {State}, Interacting: {((InteractionObject == null) ? "null" : InteractionObject.Entity.EntityId.ToString())}");
        }

        private void Cancel()
        {
            State = PlayerState.Idle;
            InteractionObject = null;
        }
    }
}
