using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SPINACH.Networking;
public class NetworkDemoMenu : MonoBehaviour
{
    public NetworkDispatch dispatch;

    public Transform spawningPoint;
    public Transform spawningPoint2;
    public PeerPacketChannel peer;

    public bool one = false;

    public void StartServer()
    {
        Debug.Log("enter into StartServer() function");
        one = true;
        dispatch._newPeerConnected += channel =>
        {
            peer = channel;
            MakeLocalPlayer();
        };
        dispatch.EnableObjectRouter(true);
        dispatch.StartAcceptingPeers(new TcpTransportListener(23563));

    }

    public void StartClient()
    {
        Debug.Log("enter into StartClient() function");
        dispatch._onServerConnectionEstablished += () => { MakeLocalPlayer(); };
        dispatch.EnableObjectRouter(false);
        peer = dispatch.EstablishConnection("127.0.0.1", 23563, NetworkTransportProtocol.TCP);
    }

    void MakeLocalPlayer()
    {
        var obj = dispatch.NetworkInstantiate(0, one ? spawningPoint.position : spawningPoint2.position, Quaternion.identity);
        obj.GetComponent<LocalCubePlayer>().BeAsAGoodLocalPlayer();
    }
}
