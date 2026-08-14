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

        /// <summary>Whether we are waiting for the server's acknowledgment packet.</summary>
        private bool waitingForAck = false;

        /// <summary>Time when connection attempt started, for timeout detection.</summary>
        private float connectStartTime;

        /// <summary>Time when last handshake packet was sent, for retry logic.</summary>
        private float lastHandshakeSendTime;

        public SteamP2PClient(FizzySteamworks transport)
        {
            this.transport = transport;
        }

        public void Connect(CSteamID hostId)
        {
            hostSteamId = hostId;

            // Register callbacks FIRST so we can handle session requests from the host
            p2pSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
            p2pConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(OnP2PConnectFail);

            // Proactively accept P2P session with the host.
            // This is critical: when the server sends us the ACK packet,
            // Steam needs to have an accepted session on our end, otherwise
            // the packet gets dropped and causes P2P timeout.
            SteamNetworking.AcceptP2PSessionWithUser(hostSteamId);

            Debug.Log($"[FizzySteamworks Client] Connecting to host: {hostSteamId}");

            // Start the handshake process - we'll repeatedly send handshake packets
            // until the server acknowledges, because the first few packets may be lost
            // while Steam is establishing the P2P NAT traversal.
            waitingForAck = true;
            connectStartTime = Time.realtimeSinceStartup;
            lastHandshakeSendTime = 0f; // Force immediate first send
            Debug.Log($"[FizzySteamworks Client] Starting connection to host: {hostSteamId}, will send handshake packets...");
        }

        public void Disconnect()
        {
            if (!Connected && !waitingForAck) return;

            Connected = false;
            waitingForAck = false;
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
            if (!Connected && !waitingForAck) return;

            // Handle handshake retry and timeout
            if (waitingForAck)
            {
                float now = Time.realtimeSinceStartup;

                if (now - connectStartTime > transport.connectionTimeout)
                {
                    Debug.LogError("[FizzySteamworks Client] Connection timed out waiting for server acknowledgment.");
                    waitingForAck = false;
                    transport.OnClientErrorInternal(TransportError.Timeout, "Connection timed out.");
                    transport.OnClientDisconnectedInternal();
                    return;
                }

                // Resend handshake every 0.5 seconds until we get an ack
                if (now - lastHandshakeSendTime >= 0.5f)
                {
                    lastHandshakeSendTime = now;
                    byte[] handshake = new byte[] { 0xFF };
                    SteamNetworking.SendP2PPacket(hostSteamId, handshake, 1, EP2PSend.k_EP2PSendReliable, transport.reliableChannel);
                    Debug.Log($"[FizzySteamworks Client] Handshake sent to host: {hostSteamId}");
                }
            }

            // Process incoming packets on reliable channel
            while (SteamNetworking.IsP2PPacketAvailable(out uint msgSize, transport.reliableChannel))
            {
                byte[] buffer = new byte[msgSize];
                if (SteamNetworking.ReadP2PPacket(buffer, msgSize, out uint bytesRead, out CSteamID remoteSteamId, transport.reliableChannel))
                {
                    if (remoteSteamId == hostSteamId)
                    {
                        // Check if this is the server's acknowledgment packet
                        if (waitingForAck && bytesRead == 1 && buffer[0] == 0xFE)
                        {
                            waitingForAck = false;
                            Connected = true;
                            Debug.Log($"[FizzySteamworks Client] Connected to host: {hostSteamId} (acknowledgment received)");
                            transport.OnClientConnectedInternal();
                            // IMPORTANT: Stop processing packets this frame.
                            // Mirror's NetworkClient needs a full frame to initialize
                            // (OnClientConnect, AddPlayer, etc.) before it can receive data.
                            // Processing data packets in the same frame causes "failed to add batch".
                            return;
                        }

                        // Only process data if we are fully connected
                        if (Connected)
                        {
                            // Ignore duplicate ACK packets that may arrive from server's
                            // handshake retry logic. These are 1-byte 0xFE packets that would
                            // cause "failed to add batch" if passed to Mirror (< 8 bytes).
                            if (bytesRead == 1 && buffer[0] == 0xFE)
                            {
                                // Duplicate ACK - ignore silently
                                continue;
                            }

                            ArraySegment<byte> segment = new ArraySegment<byte>(buffer, 0, (int)bytesRead);
                            transport.OnClientDataReceivedInternal(segment, Channels.Reliable);
                        }
                    }
                }
            }

            // Only process unreliable data if fully connected
            if (!Connected) return;

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
