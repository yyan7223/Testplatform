using System;
using System.Collections;
using System.Collections.Generic;
using SPINACH.Media;
using SPINACH.Networking;
using UnityEngine;
using UnityEngine.UI;

public class NetworkRenderClient : MonoBehaviour
{
    public NetworkDispatch dispatch;
    public InputField addressInput;

    public Text stats;
    
    public RawImage uidisplay;
    public Transform spawnpoint;

    public GameObject welcomeMenu;
    public GameObject renderMenu;
    
    public PeerPacketChannel peer;

    private NetworkRenderDemoVideoStreamer streamer;
    
    public void Connect2Server()
    {
        dispatch._onServerConnectionEstablished += () => { OnServerConnected(); };
        dispatch.EnableObjectRouter(false);
        peer = dispatch.EstablishConnection(addressInput.text, 23563, NetworkTransportProtocol.TCP);
    }

    private void Update()
    {
        if(streamer == null) return;
        stats.text = string.Format("Encoded Stream Transfered: {0}MB, {1}KB last sec", ((double)streamer.bytesPiped / 1024d / 1024d), streamer.lastsecPiped / 1024);
       
    }

    public void OnServerConnected()
    {
        var obj = dispatch.NetworkInstantiate(0, spawnpoint.position , Quaternion.identity);
        
        obj.GetComponent<LocalCubePlayer>().BeAsAGoodLocalPlayer();
        obj.GetComponent<NetworkRenderDemoVideoStreamer>().InitClient(uidisplay);
        streamer = obj.GetComponent<NetworkRenderDemoVideoStreamer>();
        
        welcomeMenu.SetActive(false);
        renderMenu.SetActive(true);
    }
}
