using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SPINACH.Networking
{

    public abstract class PeerChannel
    {
        public bool connected { get; protected set; }
        public bool wasted { get; protected set; }
        protected Action<byte[], int> _receiveCallback;

        public abstract void Send(byte[] data);
        public abstract void RegisterReceiveCallback(Action<byte[], int> cb);

        //WASTED
        public abstract void Waste();
    }


}