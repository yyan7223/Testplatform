using System;
using System.Collections;
using System.Collections.Generic;
using SPINACH.Networking;
using UnityEngine;

namespace SPINACH.Media
{

    public class NetworkRenderServer : MonoBehaviour
    {
        public NetworkDispatch dispatch;

        public Transform spawningPoint;

        private void Start()
        {
            dispatch._newPeerConnected += channel =>
            {
                Debug.Log("A client has joined our session.");
            };
            dispatch.EnableObjectRouter(true);
            dispatch.StartAcceptingPeers(new TcpTransportListener(23563));
            
            Debug.Log("server started at 23563");
        }
    }
}