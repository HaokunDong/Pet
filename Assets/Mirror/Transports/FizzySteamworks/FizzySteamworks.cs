using UnityEngine;
using Mirror;
using Steamworks;
using System;
using System.Collections.Generic;

namespace Mirror.FizzySteam
{
    /// <summary>
    /// FizzySteamworks Transport for Mirror.
    /// Uses Steam P2P networking (SteamNetworking API) for NAT-traversal-free connections.
    /// </summary>
    [HelpURL("https://github.com/Chykary/FizzySteamworks")]
    public class FizzySteamworks : Transport
    {
        private const string STEAM_SCHEME = "steam";

        [Header("Steam P2P Settings")]
        [Tooltip("The Steam P2P channel to use for reliable messages")]
        public int reliableChannel = 0;

        [Tooltip("The Steam P2P channel to use for unreliable messages")]
        public int unreliableChannel = 1;

        [Tooltip("Timeout in seconds for connection attempts")]
        public float connectionTimeout = 25f;

        [Tooltip("Maximum packet size (Steam P2P max is 1MB, but we limit for performance)")]
        public int maxPacketSize = 1200;

        private SteamP2PServer server;
        private SteamP2PClient client;

        private void OnEnable()
        {
            Debug.Log("[FizzySteamworks] Transport enabled.");
        }

        private void OnDisable()
        {
            Shutdown();
        }

        #region Transport Overrides

        public override bool Available()
        {
            try
            {
                // Check if Steam is initialized by attempting to get the user's Steam ID.
                // This avoids depending on SteamManager (which is in Assembly-CSharp).
                SteamUser.GetSteamID();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override int GetMaxPacketSize(int channelId = Channels.Reliable)
        {
            return maxPacketSize;
        }

        public override void Shutdown()
        {
            server?.Shutdown();
            client?.Disconnect();

            server = null;
            client = null;

            Debug.Log("[FizzySteamworks] Transport shut down.");
        }

        #region Client

        public override bool ClientConnected()
        {
            return client != null && client.Connected;
        }

        public override void ClientConnect(string address)
        {
            if (!Available())
            {
                Debug.LogError("[FizzySteamworks] Steam is not initialized. Cannot connect.");
                OnClientDisconnected?.Invoke();
                return;
            }

            if (!ulong.TryParse(address, out ulong steamId))
            {
                Debug.LogError($"[FizzySteamworks] Invalid Steam ID address: {address}");
                OnClientDisconnected?.Invoke();
                return;
            }

            client = new SteamP2PClient(this);
            client.Connect(new CSteamID(steamId));
        }

        public override void ClientSend(ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            if (client == null || !client.Connected)
            {
                Debug.LogWarning("[FizzySteamworks] Cannot send: client not connected.");
                return;
            }

            EP2PSend sendType = channelId == Channels.Reliable
                ? EP2PSend.k_EP2PSendReliable
                : EP2PSend.k_EP2PSendUnreliable;

            client.Send(segment, sendType);
        }

        public override void ClientDisconnect()
        {
            client?.Disconnect();
            client = null;
        }

        public override void ClientEarlyUpdate()
        {
            client?.ReceiveData();
        }

        #endregion

        #region Server

        public override Uri ServerUri()
        {
            var builder = new UriBuilder
            {
                Scheme = STEAM_SCHEME,
                Host = SteamUser.GetSteamID().m_SteamID.ToString()
            };
            return builder.Uri;
        }

        public override bool ServerActive()
        {
            return server != null && server.Active;
        }

        public override void ServerStart()
        {
            if (!Available())
            {
                Debug.LogError("[FizzySteamworks] Steam is not initialized. Cannot start server.");
                return;
            }

            server = new SteamP2PServer(this);
            server.Start();

            Debug.Log("[FizzySteamworks] Server started.");
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            if (server == null || !server.Active)
            {
                Debug.LogWarning("[FizzySteamworks] Cannot send: server not active.");
                return;
            }

            EP2PSend sendType = channelId == Channels.Reliable
                ? EP2PSend.k_EP2PSendReliable
                : EP2PSend.k_EP2PSendUnreliable;

            server.Send(connectionId, segment, sendType);
        }

        public override void ServerDisconnect(int connectionId)
        {
            server?.Disconnect(connectionId);
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            return server?.GetClientAddress(connectionId) ?? "unknown";
        }

        public override void ServerStop()
        {
            server?.Shutdown();
            server = null;
            Debug.Log("[FizzySteamworks] Server stopped.");
        }

        public override void ServerEarlyUpdate()
        {
            server?.ReceiveData();
        }

        #endregion

        #endregion

        #region Internal Callbacks (called by SteamP2PClient/Server)

        internal void OnClientConnectedInternal()
        {
            OnClientConnected?.Invoke();
        }

        internal void OnClientDisconnectedInternal()
        {
            OnClientDisconnected?.Invoke();
        }

        internal void OnClientDataReceivedInternal(ArraySegment<byte> data, int channelId)
        {
            OnClientDataReceived?.Invoke(data, channelId);
        }

        internal void OnClientErrorInternal(TransportError error, string reason)
        {
            OnClientError?.Invoke(error, reason);
        }

        internal void OnServerConnectedInternal(int connId)
        {
            OnServerConnected?.Invoke(connId);
        }

        internal void OnServerDisconnectedInternal(int connId)
        {
            OnServerDisconnected?.Invoke(connId);
        }

        internal void OnServerDataReceivedInternal(int connId, ArraySegment<byte> data, int channelId)
        {
            OnServerDataReceived?.Invoke(connId, data, channelId);
        }

        internal void OnServerErrorInternal(int connId, TransportError error, string reason)
        {
            OnServerError?.Invoke(connId, error, reason);
        }

        #endregion

        public override string ToString()
        {
            return "FizzySteamworks";
        }
    }
}
