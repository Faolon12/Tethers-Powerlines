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

// Thanks to all the guys who put a lot of work into these.
// I know some could be called directly instead but wrote these intermediate methods to make it more clear what is what and what it returns.

namespace KlimeAndPsycho
{
    class Lib
    {
        public double GetClosestPlayer(List<IMyPlayer> PlayerList, Vector3D location)
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

        public Vector3D GetDummyRelativeLocation(IMyTerminalBlock block)
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
                    DummyAttachPoint = DummyLoc;
                }
                else if (ModelDummy.ContainsKey("cable_attach_point"))
                {
                    Vector3D DummyLoc = ModelDummy["cable_attach_point"].Matrix.Translation;
                    Vector3D worldPosition = Vector3D.Transform(DummyLoc, block.WorldMatrix);
                    DummyAttachPoint = DummyLoc;
                }
            }
            else
            {
                if (ModelDummy.ContainsKey("cable_attach_point"))
                {
                    Vector3D DummyLoc = ModelDummy["cable_attach_point"].Matrix.Translation;
                    Vector3D worldPosition = Vector3D.Transform(DummyLoc, block.WorldMatrix);
                    DummyAttachPoint = DummyLoc;
                }
            }

            return DummyAttachPoint;
        }

        public Vector3D GetDummyRelativeLocation(IMyTerminalBlock endBlock, raycast_data hitblock)
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
                Vector3D worldDirection = hitblock.hit_location - endBlock.WorldMatrix.Translation;
                Vector3D bodyPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(endBlock.WorldMatrix));

                DummyAttachEndPoint = bodyPosition;
            }

            return DummyAttachEndPoint;
        }



        // =====================================================================================================================================================================================================================



        public Vector3D LocalToWorldPosition(Vector3D localPosition, MatrixD worldPosition)
        {
            return Vector3D.Transform(localPosition, worldPosition);
        }

        public Vector3D WorldToLocalPosition(Vector3D worldPosition, MatrixD referenceWorldPosition)
        {
            Vector3D worldDirection = worldPosition - referenceWorldPosition.Translation;
            return Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(referenceWorldPosition));
        }

        /// <summary>
        /// Check if block has physics and is not closing/closed. Note it may return false with some blocks that don't physics enabled in the first place, like lights.
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        public bool IsBlockValid(IMyTerminalBlock block)
        {
            //if (!block.IsFunctional || block.CubeGrid.Physics == null || !block.IsWorking || block.MarkedForClose || block.Closed)
            if (block.CubeGrid.Physics == null || block.MarkedForClose || block.Closed)
                return false;
            return true;
        }



        // =====================================================================================================================================================================================================================



        public class raycast_data
        {
            public IMySlimBlock hit_block = null;
            public Vector3D hit_location = Vector3D.Zero;
            public raycast_data(IMySlimBlock hit_block, Vector3D hit_location)
            {
                this.hit_block = hit_block;
                this.hit_location = hit_location;
            }
        }

        // Returns a custom data type IMySlimBlock block and Vector3D raycast block relative hit location.
        /// <summary>
        /// Shoots a raycast 10m forward and returns a custom data type 'raycast_data' containing IMySlimBlock block and Vector3D raycast block relative hit location.
        /// </summary>
        /// <param name="raycast_origin_matrix"></param>
        /// <returns></returns>
        public raycast_data RayCastGetHitBlock(MatrixD raycast_origin_matrix)
        {
            Vector3D hitLocation = Vector3D.Zero;
            IMySlimBlock return_block = null;
            IHitInfo ray_hit = null;
            MyAPIGateway.Physics.CastRay(raycast_origin_matrix.Translation + raycast_origin_matrix.Forward * 0.1, raycast_origin_matrix.Translation + raycast_origin_matrix.Forward * 10, out ray_hit);
            if (ray_hit != null && ray_hit.HitEntity is IMyCubeGrid)
            {
                var grid = ray_hit.HitEntity as IMyCubeGrid;
                if (grid != null && !grid.MarkedForClose && grid.Physics != null)
                {
                    hitLocation = ray_hit.Position;
                    var pos = grid.WorldToGridInteger(ray_hit.Position + raycast_origin_matrix.Forward * 0.1);
                    return_block = grid.GetCubeBlock(pos);
                }
            }

            return new raycast_data(return_block, hitLocation);
        }

        // If we ever want to implement a more complex notification system, do it here.
        public void ShowNotification(string text)
        {
            MyAPIGateway.Utilities.ShowNotification(text, 1);
        }
    }
}
