using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace SPINACH.Networking
{
    public enum NetworkTransportProtocol
    {
        TCP
    }

    /// <summary>
    /// Multi-layer dispatch between network peers and local threads, GameObjects.
    /// </summary>
    public class NetworkDispatch : MonoBehaviour
    {
        public GameObject[] cloneablePrefabs;
        public bool isServer = false;
        public int localPeerID = 0;
        public bool clientInited = false;

        private NetworkTransportListener _listener;

        private Dictionary<int, PeerPacketChannel> _peers = new Dictionary<int, PeerPacketChannel>();
        private int _peerCounter = 1;

        private Queue<Action> _mainthreadMsgs = new Queue<Action>();

        private NetworkObjectRouter _objRouter;

        public Action<PeerPacketChannel> _newPeerConnected;
        public Action _onServerConnectionEstablished;
        public Action<NetworkObjectRouter> onNetworkRouter;

        public Dictionary<byte, Action<PacketFrame>> frameProcessorHandle = new Dictionary<byte, Action<PacketFrame>>();

        private static NetworkDispatch self;

        public static NetworkDispatch Default()
        {
            return self;
        }

        private void Awake()
        {
            self = this;

            frameProcessorHandle.Add(ServerPeerHandshakeFrame.frameType, frame =>
            {
                Debug.Assert(isServer == false);
                Debug.Assert(clientInited == false);

                localPeerID = new ServerPeerHandshakeFrame(frame).peerID;
                clientInited = true;
                _onServerConnectionEstablished?.Invoke();
            });
        }


        public void StartAcceptingPeers(NetworkTransportListener listener)
        {
            if (_listener != null)
            {
                _listener.Stop();
                _listener.Dispose();
            }

            if (listener == null) return;

            _listener = listener;
            _listener.RegisterCallback((p) =>
            {
                var ppc = new PeerPacketChannel(p);

                var pid = _peerCounter++;
                ppc.SendPacket(new ServerPeerHandshakeFrame(pid).EncodeFrame());

                lock (_peers)
                    _peers.Add(pid, ppc);

                lock (_mainthreadMsgs)
                {
                    _mainthreadMsgs.Enqueue(() =>
                    {
                        _newPeerConnected?.Invoke(ppc);
                    });
                }
            });
            isServer = true;
            _listener.StartListening();

        }

        public void EnableObjectRouter(bool master)
        {
            if (_objRouter != null) throw new Exception("Router already exists.");
            _objRouter = new NetworkObjectRouter(this, master);
            frameProcessorHandle.Add(ObjectRoutablePacket.frameType, frame =>
            {
                _objRouter.RouteMessage(new ObjectRoutablePacket(frame));
            });
            onNetworkRouter?.Invoke(_objRouter);
        }

        public PeerPacketChannel EstablishConnection(string address, int port, NetworkTransportProtocol protocol)
        {
            PeerChannel channel = null;
            switch (protocol)
            {
                case NetworkTransportProtocol.TCP:
                    channel = new TCPPeerChannel(address, port);
                    break;
            }

            if (channel == null) return null;

            var ppc = new PeerPacketChannel(channel);
            _peers.Add(_peerCounter++, ppc);
            return ppc;
        }

        public void Boardcast2ConnectedPeers(PacketFrame frame)
        {
            foreach (var pears in _peers)
            {
                pears.Value.SendPacket(frame);
            }
        }

        private void Update()
        {
            lock (_mainthreadMsgs)
            {
                while (_mainthreadMsgs.Count > 0)
                {
                    _mainthreadMsgs.Dequeue().Invoke();
                }
            }

            foreach (var peer in _peers)
            {
                PacketFrame frame = peer.Value.DequeueFrame();
                while (frame != null)
                {
                    Action<PacketFrame> hdl = null;
                    if (frameProcessorHandle.TryGetValue(frame.type, out hdl))
                    {
                        hdl.Invoke(frame);
                    }
                    else
                    {
                        Debug.Log(string.Format("Unhandled frame received with type: {0}.", frame.type));
                    }
                    frame = peer.Value.DequeueFrame();
                }
            }
        }

        public GameObject NetworkInstantiate(int prefabID, Vector3 pos, Quaternion rot)
        {
            if (_objRouter == null) return null;
            return _objRouter.NetworkInstantiate(prefabID, pos, rot);
        }

        private void OnDestroy()
        {
            //call everyone to stop their threads
            StartAcceptingPeers(null);
        }
    }
}

