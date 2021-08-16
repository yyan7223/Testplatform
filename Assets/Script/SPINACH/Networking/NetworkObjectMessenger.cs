using System;
using System.Collections;
using System.Collections.Generic;
using SPINACH.Networking;
using UnityEngine;

namespace SPINACH.Networking
{
    public class NetworkObjectMessenger : MonoBehaviour
    {
        public int address = -2;
        public bool pregenObject;

        public NetworkObjectRouter _router;
        private Dictionary<byte, Action<byte, byte[]>> _methodList = new Dictionary<byte, Action<byte, byte[]>>();

        private void Start()
        {
            if (pregenObject) NetworkDispatch.Default().onNetworkRouter += RegisterSelf;
        }

        private void OnDestroy()
        {
            if (pregenObject) NetworkDispatch.Default().onNetworkRouter -= RegisterSelf;
        }

        void RegisterSelf(NetworkObjectRouter router)
        {
            _router = router;
            _router.RegisterMessengerStaticAddr(this);
        }

        public void RegisterMethod(byte type, Action<byte, byte[]> method)
        {
            _methodList.Add(type, method);
        }

        public void RouteMessage(ObjectRoutablePacket packet)
        {
            Action<byte, byte[]> me = null;
            if (_methodList.TryGetValue(packet.packetType, out me))
            {
                me.Invoke(packet.packetRevision, packet.content);
            }
        }

        public void SendMessage(IRoutablePacketContent packet)
        {
            if(_router == null || address < 0) return;
            _router.SendMessage(new ObjectRoutablePacket(address, packet));
        }
    }
}