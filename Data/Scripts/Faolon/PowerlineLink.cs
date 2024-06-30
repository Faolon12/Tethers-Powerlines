using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace FaolonTether
{
    [ProtoContract]
    public class PowerlineLink
    {
        //[ProtoMember(1)]
        //public long PoleAId;

        [ProtoMember(2)]
        public string PoleAGridName;

        [ProtoMember(3)]
        public Vector3I PoleAPosition;

        //[ProtoMember(4)]
        //public long PoleBId;

        [ProtoMember(5)]
        public string PoleBGridName;

        [ProtoMember(6)]
        public Vector3I PoleBPosition;

        [XmlIgnore]
        public PowerlinePole PoleA;

        [XmlIgnore]
        public PowerlinePole PoleB;

        public static PowerlineLink Generate(PowerlinePole a, PowerlinePole b)
        {
            PowerlineLink link = new PowerlineLink();
            link.PoleA = a;
            link.PoleB = b;
            link.SavePrep();

            return link;
        }

        public void SavePrep()
        {
            if (PoleA != null && PoleB != null)
            {
                PoleAGridName = PoleA.Grid.DisplayName;
                PoleAPosition = PoleA.ModBlock.Position;

                PoleBGridName = PoleB.Grid.DisplayName;
                PoleBPosition = PoleB.ModBlock.Position;
            }
            else
            {
                MyLog.Default.Info("[Tether] Warning! Link has null PowerlinePole and cannot generate grid and position");
            }
        }

        public void LoadPrep()
        {
            HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
            if (PoleA == null)
            {
                IMyEntity gridA = null;
                MyAPIGateway.Entities.GetEntities(entities, e => (e.DisplayName == PoleAGridName ? (gridA = e) != e : false));
                if (gridA != null)
                {
                    IMySlimBlock block = ((MyCubeGrid)gridA).GetCubeBlock(PoleAPosition);

                    MyLog.Default.Info($"[Tether] gridA cubeblock lookup: {block != null}");

                    if (block != null && block.FatBlock != null)
                    {
                        PoleA = block.FatBlock.GameLogic.GetAs<PowerlinePole>();

                        MyLog.Default.Info($"[Tether] gridA GameLogic lookup: {PoleA != null}");
                    }
                }
            }

            if (PoleB == null)
            {
                IMyEntity gridB = null;
                MyAPIGateway.Entities.GetEntities(entities, e => (e.DisplayName == PoleBGridName ? (gridB = e) != e : false));
                if (gridB != null)
                {
                    IMySlimBlock block = ((MyCubeGrid)gridB).GetCubeBlock(PoleBPosition);

                    MyLog.Default.Info($"[Tether] gridB cubeblock lookup: {block != null}");

                    if (block != null && block.FatBlock != null)
                    {
                        PoleB = block.FatBlock.GameLogic.GetAs<PowerlinePole>();

                        MyLog.Default.Info($"[Tether] gridB GameLogic lookup: {PoleB != null}");

                    }
                }
            }
        }

    }
}
