using UnityEngine;
using Steamworks;
using System;
using System.Collections.Generic;

namespace Mirror.FizzySteam
{
    /// <summary>
    /// Steam P2P Server implementation for FizzySteamworks transport.
    /// Handles incoming connections and data from Steam P2P clients.
    /// </summary>
    internal class SteamP2PServer
    {
        private readonly FizzySteamworks transport;
        private readonly Dictionary<int, CSteamID> connectedClients = new Dictionary<int, CSteamID>();
        private readonly Dictionary<CSteamID, int> steamIdToConnId = new Dictionary<CSteamID, int>();
        private int nextConnectionId = 1;

        private Callback<P2PSessionRequest_t> p2pSessionRequestCallback;
        private Callback<P2PSessionConnectFail_t> p2pConnectFailCallback;

        // Queue for delayed connection notifications.
        // We delay notifying Mirror by one frame to let the P2P channel stabilize.
        private readonly Queue<int> pendingConnections = new Queue<int>();
        private readonly HashSet<int> pendingConnectionSet = new HashSet<int>();

        public bool Active { get; private set; }

        public SteamP2PServer(FizzySteamworks transport)
        {
            this.transport = transport;
        }

        public void Start()
        {
            Active = true;

            // Register Steam P2P callbacks
            p2pSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
            p2pConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(OnP2PConnectFail);

            Debug.Log("[FizzySteamworks Server] Started listening for P2P connections.");
        }

        public void Shutdown()
        {
            if (!Active) return;

            Active = false;

            // Close all P2P sessions
            foreach (var kvp in connectedClients)
            {
                SteamNetworking.CloseP2PSessionWithUser(kvp.Value);
            }

            connectedClients.Clear();
            steamIdToConnId.Clear();
            pendingConnections.Clear();
            pendingConnectionSet.Clear();

            p2pSessionRequestCallback?.Dispose();
            p2pConnectFailCallback?.Dispose();
            p2pSessionRequestCallback = null;
            p2pConnectFailCallback = null;

            Debug.Log("[FizzySteamworks Server] Shut down.");
        }

        public void ReceiveData()
        {
            if (!Active) return;

            // Process pending connection notifications (delayed by one frame).
            // This gives the P2P channel time to stabilize before Mirror starts
            // sending spawn data to the new client.
            while (pendingConnections.Count > 0)
            {
                int connId = pendingConnections.Dequeue();
                pendingConnectionSet.Remove(connId);

                // Only notify if the client is still connected (hasn't timed out)
                if (connectedClients.ContainsKey(connId))
                {
                    transport.OnServerConnectedInternal(connId);
                }
            }

            // Process incoming packets on reliable channel
            while (SteamNetworking.IsP2PPacketAvailable(out uint msgSize, transport.reliableChannel))
            {
                byte[] buffer = new byte[msgSize];
                if (SteamNetworking.ReadP2PPacket(buffer, msgSize, out uint bytesRead, out CSteamID remoteSteamId, transport.reliableChannel))
                {
                    HandleData(remoteSteamId, buffer, bytesRead, Channels.Reliable);
                }
            }

            // Process incoming packets on unreliable channel
            while (SteamNetworking.IsP2PPacketAvailable(out uint msgSize2, transport.unreliableChannel))
            {
                byte[] buffer = new byte[msgSize2];
                if (SteamNetworking.ReadP2PPacket(buffer, msgSize2, out uint bytesRead, out CSteamID remoteSteamId, transport.unreliableChannel))
                {
                    HandleData(remoteSteamId, buffer, bytesRead, Channels.Unreliable);
                }
            }
        }

        private void HandleData(CSteamID remoteSteamId, byte[] data, uint bytesRead, int channelId)
        {
            // Check if this is a known client
            if (!steamIdToConnId.TryGetValue(remoteSteamId, out int connId))
            {
                // New client - first packet should be the handshake (0xFF)
                if (bytesRead == 1 && data[0] == 0xFF)
                {
                    // Register the connection
                    connId = nextConnectionId++;
                    connectedClients[connId] = remoteSteamId;
                    steamIdToConnId[remoteSteamId] = connId;

                    Debug.Log($"[FizzySteamworks Server] Client connected: {remoteSteamId} (connId: {connId})");

                    // Send acknowledgment packet (0xFE) back to client
                    byte[] ack = new byte[] { 0xFE };
                    SteamNetworking.SendP2PPacket(remoteSteamId, ack, 1, EP2PSend.k_EP2PSendReliable, transport.reliableChannel);

                    // Queue the connection notification for next frame.
                    // This ensures the client has time to receive the ACK and
                    // transition to Connected state before Mirror sends spawn data.
                    pendingConnections.Enqueue(connId);
                    pendingConnectionSet.Add(connId);
                    return;
                }
                else
                {
                    // Unknown client sent non-handshake data - ignore
                    Debug.LogWarning($"[FizzySteamworks Server] Received data from unknown client {remoteSteamId} without handshake. Ignoring.");
                    return;
                }
            }

            // Known client - check if this is a duplicate handshake (client retrying)
            if (bytesRead == 1 && data[0] == 0xFF)
            {
                // Client is retrying handshake - resend acknowledgment
                byte[] ack = new byte[] { 0xFE };
                SteamNetworking.SendP2PPacket(remoteSteamId, ack, 1, EP2PSend.k_EP2PSendReliable, transport.reliableChannel);
                return;
            }

            // If this client is still pending connection notification, skip data delivery.
            // Mirror hasn't been notified yet, so it can't process data for this connection.
            if (pendingConnectionSet.Contains(connId))
            {
                return;
            }

            // Known client - deliver data to Mirror
            ArraySegment<byte> segment = new ArraySegment<byte>(data, 0, (int)bytesRead);
            transport.OnServerDataReceivedInternal(connId, segment, channelId);
        }

        public void Send(int connectionId, ArraySegment<byte> data, EP2PSend sendType)
        {
            if (!connectedClients.TryGetValue(connectionId, out CSteamID steamId))
            {
                Debug.LogWarning($"[FizzySteamworks Server] Cannot send to unknown connection: {connectionId}");
                return;
            }

            int channel = sendType == EP2PSend.k_EP2PSendReliable
                ? transport.reliableChannel
                : transport.unreliableChannel;

            byte[] buffer = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, buffer, 0, data.Count);

            if (!SteamNetworking.SendP2PPacket(steamId, buffer, (uint)buffer.Length, sendType, channel))
            {
                Debug.LogWarning($"[FizzySteamworks Server] Failed to send packet to {steamId}");
            }
        }

        public void Disconnect(int connectionId)
        {
            if (!connectedClients.TryGetValue(connectionId, out CSteamID steamId))
                return;

            SteamNetworking.CloseP2PSessionWithUser(steamId);
            connectedClients.Remove(connectionId);
            steamIdToConnId.Remove(steamId);

            transport.OnServerDisconnectedInternal(connectionId);
            Debug.Log($"[FizzySteamworks Server] Disconnected client: {connectionId}");
        }

        public string GetClientAddress(int connectionId)
        {
            if (connectedClients.TryGetValue(connectionId, out CSteamID steamId))
                return steamId.m_SteamID.ToString();
            return "unknown";
        }

        private void OnP2PSessionRequest(P2PSessionRequest_t request)
        {
            // Accept all incoming P2P session requests (server accepts everyone)
            CSteamID remoteSteamId = request.m_steamIDRemote;
            Debug.Log($"[FizzySteamworks Server] P2P session request from: {remoteSteamId}");
            SteamNetworking.AcceptP2PSessionWithUser(remoteSteamId);
        }

        private void OnP2PConnectFail(P2PSessionConnectFail_t failure)
        {
            CSteamID remoteSteamId = failure.m_steamIDRemote;
            Debug.LogWarning($"[FizzySteamworks Server] P2P connection failed with {remoteSteamId}: {(EP2PSessionError)failure.m_eP2PSessionError}");

            if (steamIdToConnId.TryGetValue(remoteSteamId, out int connId))
            {
                transport.OnServerErrorInternal(connId, TransportError.ConnectionClosed,
                    $"P2P connection failed: {(EP2PSessionError)failure.m_eP2PSessionError}");
                Disconnect(connId);
            }
        }
    }
}
