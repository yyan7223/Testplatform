using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using SPINACH.Networking;
using SPINACH.Media;
using UnityEngine;

namespace VPRAssets.Scripts
{
    public class PlayerControlLogic : MonoBehaviour
    {
        // Joystick
        public SimpleTouchController leftController;
	    public SimpleTouchController rightController;

        // Player control related variables and classes 
        private float moveDistance = 0.1f;
        Vector3 targetAngles;
        Vector3 followAngles;
        Vector3 followVelocity;
        Quaternion originalRotation;
        Vector3 originalPosition;
        private CapsuleCollider capsule;                                                  
        private IComparer rayHitComparer;
        private Quaternion input;
        public Vector2 rotationRange = new Vector3(70, 70);
        public float rotationSpeed = 10.0f;
        public float dampingTime = 0.2f;

        public GameObject VPRCameraPrefab;

        // Client Sending Transform related buffer
        private ConcurrentQueue<Vector3> tmpPositionBuffer;
        private ConcurrentQueue<Quaternion> tmpRotationBuffer;
        private Vector3 tmpPosition, currentPosition;
        private Quaternion tmpRotation, currentRotation;
        
        // Server receiving Transform related buffer
        private ConcurrentQueue<Vector3> receivedPositionBuffer;
        private ConcurrentQueue<Quaternion> receivedRotationBuffer;
        private Vector3 receivedPosition;
        private Quaternion receivedRotation;
        public static bool hasDequeuedNewTransform = false; 

        // Transform packet
        private NetworkObjectMessenger _nom;
        public const byte NTYPE = 2;
        class TransformInfoPacket : IRoutablePacketContent
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool trigger = true;

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

        class RayHitComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                return ((RaycastHit)x).distance.CompareTo(((RaycastHit)y).distance);
            }
        }

        private void Awake()
        {
            Instantiate(VPRCameraPrefab, transform.Find("Head"));

            receivedPositionBuffer = new ConcurrentQueue<Vector3>();
            receivedRotationBuffer = new ConcurrentQueue<Quaternion>();

            tmpPositionBuffer = new ConcurrentQueue<Vector3>();
            tmpRotationBuffer = new ConcurrentQueue<Quaternion>() ;

            _nom = GetComponent<NetworkObjectMessenger>();
            if (NetworkDispatch.Default().isServer)
            {
                _nom.RegisterMethod(NTYPE, bufferReceivedTransform);
            }

            // Set up a reference to the capsule collider.
            capsule = GetComponent<Collider>() as CapsuleCollider;
            rayHitComparer = new RayHitComparer();
            originalRotation = transform.rotation;
            originalPosition = transform.position;
            tmpPosition = originalPosition;
        }

        private void Start()
        {
            // do nothing
        }

        private void Update()
        {
            if (NetworkDispatch.Default().isServer)
            {
                UpdateTransform();
            }

            if (!NetworkDispatch.Default().isServer)
            {
                input =
                new Quaternion(leftController.GetTouchPosition.x * 0.8f, // movement
                            leftController.GetTouchPosition.y * 0.8f,
                            rightController.GetTouchPosition.x * 0.5f, // rotation
                            rightController.GetTouchPosition.x * 0.5f);
                
                tmpPosition = calculatePosition(input.x, input.y, tmpPosition);
                tmpRotation = calculateRotation(input.z, input.w);
                _nom.SendMessage(new TransformInfoPacket(tmpPosition, tmpRotation));
                tmpPositionBuffer.Enqueue(tmpPosition);
                tmpRotationBuffer.Enqueue(tmpRotation);
                
                if(VisionPerceptionRendering.hasReceivedOneNewRemoteRT)
                {
                    if(tmpPositionBuffer.Count > 0 && tmpRotationBuffer.Count > 0)
                    {
                        tmpPositionBuffer.TryDequeue(out currentPosition);
                        tmpRotationBuffer.TryDequeue(out currentRotation);
                        transform.position = currentPosition;
                        transform.rotation = currentRotation;
                    }
                }
            }
        }

        // Rotation calculation result is updated based on the original Rotation
        Quaternion calculateRotation(float inputH, float inputV)
        {
            // wrap values to avoid springing quickly the wrong way from positive to negative
            if (targetAngles.y > 180) { targetAngles.y -= 360; followAngles.y -= 360; }
            if (targetAngles.x > 180) { targetAngles.x -= 360; followAngles.x -= 360; }
            if (targetAngles.y < -180) { targetAngles.y += 360; followAngles.y += 360; }
            if (targetAngles.x < -180) { targetAngles.x += 360; followAngles.x += 360; }

            // with mouse input, we have direct control with no springback required.
            targetAngles.y += inputH * rotationSpeed;
            targetAngles.x += inputV * rotationSpeed;

            // clamp values to allowed range
            targetAngles.y = Mathf.Clamp(targetAngles.y, -rotationRange.y * 0.5f, rotationRange.y * 0.5f);
            targetAngles.x = Mathf.Clamp(targetAngles.x, -rotationRange.x * 0.5f, rotationRange.x * 0.5f);

            // smoothly interpolate current values to target angles
            followAngles = Vector3.SmoothDamp(followAngles, targetAngles, ref followVelocity, dampingTime);

            Quaternion rotation = originalRotation * Quaternion.Euler(-followAngles.x, followAngles.y, 0);

            return rotation;
        }

        // Position calculated result is updated based on the last position
        Vector3 calculatePosition(float inputX, float inputY, Vector3 lastPosition)
        {
            if (inputX > 0) inputX = 1;
            else if (inputX < 0) inputX = -1;
            if (inputY > 0) inputY = 1;
            else if (inputY < 0) inputY = -1;
            Vector3 position = lastPosition + transform.forward * inputY * moveDistance + transform.right * inputX * moveDistance;

            return position;
        }

        // Store the received Transform
        void bufferReceivedTransform(byte rev, byte[] content)
        {
            if (rev != 0) return;
            var p = new TransformInfoPacket(content);

            receivedPositionBuffer.Enqueue(p.position);
            receivedRotationBuffer.Enqueue(p.rotation);
        }

        // Server update transform according to dequeued results
        void UpdateTransform()
        {
            if(receivedPositionBuffer.Count > 0 && receivedRotationBuffer.Count > 0)
            {
                receivedPositionBuffer.TryDequeue(out receivedPosition); 
                receivedRotationBuffer.TryDequeue(out receivedRotation);

                transform.position = receivedPosition;
                transform.rotation = receivedRotation;

                hasDequeuedNewTransform = true;
            }
        }
    }
}