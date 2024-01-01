using ProtoBuf;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRageMath;
using FaolonTether;

namespace FaolonTether.PowerCables.Sync
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

            // Log when a packet is being sent
            Log.Info($"Sending PacketBlockSettings: EntityId={EntityId}, Settings={Settings}");

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                Networking.RelayToClients(this);
                Log.Info("[PacketBlockSettings] Packet relayed to clients.");
            }
            else
            {
                Networking.SendToServer(this);
                Log.Info("[PacketBlockSettings] Packet sent to server.");
            }
        }

        public override void Received(ref bool relay)
        {
            // Log when a packet is received
            Log.Info($"Received PacketBlockSettings: EntityId={EntityId}");

            var block = MyAPIGateway.Entities.GetEntityById(this.EntityId) as IMyTerminalBlock;

            if (block == null)
            {
                Log.Error("Received PacketBlockSettings but block not found");
                return;
            }

            var logic = block.GameLogic?.GetAs<PowerCable>();

            if (logic == null)
            {
                Log.Error("Received PacketBlockSettings but PowerCable logic not found for block");
                return;
            }

            //logic.Settings.cable_draw = this.Settings.cable_draw;
            logic.Settings.ConnectedBlockId = this.Settings.ConnectedBlockId;
            logic.Settings.ConnectedBlockAttachLocation = this.Settings.ConnectedBlockAttachLocation;

            logic.Settings.ConnectionId = this.Settings.ConnectionId;
            Log.Info($"[PacketBlockSettings] Packet processed. EntityId={EntityId}, ConnectedBlockId={logic.Settings.ConnectedBlockId}");
            relay = true;
        }
    }
}