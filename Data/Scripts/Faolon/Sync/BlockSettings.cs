using ProtoBuf;
using System.Collections.Generic;
using VRageMath;

namespace FaolonTether.PowerCables
{
    [ProtoContract(UseProtoMembersOnly = true)]
    public class PowerCableBlockSettings
    {
        //[ProtoMember(1)]
        //public List<PowerCable.Connection> cable_draw;

        [ProtoMember(1)]
        public long ConnectedBlockId;

        [ProtoMember(2)]
        public Vector3D ConnectedBlockAttachLocation;

        [ProtoMember(3)]
        public long ConnectionId;

        // PLACEHOLDER for multi-connection tether blocks.
        [ProtoMember(4)]
        public long ConnectedBlockIdA;

        [ProtoMember(5)]
        public long ConnectedBlockIdB;

        [ProtoMember(6)]
        public long ConnectedBlockIdC;

        [ProtoMember(7)]
        public long ConnectedBlockIdD;
    }
}
