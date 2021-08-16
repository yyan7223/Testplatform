using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SPINACH.Networking
{

    public interface IRoutablePacketContent
    {
        byte GetNType();
        byte GetRevision();
        int GetByteLength();
        byte[] GetByteStream();
    }

    public class ObjectRoutablePacket
    {
        public static readonly byte frameType = 0x8c;

        public int objAddress;
        public byte packetType;
        public byte packetRevision;
        public int size;

        public byte[] content;

        public ObjectRoutablePacket(int addr, byte type, byte rev, byte[] content)
        {
            objAddress = addr;
            packetType = type;
            packetRevision = rev;
            this.content = content;
            size = content.Length;
        }

        public ObjectRoutablePacket(int addr, IRoutablePacketContent content)
        {
            objAddress = addr;
            packetType = content.GetNType();
            packetRevision = content.GetRevision();
            size = content.GetByteLength();
            this.content = new byte[size];
            Buffer.BlockCopy(content.GetByteStream(), 0, this.content, 0, size);
        }

        public ObjectRoutablePacket(PacketFrame frame)
        {
            if (frame.type != frameType) throw new ArgumentException("Bad frame type");

            var d = frame.content;
            objAddress = BitConverter.ToInt32(d, 0);
            packetType = d[4];
            packetRevision = d[5];
            size = BitConverter.ToInt32(d, 6);
            content = new byte[size];

            Buffer.BlockCopy(d, 10, content, 0, size);
        }

        public byte[] EncodeStream()
        {
            var buf = new byte[content.Length + 4 + 2 + 4];

            Buffer.BlockCopy(BitConverter.GetBytes(objAddress), 0, buf, 0, 4);
            buf[4] = packetType;
            buf[5] = packetRevision;
            Buffer.BlockCopy(BitConverter.GetBytes(size), 0, buf, 6, 4);

            Buffer.BlockCopy(content, 0, buf, 10, content.Length);

            return buf;
        }

        public PacketFrame EncodeFrame()
        {
            return new PacketFrame(EncodeStream(), frameType);
        }
    }



    public class NetworkObjectRouter
    {
        public bool master { get; private set; }

        public static readonly int routerAddress = -1;

        private Action<PacketFrame> _sendingHandler;

        private Dictionary<int, NetworkObjectMessenger> _registerdMessagers
            = new Dictionary<int, NetworkObjectMessenger>();

        private static readonly int staticAddrMax = 1024;
        private int _localAddrAlloc = staticAddrMax + 1;
        private NetworkDispatch _dispatch;

        public class ObjectInstantiatePacket : IRoutablePacketContent
        {
            public const byte NTYPE = 0;

            public int prefabID;
            public int localPeerID;
            public int address;
            public Vector3 pos;
            public Quaternion rot;

            public ObjectInstantiatePacket(int pid, int lpid, int addr, Vector3 pos, Quaternion rot)
            {
                prefabID = pid;
                localPeerID = lpid;
                address = addr;
                this.pos = pos;
                this.rot = rot;
            }

            public ObjectInstantiatePacket(byte[] buf)
            {
                localPeerID = BitConverter.ToInt32(buf, 0);
                address = BitConverter.ToInt32(buf, 4);
                prefabID = BitConverter.ToInt32(buf, 8);
                pos = Utils.DecodeVector3(buf, 12);
                rot = Utils.DecodeQuaternion(buf, 24);
            }

            public byte GetNType()
            {
                return NTYPE;
            }

            public byte GetRevision()
            {
                return 0;
            }

            public int GetByteLength()
            {
                return 48;
            }

            public byte[] GetByteStream()
            {
                var buf = new byte[GetByteLength()];

                Buffer.BlockCopy(BitConverter.GetBytes(localPeerID), 0, buf, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(address), 0, buf, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(prefabID), 0, buf, 8, 4);

                Utils.EncodeVector3(pos, buf, 12);
                Utils.EncodeQuaternion(rot, buf, 24);

                return buf;
            }
        }

        public NetworkObjectRouter(NetworkDispatch dispatch, bool isMaster)
        {
            // if(!dispatch.clientInited) throw new Exception("no no, not gonna work.");
            master = isMaster;
            _dispatch = dispatch;
        }

        public void RegisterMessengerStaticAddr(NetworkObjectMessenger messenger)
        {
            if (messenger.address > staticAddrMax || messenger.address < 0) throw new Exception("Address out of range");
            if (_registerdMessagers.ContainsKey(messenger.address))
                throw new Exception(string.Format("Address {0} already registered.",
                    messenger.address));

            _registerdMessagers.Add(messenger.address, messenger);
        }


        public void UnregisterMessenger(NetworkObjectMessenger messenger)
        {
            if (!_registerdMessagers.ContainsKey(messenger.address)) return;

            _registerdMessagers.Remove(messenger.address);
        }

        public void RouteMessage(ObjectRoutablePacket packet)
        {
            if (packet.objAddress == routerAddress)
            {
                switch (packet.packetType)
                {
                    case ObjectInstantiatePacket.NTYPE:
                        OnReceivedInstantiate(new ObjectInstantiatePacket(packet.content));
                        break;
                    default:
                        Debug.Log("bad router message.");
                        break;
                }
                return;
            }

            NetworkObjectMessenger rec = null;
            if (_registerdMessagers.TryGetValue(packet.objAddress, out rec))
            {
                rec.RouteMessage(packet);
            }
        }

        public void SendMessage(ObjectRoutablePacket packet)
        {
            _dispatch.Boardcast2ConnectedPeers(packet.EncodeFrame());
        }

        private int AddrAlloc()
        {
            return (_dispatch.localPeerID << 24) | _localAddrAlloc++;
        }

        public GameObject NetworkInstantiate(int prefabID, Vector3 position, Quaternion rotation)
        {
            if (!master && !_dispatch.clientInited) throw new Exception("no no, not gonna work.");

            var addr = AddrAlloc();
            var obj = DoInstantiateObj(prefabID, addr, position, rotation);

            SendMessage(new ObjectRoutablePacket(routerAddress,
                new ObjectInstantiatePacket(prefabID,
                    _dispatch.localPeerID, addr, position, rotation)));


            return obj;
        }

        private void OnReceivedInstantiate(ObjectInstantiatePacket packet)
        {
            //do nothing if master forward back our request.
            if (packet.localPeerID == _dispatch.localPeerID) return;
            DoInstantiateObj(packet.prefabID, packet.address, packet.pos, packet.rot);

            //if we're master forward this message to everyone.
            if (master) SendMessage(new ObjectRoutablePacket(routerAddress, packet));
        }

        private GameObject DoInstantiateObj(int prefabID, int address, Vector3 pos, Quaternion rot)
        {

            var obj = GameObject.Instantiate(_dispatch.cloneablePrefabs[prefabID].gameObject, pos, rot) as GameObject;

            var msger = obj.GetComponent<NetworkObjectMessenger>();
            msger.address = address;
            msger._router = this;

            if (_registerdMessagers.ContainsKey(msger.address))
                throw new Exception(string.Format("Address {0} already registered. WHAT THE FUCK>>>?????????",
                    msger.address));

            _registerdMessagers.Add(msger.address, msger);

            return obj;
        }

    }

}