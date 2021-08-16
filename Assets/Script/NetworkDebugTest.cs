using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using SPINACH.Networking;
using UnityEngine;

public class NetworkDebugTest : MonoBehaviour
{


    public NetworkDispatch dispatch;

    public PeerPacketChannel peer;
    
    public void StartServer()
    {
        dispatch._newPeerConnected += channel =>
        {
            Debug.Log("new fucking peer!");
            peer = channel;

        };
        dispatch.StartAcceptingPeers(new TcpTransportListener(23563));
        dispatch.EnableObjectRouter(true);
    }

    public void StartClient()
    {
        peer = dispatch.EstablishConnection("127.0.0.1", 23563, NetworkTransportProtocol.TCP);
        dispatch.EnableObjectRouter(false);
    }

    public void Send()
    {
        peer.Send(Encoding.UTF8.GetBytes("hello you motherfucker hahahahah lol lol lol lmao lmao hahahha"));
    }

    private void Update()
    {
        // if (peer != null)
        // {
        //     var p = peer.DequeueFrame();
        //     // Debug.Log(p);
        //     if (p != null)
        //     {
        //         Debug.Log(string.Format("received msg packet {0}: {1}",p.type, Encoding.UTF8.GetString(p.content)));
        //     }
        // }
    }
}
