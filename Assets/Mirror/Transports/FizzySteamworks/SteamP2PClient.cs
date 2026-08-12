using UnityEngine;
using Steamworks;
using System;

namespace Mirror.FizzySteam
{
    /// <summary>
    /// Steam P2P Client implementation for FizzySteamworks transport.
    /// Handles connecting to a Steam P2P host and sending/receiving data.
    /// </summary>
    internal class SteamP2PClient
    {
        private readonly FizzySteamworks transport;
        private CSteamID hostSteamId;

        private Callback<P2PSessionRequest_t> p2pSessionRequestCallback;
        private Callback<P2PSessionConnectFail_t> p2pConnectFailCallback;

        public bool Connected { get; private set; }

        public SteamP2PClient(FizzySteamworks transport)
        {
            this.transport = transport;
        }

        public void Connect(CSteamID hostId)
        {
            hostSteamId = hostId;

            // Register callbacks
            p2pSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
            p2pConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(OnP2PConnectFail);

            Debug.Log($"[FizzySteamworks Client] Connecting to host: {hostSteamId}");

            // Send an initial "handshake" packet to establish the P2P session
            byte[] handshake = new byte[] { 0xFF };
            if (SteamNetworking.SendP2PPacket(hostSteamId, handshake, 1, EP2PSend.k_EP2PSendReliable, transport.reliableChannel))
            {
                Connected = true;
                transport.OnClientConnectedInternal();
                Debug.Log($"[FizzySteamworks Client] Connected to host: {hostSteamId}");
            }
            else
            {
                Debug.LogError($"[FizzySteamworks Client] Failed to send handshake to {hostSteamId}");
                transport.OnClientErrorInternal(TransportError.Refused, "Failed to establish P2P connection.");
                transport.OnClientDisconnectedInternal();
            }
        }

        public void Disconnect()
        {
            if (!Connected) return;

            Connected = false;
            SteamNetworking.CloseP2PSessionWithUser(hostSteamId);

            p2pSessionRequestCallback?.Dispose();
            p2pConnectFailCallback?.Dispose();
            p2pSessionRequestCallback = null;
            p2pConnectFailCallback = null;

            transport.OnClientDisconnectedInternal();
            Debug.Log("[FizzySteamworks Client] Disconnected.");
        }

        public void ReceiveData()
        {
            if (!Connected) return;

            // Process incoming packets on reliable channel
            while (SteamNetworking.IsP2PPacketAvailable(out uint msgSize, transport.reliableChannel))
            {
                byte[] buffer = new byte[msgSize];
                if (SteamNetworking.ReadP2PPacket(buffer, msgSize, out uint bytesRead, out CSteamID remoteSteamId, transport.reliableChannel))
                {
                    if (remoteSteamId == hostSteamId)
                    {
                        ArraySegment<byte> segment = new ArraySegment<byte>(buffer, 0, (int)bytesRead);
                        transport.OnClientDataReceivedInternal(segment, Channels.Reliable);
                    }
                }
            }

            // Process incoming packets on unreliable channel
            while (SteamNetworking.IsP2PPacketAvailable(out uint msgSize2, transport.unreliableChannel))
            {
                byte[] buffer = new byte[msgSize2];
                if (SteamNetworking.ReadP2PPacket(buffer, msgSize2, out uint bytesRead, out CSteamID remoteSteamId, transport.unreliableChannel))
                {
                    if (remoteSteamId == hostSteamId)
                    {
                        ArraySegment<byte> segment = new ArraySegment<byte>(buffer, 0, (int)bytesRead);
                        transport.OnClientDataReceivedInternal(segment, Channels.Unreliable);
                    }
                }
            }
        }

        public void Send(ArraySegment<byte> data, EP2PSend sendType)
        {
            if (!Connected) return;

            int channel = sendType == EP2PSend.k_EP2PSendReliable
                ? transport.reliableChannel
                : transport.unreliableChannel;

            byte[] buffer = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, buffer, 0, data.Count);

            if (!SteamNetworking.SendP2PPacket(hostSteamId, buffer, (uint)buffer.Length, sendType, channel))
            {
                Debug.LogWarning("[FizzySteamworks Client] Failed to send packet to host.");
            }
        }

        private void OnP2PSessionRequest(P2PSessionRequest_t request)
        {
            // Accept session requests from the host
            if (request.m_steamIDRemote == hostSteamId)
            {
                SteamNetworking.AcceptP2PSessionWithUser(hostSteamId);
            }
        }

        private void OnP2PConnectFail(P2PSessionConnectFail_t failure)
        {
            if (failure.m_steamIDRemote == hostSteamId)
            {
                Debug.LogError($"[FizzySteamworks Client] P2P connection to host failed: {(EP2PSessionError)failure.m_eP2PSessionError}");
                transport.OnClientErrorInternal(TransportError.ConnectionClosed,
                    $"P2P connection failed: {(EP2PSessionError)failure.m_eP2PSessionError}");
                Disconnect();
            }
        }
    }
}
