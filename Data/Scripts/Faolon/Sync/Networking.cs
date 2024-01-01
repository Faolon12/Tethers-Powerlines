using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace FaolonTether.PowerCables.Sync
{
    public class Networking
    {
        public readonly ushort PacketId;

        /// <summary>
        /// <paramref name="packetId"/> must be unique from all other mods that also use packets.
        /// </summary>
        public Networking(ushort packetId)
        {
            PacketId = packetId;
            Log.Info($"[Networking] Initialized with PacketId={PacketId}"); // Log the initialization of Networking

        }

        /// <summary>
        /// Register packet monitoring, not necessary if you don't want the local machine to handle incomming packets.
        /// </summary>
        public void Register()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(PacketId, ReceivedPacket);
            Log.Info("[Networking] Packet handler registered."); // Log when packet handler is registered

        }

        /// <summary>
        /// This must be called on world unload if you called <see cref="Register"/>.
        /// </summary>
        public void Unregister()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(PacketId, ReceivedPacket);
            Log.Info("[Networking] Packet handler unregistered."); // Log when packet handler is unregistered

        }

        private void ReceivedPacket(ushort handlerId, byte[] rawData, ulong senderId, bool fromServer) // executed when a packet is received on this machine
        {
            Log.Info($"[Networking] ReceivedPacket called. HandlerId={handlerId}, SenderId={senderId}, FromServer={fromServer}, DataSize={rawData.Length}");

            try
            {
                var packet = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase>(rawData);

                // Log after successful deserialization
                // Log the receipt of raw packet data
                Log.Info($"[Networking] Packet deserialized: {packet.GetType()}, SenderId={packet.SenderId}");

                bool relay = false;
                packet.Received(ref relay);

                Log.Info($"[Networking] Packet 'Received' method processed. PacketType={packet.GetType()}, Relay={relay}");

                if (relay)
                {
                    RelayToClients(packet, rawData);
                    Log.Info($"[Networking] RelayToClients called. PacketType={packet.GetType()}"); // Log when the packet is relayed to clients
                }
            }
            catch (Exception e)
            {
                Log.Error($"[Networking] Exception in ReceivedPacket: {e.Message}");
            }
        }

        /// <summary>
        /// Send a packet to the server.
        /// Works from clients and server.
        /// </summary>
        /// <param name="packet"></param>
        public void SendToServer(PacketBase packet)
        {
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);

            MyAPIGateway.Multiplayer.SendMessageToServer(PacketId, bytes);

            Log.Info($"[Networking] Packet sent to server. PacketType={packet.GetType()}"); // Log when a packet is sent to the server

        }

        /// <summary>
        /// Send a packet to a specific player.
        /// Only works server side.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="steamId"></param>
        public void SendToPlayer(PacketBase packet, ulong steamId)
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);

            MyAPIGateway.Multiplayer.SendMessageTo(PacketId, bytes, steamId);
            Log.Info($"[Networking] Packet sent to player. PacketType={packet.GetType()}, SteamId={steamId}"); // Log when a packet is sent to a specific player

        }

        private List<IMyPlayer> tempPlayers;

        /// <summary>
        /// Sends packet (or supplied bytes) to all players except server player and supplied packet's sender.
        /// Only works server side.
        /// </summary>
        public void RelayToClients(PacketBase packet, byte[] rawData = null)
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            if (tempPlayers == null)
                tempPlayers = new List<IMyPlayer>(MyAPIGateway.Session.SessionSettings.MaxPlayers);
            else
                tempPlayers.Clear();

            MyAPIGateway.Players.GetPlayers(tempPlayers);
            Log.Info($"[Networking] Relaying packet to clients. PacketType={packet.GetType()}, ClientsCount={tempPlayers.Count}"); // Log before relaying to clients


            foreach (var p in tempPlayers)
            {
                if (p.SteamUserId == MyAPIGateway.Multiplayer.ServerId)
                    continue;

                if (p.SteamUserId == packet.SenderId)
                    continue;

                if (rawData == null)
                    rawData = MyAPIGateway.Utilities.SerializeToBinary(packet);

                MyAPIGateway.Multiplayer.SendMessageTo(PacketId, rawData, p.SteamUserId);
            }

            tempPlayers.Clear();
        }
    }
}
