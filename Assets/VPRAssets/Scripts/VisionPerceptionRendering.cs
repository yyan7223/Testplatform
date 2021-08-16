using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using SPINACH.Networking;
using SPINACH.Media;

namespace VPRAssets.Scripts
{
    public class VisionPerceptionRendering : MonoBehaviour
    {
        // Foveated Rendering related functions 
        private FRutils _FRutils;
        // LIWC component related functions 
        private LIWCutils _LIWCutils; 

        // Foveated Rendering related parameters
        private Vector2 foveaCoordinate = new Vector2(0.5f, 0.5f); // Define the coordinates where eyes focus on (normalized)
        private float E1 = 0.1f; // Define eccentricity
        private float foveaRTScale = 0.2f; // foveaRTScale should always be twice of E1
        private float remoteRTScale = 0.25f; // Decoding services need this parameter to be declared before Start(), and its value should be kept the same as the one declared in Start()
        private Vector3 eyeballMovingSpeed = new Vector3(0.1f, 0.1f, 0.1f);

        // LIWC component related parameters
        float latency = 0; // frame latency
        int frameCount = 0;
        Vector3 oldPos, newPos;
        Quaternion oldRot, newRot;
        Vector2 oldCoord, newCoord;
        uint mappingIndex;

        // Collaborative Foveated Rendering related parameters
        private Camera cam_remote;
        private Camera cam_fovea;
        private RenderTexture foveaRT;
        private RenderTexture remoteRT; // Server
        private Texture2D receivedRemoteRT; //Client
        public Material compositeMaterial; // material for shader that composite three render textures

        // ffmpeg encoding decoding related parameters
        private VideoEncodingSession _remoteRTEncodingSession;
        private VideoDecodingSession _remoteRTDecodingSession;
        private NetworkObjectMessenger _nom;
        private Thread _remoteRTPipeThread;
        private Queue<VideoStreamSlicePacket> receivedRemoteRTSlices;
        private int texEncodeRate = 60;
        public static bool hasReceivedOneNewRemoteRT = false;

        // because both fovea camera and remote camera are attached below one single camera,
        // the onRenderImage() will be called for once after all Start() has been executed, before all Update()
        // we need this variable to avoid unpredictable behaviour before entering into Update()
        private bool hasPassedFisrtORI = false; 

        // Rendertexture transmission packet
        class VideoStreamSlicePacket : IRoutablePacketContent
        {
            public const byte NTYPE = 0xce;
            public const int MAXSLICE = 16384; 

            public byte[] codingBuf = new byte[MAXSLICE];
            public int validSize;

            public VideoStreamSlicePacket(byte[] buf, int offset, int len)
            {
                if (len > MAXSLICE)
                    throw new ArgumentOutOfRangeException("remote rendertexture max slice exceeded, manual slicing in needed.");

                Buffer.BlockCopy(buf, offset, codingBuf, 0, len);
                validSize = len;
            }

            public VideoStreamSlicePacket(byte[] bytes)
            {
                validSize = BitConverter.ToInt32(bytes, 0);
                Buffer.BlockCopy(bytes, 4, codingBuf, 0, validSize);
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
                return MAXSLICE + 4;
            }

            public byte[] GetByteStream()
            {
                var buf = new byte[GetByteLength()];
                Buffer.BlockCopy(BitConverter.GetBytes(validSize), 0, buf, 0, 4);
                Buffer.BlockCopy(codingBuf, 0, buf, 4, validSize);
                return buf;
            }
        }

        void Awake()
        {
            _nom = transform.root.GetComponent<NetworkObjectMessenger>();
            if (NetworkDispatch.Default().isServer) PrepareEncodeService();
            if (!NetworkDispatch.Default().isServer) PrepareDecodeService();
            Application.targetFrameRate = texEncodeRate;
        }

        void Start()
        {
            InitializeRenderTarget();

            if (NetworkDispatch.Default().isServer) // On server, disable Camera_fovea, get Camera_remote
            {
                transform.Find("Camera_fovea").gameObject.SetActive(false);

                cam_remote = transform.Find("Camera_remote").gameObject.GetComponent<Camera>();
                cam_remote.targetTexture = remoteRT;
                _FRutils = new FRutils(cam_remote); // initialize Foveated Rendering utils
                cam_remote.enabled = false;
            }

            if (!NetworkDispatch.Default().isServer) 
            { 
                // initialize LIWC component utils
                GameObject scene = GameObject.Find("/Map_v1");
                _LIWCutils = new LIWCutils(scene);

                // On client, disable Camera_remote, get Camera_fovea 
                transform.Find("Camera_remote").gameObject.SetActive(false);

                cam_fovea = transform.Find("Camera_fovea").gameObject.GetComponent<Camera>();
                cam_fovea.targetTexture = foveaRT;
                _FRutils = new FRutils(cam_fovea); // initialize Foveated Rendering utils
                cam_fovea.enabled = false;
            }
        }

        void Update()
        {
            // once enter Update(), the extra onRenderImage must have been executed
            hasPassedFisrtORI = true;

            if (NetworkDispatch.Default().isServer && PlayerControlLogic.hasDequeuedNewTransform)
            {
                remoteRT.Release();
                cam_remote.Render();
                
                frameCount++;
            }

            if (!NetworkDispatch.Default().isServer && hasReceivedOneNewRemoteRT)
            {
                // // Refresh GPU_m and throughput according to frame time consumption
                latency = Time.deltaTime;
                Debug.Log(string.Format("The real-time FPS is: {0}", 1.0f/latency));
                // _LIWCutils.refreshGPUm(latency);
                // _LIWCutils.refreshThroughPut(latency);

                // // The simulation of fetching fovea coordinate from eyetracker.
                foveaCoordinate = new Vector2(0.5f, 0.5f);

                if(frameCount > 0)
                {
                    // Generate mapping index according to the old value and new value
                    newPos = transform.root.position;
                    newRot = transform.root.rotation;
                    newCoord = foveaCoordinate;
                    mappingIndex = _LIWCutils.generateMappingIndex(oldPos, newPos, oldRot, newRot, oldCoord, newCoord);
                    
                    // get best E1
                    // _LIWCutils.totalVertsCounter(cam_fovea, E1, foveaCoordinate);
                    // E1 = _LIWCutils.selectBestEccentricity(frameCount, mappingIndex, E1, latency);
                    // Debug.Log(string.Format("frame: {0}, E1: {1}, vertice: {2}", frameCount, E1, _LIWCutils.totalVerticesCount));
                }
                
                // // refresh the resolution of different layers according to the selected E1 and E2
                foveaRTScale = 2 * E1;
                // RefreshRenderTextureScale(foveaRTScale);

                // Reset Camera PM according to best E1 and execute rendering
                cam_fovea.ResetProjectionMatrix();
                _FRutils.RefreshPM(cam_fovea, foveaCoordinate, E1);
                foveaRT.Release();
                cam_fovea.Render();

                // let current Transform and eyes coordinates to be old values
                oldPos = transform.root.position;
                oldRot = transform.root.rotation;
                oldCoord = foveaCoordinate;

                // reset the bool variable
                hasReceivedOneNewRemoteRT = false;

                frameCount++;
            }
        }

        /**
        * @description: OnRenderImage is automatically called after all rendering is complete to render image (LateUpdate).
                        Always used for Postprocessing effects.
                        It allows you to modify final image by processing it with shader based filters. 
                        The incoming image is source render texture.The result should end up in destination render texture.
                        You must always issue a Graphics.Blit() or render a fullscreen quad if your override this method.
        * @param {RenderTexture source, RenderTexture destination} 
        * @return {void} 
        */
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // On the server, send the rendered remoteRT to Client
            if (NetworkDispatch.Default().isServer && PlayerControlLogic.hasDequeuedNewTransform && hasPassedFisrtORI)
            {
                PushRenderedResult2FFmpeg();
            }

            // On the client, composite foveaRT and received remoteRT
            if (!NetworkDispatch.Default().isServer) 
            {
                _FRutils.RefreshShaderParameter(compositeMaterial, foveaRT,
                                                receivedRemoteRT,
                                                foveaCoordinate,
                                                E1, E1 + 0.1f);
                Graphics.Blit(receivedRemoteRT, destination, compositeMaterial);

                // check whether successfully decode a remoteRT
                if(_remoteRTDecodingSession.ConsumeFrame(receivedRemoteRT)) 
                {
                    hasReceivedOneNewRemoteRT = true;
                }
            }
        }

        // Initialize rendertexture  
        void InitializeRenderTarget()
        {
            if (NetworkDispatch.Default().isServer)
            {
                remoteRT = new RenderTexture(
                    (int)(UnityEngine.Screen.width * remoteRTScale),
                    (int)(UnityEngine.Screen.height * remoteRTScale),
                    16, RenderTextureFormat.ARGB32
                );
            }
            if (!NetworkDispatch.Default().isServer)
            {
                // Creat the render texture
                foveaRT = new RenderTexture(
                    (int)(UnityEngine.Screen.width * foveaRTScale),
                    (int)(UnityEngine.Screen.height * foveaRTScale),
                    16, RenderTextureFormat.ARGB32
                );

                // fix the resolution
                receivedRemoteRT = new Texture2D(
                            (int)(UnityEngine.Screen.width * remoteRTScale),
                            (int)(UnityEngine.Screen.height * remoteRTScale),
                            TextureFormat.RGBA32, false);
            }
        }

        // Change the resolution of foveaRT without reallocating it
        void RefreshRenderTextureScale(float scale)
        {
            // With 'Allow Dynamic Resolution' box checked, render targets have the DynamicallyScalable flag
            // The ScalableBufferManager handles the scaling of any render textures that have been marked to be DynamicallyScalable
            float widthScale = scale;
            float heightScale = scale;
            ScalableBufferManager.ResizeBuffers(widthScale, heightScale);
            Debug.Log(string.Format("frame: {0}, E1: {1}, foveaRT size: {2}x{3}:", frameCount, E1, foveaRT.width, foveaRT.height));
            // Please reference https://docs.unity3d.com/Manual/DynamicResolution.html
            // https://docs.unity3d.com/ScriptReference/ScalableBufferManager.html for more details
        }

        // Fetch tbe rendered remoteRT from GPU, encode them to byte stream and send through the network
        public void PushRenderedResult2FFmpeg()
        {
            // Fetch the remotely rendered texture in GPU, store as the byte stream, and push it to the encoding session
            var request = AsyncGPUReadback.Request(remoteRT);
            request.WaitForCompletion();//bruh async fuck off.
            var requestTexture = request.GetData<byte>();
            byte[] managed = new byte[requestTexture.Length];
            requestTexture.CopyTo(managed);
            _remoteRTEncodingSession.PushFrame(managed); // push into the encoding stream 

            PlayerControlLogic.hasDequeuedNewTransform = false;
        }

        // Start remoteRT encoding session
        private void PrepareEncodeService()
        {
            _remoteRTEncodingSession = new VideoEncodingSession((int)(UnityEngine.Screen.width * remoteRTScale),
                                                                (int)(UnityEngine.Screen.height * remoteRTScale),
                                                                1600, 2000, texEncodeRate);

            _remoteRTPipeThread = new Thread(StreamCaptureSendEncoded);
            _remoteRTPipeThread.Start();
        }

        // Thread function to send encoded RT (byte stream) to Client
        private void StreamCaptureSendEncoded()
        {
            while (true)
            {
                byte[] buf = new byte[VideoStreamSlicePacket.MAXSLICE];

                var act = _remoteRTEncodingSession.ConsumeEncodedStream(buf, 0, buf.Length);

                if (act > 0)
                {
                    _nom.SendMessage(new VideoStreamSlicePacket(buf, 0, act));
                    // Debug.Log(string.Format("has sent {0} bytes", act));
                }
            }
        }

        // Register the method to receive transmitted encoded RT
        // Start receiveedRemoteRT decoding session
        private void PrepareDecodeService()
        {
            _remoteRTDecodingSession = new VideoDecodingSession((int)(UnityEngine.Screen.width * remoteRTScale),
                                                        (int)(UnityEngine.Screen.height * remoteRTScale));
            _remoteRTPipeThread = new Thread(FeedDecoder);
            _remoteRTPipeThread.Start();

            receivedRemoteRTSlices = new Queue<VideoStreamSlicePacket>();
            _nom.RegisterMethod(VideoStreamSlicePacket.NTYPE, (rev, bytes) =>
            {
                var p = new VideoStreamSlicePacket(bytes);
                lock (receivedRemoteRTSlices)
                {
                    receivedRemoteRTSlices.Enqueue(p);
                    // Debug.Log(string.Format("client received remote rendertexture {0}", p.validSize));
                }
            });

            Debug.Log("remoteRT decoding services started and waiting for remoteRT video stream!");
        }

        // Thread function to push received encoded RT (byte stream) to the decoding service
        private void FeedDecoder()
        {
            while (true)
            {
                VideoStreamSlicePacket p = null;
                lock (receivedRemoteRTSlices)
                {
                    if (receivedRemoteRTSlices.Count > 0) p = receivedRemoteRTSlices.Dequeue();
                }
                if (p == null) continue;
                _remoteRTDecodingSession.PushStream(p.codingBuf, 0, p.validSize);
            }
        }
    }
}