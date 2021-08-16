using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SPINACH.Networking;
using VPRAssets.Scripts;
using UnityEngine;

namespace SPINACH.Networking
{
    public class NetworkSyncTransform : MonoBehaviour
    {
        public const byte NTYPE = 2;

        public float sendDelay = 0.005f;

        public bool disableRigidbodyOnNonServer = false;

        class TransformInfoPacket : IRoutablePacketContent
        {
            public Vector3 position;
            public Quaternion rotation;

            public TransformInfoPacket(Vector3 position, Quaternion rotation)
            {
                this.position = position;
                this.rotation = rotation;
            }

            public TransformInfoPacket(byte[] bytes)
            {
                position = Utils.DecodeVector3(bytes, 0);
                rotation = Utils.DecodeQuaternion(bytes, 12);
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
                return 12 + 16;
            }

            public byte[] GetByteStream()
            {
                var buf = new byte[GetByteLength()];
                Utils.EncodeVector3(position, buf, 0);
                Utils.EncodeQuaternion(rotation, buf, 12);
                return buf;
            }
        }

        private NetworkObjectMessenger _nom;

        private void Start()
        {
            _nom = GetComponent<NetworkObjectMessenger>();
            _nom.RegisterMethod(NTYPE, UpdateTransform);

            if (disableRigidbodyOnNonServer && !NetworkDispatch.Default().isServer)
                Destroy(GetComponent<Rigidbody>());

            StartCoroutine(SendTransform());
        }

        IEnumerator SendTransform()
        {
            while (enabled)
            {
                if (NetworkDispatch.Default().isServer) _nom.SendMessage(new TransformInfoPacket(transform.position, transform.rotation));
                yield return new WaitForSeconds(sendDelay);
            }
        }

        void UpdateTransform(byte rev, byte[] content)
        {
            if (rev != 0) return;
            var p = new TransformInfoPacket(content);

            transform.position = p.position;
            transform.rotation = p.rotation;
        }

    }


}