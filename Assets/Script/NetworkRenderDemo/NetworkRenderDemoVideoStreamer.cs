using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using SPINACH.Networking;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SPINACH.Media
{

    /// <summary>
    /// this was used to mess around with possible implementations and contain unsafe bad practices.
    /// DO NOT use this either directly, indirectly, or for reference in any actual code.
    ///
    /// -haoyan
    /// </summary>
    public class NetworkRenderDemoVideoStreamer : MonoBehaviour
    {
        class VideoStreamRequestPacket : IRoutablePacketContent
        {
            public const byte NTYPE = 0xcb;

            public int width;
            public int height;

            public VideoStreamRequestPacket(int w, int h)
            {
                width = w;
                height = h;
            }

            public VideoStreamRequestPacket(byte[] bytes)
            {
                width = BitConverter.ToInt32(bytes, 0);
                height = BitConverter.ToInt32(bytes, 4);
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
                Buffer.BlockCopy(BitConverter.GetBytes(width), 0, buf, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(height), 0, buf, 4, 4);
                return buf;
            }
        }

        class VideoStreamSlicePacket : IRoutablePacketContent
        {
            public const byte NTYPE = 0xce;
            public const int MAXSLICE = 256;

            public byte[] codingBuf = new byte[MAXSLICE];
            public int validSize;

            public VideoStreamSlicePacket(byte[] buf, int offset, int len)
            {
                if (len > MAXSLICE)
                    throw new ArgumentOutOfRangeException("max slice exceeded, manual slicing in needed.");

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
                //TODO: support dynamic sizing
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


        public Camera renderingCamera;
        public RawImage uidisplay;

        public int encodeRate = 30;

        private RenderTexture rtt;
        private Texture2D texx;
        private bool weAreStillGoing = false;

        private bool streamingRequested = false;
        private int contentWidth;
        private int contentHeight;

        private VideoEncodingSession _encodingSession;
        private VideoDecodingSession _decodingSession;

        private Thread pipeThread;

        private NetworkObjectMessenger _nom;

        private void Awake()
        {
            _nom = GetComponent<NetworkObjectMessenger>();

        }

        private void Start()
        {
            if (NetworkDispatch.Default().isServer) InitServer();
        }

        private Queue<VideoStreamSlicePacket> receivedSlices;

        public int getqueuecount()
        {
            return receivedSlices == null ? 0 : receivedSlices.Count;
        }
        public void InitClient(RawImage target)
        {
            weAreStillGoing = true;
            Destroy(renderingCamera.gameObject);
            texx = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);

            uidisplay = target;
            uidisplay.texture = texx;

            receivedSlices = new Queue<VideoStreamSlicePacket>();
            int bs = Screen.width * Screen.height * 4;
            byte[] fb = new byte[bs];
            for (int i = 0; i < bs; i += 4)
            {
                fb[i] = 0;
                fb[i + 1] = 255;
                fb[i + 2] = 0;
                fb[i + 3] = 255;
            }
            texx.LoadRawTextureData(fb);
            texx.Apply(false);


            StartCoroutine(delayInitClient());
            _nom.RegisterMethod(VideoStreamSlicePacket.NTYPE, (rev, bytes) =>
            {
                var p = new VideoStreamSlicePacket(bytes);
                lock (receivedSlices)
                {
                    receivedSlices.Enqueue(p);
                    Debug.Log(string.Format("client received {0}", p.validSize));
                }
                c++;
            });

            _decodingSession = new VideoDecodingSession(Screen.width, Screen.height);
            pipeThread = new Thread(FeedDecoder);
            pipeThread.Start();
            Debug.Log("decoding services started and waiting for video stream!");
        }

        IEnumerator delayInitClient()
        {
            yield return new WaitForSeconds(0.5f);
            _nom.SendMessage(new VideoStreamRequestPacket(Screen.width, Screen.height));
        }

        public int c;
        private void FeedDecoder()
        {
            var lastsec = DateTime.UtcNow;
            while (weAreStillGoing)
            {
                VideoStreamSlicePacket p = null;
                lock (receivedSlices)
                {
                    if (receivedSlices.Count > 0) p = receivedSlices.Dequeue();
                }
                if (p == null) continue;
                bytesPiped += (ulong)p.validSize;
                lastsecPiped += (ulong)p.validSize;
                _decodingSession.PushStream(p.codingBuf, 0, p.validSize);

                if ((DateTime.UtcNow - lastsec).TotalSeconds > 1)
                {
                    lastsec = DateTime.UtcNow;
                    lastsecPiped = 0;
                }
            }
        }

        public void InitServer()
        {
            weAreStillGoing = true;



            _nom.RegisterMethod(VideoStreamRequestPacket.NTYPE, (rev, bytes) =>
            {
                var req = new VideoStreamRequestPacket(bytes);
                contentWidth = req.width;
                contentHeight = req.height;
                streamingRequested = true;
                Debug.Log(string.Format("Stream request received {0}x{1}", contentWidth, contentHeight));

            });

            Debug.Log("video stream service started!");
        }

        private void FixedUpdate()
        {
            if (NetworkDispatch.Default().isServer)
            {
                StreamCapture();
            }
            else
            {
                FetchNewFrame();
            }
        }

        void FetchNewFrame()
        {
            _decodingSession.ConsumeFrame(texx);
        }

        private float lastEncoded;
        void StreamCapture()
        {
            if (!streamingRequested) return;

            //lazy init
            if (_encodingSession == null)
            {
                rtt = new RenderTexture(contentWidth, contentHeight, 0, RenderTextureFormat.ARGB32);
                renderingCamera.targetTexture = rtt;
                _encodingSession = new VideoEncodingSession(contentWidth, contentHeight, 800, 1000, encodeRate);

                pipeThread = new Thread(StreamCaptureSendEncoded);
                pipeThread.Start();
                Debug.Log("video encoder started!");
            }
            if (Time.time - lastEncoded < 1 / (float)encodeRate) return;

            renderingCamera.Render();
            var rq = AsyncGPUReadback.Request(rtt);
            rq.WaitForCompletion();//bruh async fuck off.
            var rb = rq.GetData<byte>();

            byte[] managed = new byte[rb.Length];
            rb.CopyTo(managed);//nobody cares about marshalling performance lol.
            // Debug.Log("managed length is: " + managed.Length);
            _encodingSession.PushFrame(managed);

            lastEncoded = Time.time;
        }

        public ulong bytesPiped = 0;
        public ulong lastsecPiped = 0;
        void StreamCaptureSendEncoded()
        {
            while (weAreStillGoing)
            {
                var lastsec = DateTime.UtcNow;
                while (weAreStillGoing)
                {
                    byte[] buf = new byte[VideoStreamSlicePacket.MAXSLICE];

                    var act = _encodingSession.ConsumeEncodedStream(buf, 0, buf.Length);
                    bytesPiped += (ulong)act;
                    lastsecPiped += (ulong)act;

                    if (act > 0)
                    {
                        _nom.SendMessage(new VideoStreamSlicePacket(buf, 0, act));

                    }


                    if ((DateTime.UtcNow - lastsec).TotalSeconds > 1)
                    {
                        lastsec = DateTime.UtcNow;
                        lastsecPiped = 0;
                    }
                }
            }
        }

        private void OnDisable()
        {
            weAreStillGoing = false;
            if (_encodingSession != null) Debug.Log(_encodingSession.EndSession());
            if (_decodingSession != null) Debug.Log(_decodingSession.EndSession());
        }
    }

}