using ProtoBuf;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRageMath;

namespace KlimeAndPsycho.PowerCables.Sync
{
    [ProtoContract(UseProtoMembersOnly = true)]
    public class PacketBlockSettings : PacketBase
    {
        [ProtoMember(1)]
        public long EntityId;

        [ProtoMember(2)]
        public PowerCableBlockSettings Settings;

        public PacketBlockSettings() { } // Empty constructor required for deserialization

        public void Send(long entityId, PowerCableBlockSettings settings)
        {
            EntityId = entityId;
            Settings = settings;

            if (MyAPIGateway.Multiplayer.IsServer)
                Networking.RelayToClients(this);
            else
                Networking.SendToServer(this);
        }

        public override void Received(ref bool relay)
        {
            var block = MyAPIGateway.Entities.GetEntityById(this.EntityId) as IMyTerminalBlock;

            if (block == null)
                return;

            var logic = block.GameLogic?.GetAs<PowerCable>();

            if (logic == null)
                return;

            //logic.Settings.cable_draw = this.Settings.cable_draw;
            logic.Settings.ConnectedBlockId = this.Settings.ConnectedBlockId;
            logic.Settings.ConnectedBlockAttachLocation = this.Settings.ConnectedBlockAttachLocation;

            logic.Settings.ConnectionId = this.Settings.ConnectionId;

            relay = true;
        }
    }
}