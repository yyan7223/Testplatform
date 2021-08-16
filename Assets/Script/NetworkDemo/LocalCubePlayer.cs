using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SPINACH.Networking;

public class LocalCubePlayer : MonoBehaviour
{
    class InputSyncPacket : IRoutablePacketContent
    {
        public const byte NTYPE = 0x1f;
        
        public Vector2 hv;

        public InputSyncPacket(Vector2 v)
        {
            this.hv= v;
        }

        public InputSyncPacket(byte[] bytes)
        {
            hv = Utils.DecodeVector2(bytes, 0);
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
            return 8;
        }

        public byte[] GetByteStream()
        {
            var buf = new byte[GetByteLength()];
            Utils.EncodeVector2(hv, buf,0);
            return buf;
        }
    }

    public Vector2 input;
    public float syncDelay;
    public float moveSpeed;
    public float rotSpeed;

    public Transform cubeicPoint;
    
    private bool ownedByLocalPlayer = false;
    private NetworkObjectMessenger _nom;
    
    private void Awake()
    {
        _nom = GetComponent<NetworkObjectMessenger>();
        _nom.RegisterMethod(InputSyncPacket.NTYPE, (rev, bytes) =>
        {
            if(rev != 0) return;
            var p = new InputSyncPacket(bytes);
            input = p.hv;
        });
    }

    private void Update()
    {
        if (ownedByLocalPlayer && Input.GetMouseButtonDown(0))
        {
            var n = NetworkDispatch.Default().NetworkInstantiate(1, cubeicPoint.position, cubeicPoint.rotation);
        }
        
        if (!NetworkDispatch.Default().isServer) return;
        transform.Translate(Vector3.forward * (moveSpeed * input.y * Time.deltaTime));
        transform.Rotate(Vector3.up * (rotSpeed * input.x * Time.deltaTime));

        
    }

    public void BeAsAGoodLocalPlayer()
    {
        ownedByLocalPlayer = true;
        StartCoroutine(SyncInput());
    }
    
    IEnumerator SyncInput()
    {
        while (enabled && ownedByLocalPlayer)
        {
            input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            
            if(!NetworkDispatch.Default().isServer) _nom.SendMessage(new InputSyncPacket(input));
            yield return new  WaitForSeconds(syncDelay);
        }
    }
    
}
