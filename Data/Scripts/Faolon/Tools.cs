using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using Sandbox.Game.EntityComponents;
using VRage.Game.Components;
using VRage.ModAPI;

// Thanks to all the guys who put a lot of work into these.
// I know some could be called directly instead but wrote these intermediate methods to make it more clear what is what and what it returns.

namespace FaolonTether
{
    public class Tools
    {
        public static double GetClosestPlayer(List<IMyPlayer> PlayerList, Vector3D location)
        {
            PlayerList.Clear();
            MyAPIGateway.Players.GetPlayers(PlayerList);

            double closestDistance = double.PositiveInfinity;

            foreach (IMyPlayer player in PlayerList)
            {

                if (player?.Character == null)
                    continue;
                double distance = Vector3D.DistanceSquared(player.Character.WorldMatrix.Translation, location);

                if (distance < closestDistance * closestDistance)
                {
                    closestDistance = distance;
                }
            }

            if (closestDistance == double.PositiveInfinity)
                return -1;

            return Math.Sqrt(closestDistance);
        }

        public static Vector3D GetDummyRelativeLocation(IMyTerminalBlock block)
        {
            Vector3D DummyAttachPoint = Vector3D.Zero;

            IDictionary<string, IMyModelDummy> ModelDummy = new Dictionary<string, IMyModelDummy>();
            var DummyCount = block.Model.GetDummies(ModelDummy);

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

            return DummyAttachPoint;
        }

        public static Vector3D GetDummyRelativeLocation(IMyTerminalBlock endBlock, RaycastData hitblock)
        {
            Vector3D DummyAttachEndPoint = Vector3D.Zero;

            IDictionary<string, IMyModelDummy> ModelDummy = new Dictionary<string, IMyModelDummy>();
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
                Vector3D worldDirection = hitblock.Position - endBlock.WorldMatrix.Translation;
                Vector3D bodyPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(endBlock.WorldMatrix));

                DummyAttachEndPoint = bodyPosition;
            }

            return DummyAttachEndPoint;
        }



        // =====================================================================================================================================================================================================================



        public static Vector3D LocalToWorldPosition(Vector3D localPosition, MatrixD worldPosition)
        {
            return Vector3D.Transform(localPosition, worldPosition);
        }

        public static Vector3D WorldToLocalPosition(Vector3D worldPosition, MatrixD referenceWorldPosition)
        {
            Vector3D worldDirection = worldPosition - referenceWorldPosition.Translation;
            return Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(referenceWorldPosition));
        }

        /// <summary>
        /// Check if block has physics and is not closing/closed. Note it may return false with some blocks that don't physics enabled in the first place, like lights.
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        public static bool IsBlockValid(IMyTerminalBlock block)
        {
            if (block == null || block.CubeGrid.Physics == null || block.MarkedForClose || block.Closed)
                return false;
            return true;
        }

        public static bool IsPlayerInMenus() {
            return MyAPIGateway.Gui.IsCursorVisible || MyAPIGateway.Gui.ChatEntryVisible;
        }


        // =====================================================================================================================================================================================================================



        public class RaycastData
        {
            public IMySlimBlock Block = null;
            public Vector3D Position = Vector3D.Zero;
            public RaycastData(IMySlimBlock block, Vector3D position)
            {
                this.Block = block;
                this.Position = position;
            }
        }

        // Returns a custom data type IMySlimBlock block and Vector3D raycast block relative hit location.
        /// <summary>
        /// Shoots a raycast 10m forward and returns a custom data type 'raycast_data' containing IMySlimBlock block and Vector3D raycast block relative hit location.
        /// </summary>
        /// <param name="origin"></param>
        /// <returns></returns>
        public static RaycastData RayCastGetHitBlock(MatrixD origin)
        {
            Vector3D hitLocation = Vector3D.Zero;
            IMySlimBlock returnBlock = null;
            IHitInfo hit = null;
            MyAPIGateway.Physics.CastRay(origin.Translation + origin.Forward * 0.1, origin.Translation + origin.Forward * 10, out hit);
            if (hit != null && hit.HitEntity is IMyCubeGrid)
            {
                IMyCubeGrid grid = hit.HitEntity as IMyCubeGrid;
                if (grid != null && !grid.MarkedForClose && grid.Physics != null)
                {
                    hitLocation = hit.Position;
                    Vector3I pos = grid.WorldToGridInteger(hit.Position + origin.Forward * 0.1);
                    returnBlock = grid.GetCubeBlock(pos);
                }
            }

            return new RaycastData(returnBlock, hitLocation);
        }

        // If we ever want to implement a more complex notification system, do it here.
        public static void ShowNotification(string text)
        {
            MyAPIGateway.Utilities.ShowNotification(text, 1);
        }

        public static MyModStorageComponentBase GetStorage(IMyEntity entity)
        {
            return entity.Storage ?? (entity.Storage = new MyModStorageComponent());
        }
    }
}
