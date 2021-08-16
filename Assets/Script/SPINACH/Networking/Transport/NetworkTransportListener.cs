using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace SPINACH.Networking
{
    public abstract class NetworkTransportListener : IDisposable
    {
        public int listeningPort { get; protected set; }

        protected Action<PeerChannel> _newPeerCallback;
        
        public NetworkTransportListener(int port)
        {
            listeningPort = port;
        }

        public virtual void RegisterCallback(Action<PeerChannel> cb)
        {
            _newPeerCallback = cb;
        }
        
        public abstract void StartListening();
        public abstract void Stop();
        
        public abstract void Dispose();
    }
}
