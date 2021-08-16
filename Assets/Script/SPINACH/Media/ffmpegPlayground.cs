using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace SPINACH.Media
{
    /// <summary>
    /// this was used to mess around with possible implementations and contain unsafe bad practices.
    /// DO NOT use this either directly, indirectly, or for reference in any actual code.
    ///
    /// -haoyan
    /// </summary>
    public class ffmpegPlayground : MonoBehaviour
    {

        public Camera cam;

        public float bitrate;
        public float bufsize;
        public float encodeRate;
        public int encodedFrameAmount;
        private int frameCount;

        private Process _encodeProcess;
        private StreamWriter encodeStdin;
        private StreamReader encodeStdout;
        private StreamReader encodeStderr;

        private Process _decodeProcess;
        private StreamWriter decodeStdin;
        private StreamReader decodeStdout;
        private StreamReader decodeStderr;

        private RenderTexture rtt;


        private bool weAreStillGoing = false;

        private Queue<byte[]> framebuffers = new Queue<byte[]>();
        private Thread pipeThread;
        private Thread copyThread;


        // Start is called before the first frame update
        void Start()
        {
            weAreStillGoing = true;

            // set render texture
            rtt = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtt;

            // start encoding session
            string encodeArgu = "-y -f rawvideo -vcodec rawvideo -pixel_format rgba " +
                                    "-video_size {0}x{1} -r {4} -i - " +
                                    "-preset ultrafast -vcodec libx264 " +
                                    "-tune zerolatency -maxrate {2}k -bufsize {3}k -r {4} -f flv -";
            var _encodeArgu = string.Format(encodeArgu, Screen.width, Screen.height, bitrate, bufsize, encodeRate);

            string decodeArgu = "-y -probesize 32 -flags low_delay -f flv -vcodec h264 -i - " +
                                    "-video_size {0}x{1} -f rawvideo -vcodec rawvideo -pix_fmt rgba -";
            var _decodeArgu = string.Format(decodeArgu, Screen.width, Screen.height);

            _encodeProcess = new Process();
            _encodeProcess.StartInfo = new ProcessStartInfo
            {
                FileName = Application.streamingAssetsPath + "/SPINACH/Media/ffmpeg/Windows/ffmpeg.exe",
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                /*
                 * Why flv?
                 *  flv have the least overhead compares to other container format.
                 *  I've tried raw h264 stream which definitely have the lowest overhead(stream data size) but
                 * raw h264 doesnt contains frame delimiter, thus make long probing necessary and introduce unacceptable delay.
                 * Which renders a container format necessary and flv yields the best result balanced between latency and bitrate.
                 */
                Arguments = _encodeArgu
            };
            _encodeProcess.Start();
            encodeStdin = _encodeProcess.StandardInput;
            encodeStdout = _encodeProcess.StandardOutput;
            encodeStderr = _encodeProcess.StandardError;

            // start decoding session
            _decodeProcess = new Process();
            _decodeProcess.StartInfo = new ProcessStartInfo
            {
                FileName = Application.streamingAssetsPath + "/SPINACH/Media/ffmpeg/Windows/ffmpeg.exe",
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = _decodeArgu
            };
            _decodeProcess.Start();
            decodeStdin = _decodeProcess.StandardInput;
            decodeStdout = _decodeProcess.StandardOutput;
            decodeStderr = _decodeProcess.StandardError;

            // start 
            pipeThread = new Thread(pipepipepipepipe);
            pipeThread.Start();

            copyThread = new Thread(copycopycopycopy);
            copyThread.Start();
        }

        private ulong bytesPiped = 0;
        private ulong lastsecPiped = 0;
        void pipepipepipepipe()
        {
            var lastsec = DateTime.UtcNow;
            while (weAreStillGoing)
            {
                var buf = new byte[4096];
                var act = encodeStdout.BaseStream.Read(buf, 0, 4096);

                bytesPiped += (ulong)act;
                lastsecPiped += (ulong)act;

                decodeStdin.BaseStream.Write(buf, 0, act);
                decodeStdin.BaseStream.Flush();


                if ((DateTime.UtcNow - lastsec).TotalSeconds > 1)
                {
                    lastsec = DateTime.UtcNow;
                    lastsecPiped = 0;
                }
            }
        }

        void copycopycopycopy()
        {
            int bs = Screen.width * Screen.height * 4;
            byte[] fb = new byte[bs];

            while (weAreStillGoing)
            {

                int nbl = bs;
                int bnrd = 0;
                do
                {
                    var act = decodeStdout.BaseStream.Read(fb, bnrd, nbl);
                    bnrd += act;
                    nbl -= act;
                } while (nbl > 0);


                lock (framebuffers)
                {
                    var nbb = new byte[bs];
                    Buffer.BlockCopy(fb, 0, nbb, 0, bs);
                    framebuffers.Enqueue(nbb);
                }
            }
        }

        private void Update()
        {
            // transform.position = new Vector3(frameCount, 1.495f, frameCount);
            // transform.localEulerAngles = new Vector3(0, frameCount * 90, 0);

            cam.Render();
            var rq = AsyncGPUReadback.Request(rtt);
            rq.WaitForCompletion();//bruh async fuck off.
            var rb = rq.GetData<byte>();
            byte[] managed = new byte[rb.Length];
            rb.CopyTo(managed);//nobody cares about marshalling performance lol.

            encodeStdin.BaseStream.Write(managed, 0, managed.Length);
            encodeStdin.BaseStream.Flush();
            frameCount++;

            var startEncode = DateTime.Now.Millisecond;
            encodeStdin.Close();
            _encodeProcess.WaitForExit();
            var endEncode = DateTime.Now.Millisecond;

            Debug.Log(string.Format("Encoded Stream Transfered: {0}KB, for 1 frame (resolution: {1}x{2})", ((double)bytesPiped / 1024d), Screen.width, Screen.height));
            Debug.Log(string.Format("Encoded time for 1 frame: {0} ms)", endEncode - startEncode));

            decodeStdin.Close();
            _decodeProcess.WaitForExit();

        }

    }

}