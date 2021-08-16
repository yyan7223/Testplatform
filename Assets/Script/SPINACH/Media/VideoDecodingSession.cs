using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace SPINACH.Media
{

    public class VideoDecodingSession
    {
        private ffmpegSession _ffmpeg;
        private Thread _frameFetcherThread;

        private int _width;
        private int _height;
        private bool _sessionActive;

        // Windows
        //private const string argu = "-y -probesize 32 -flags low_delay -f flv -vcodec h264 -i - " +
        //                            "-video_size {0}x{1} -f rawvideo -vcodec rawvideo -pix_fmt rgba - ";

        // Adnroid
        private const string argu = "-y -probesize 32 -flags low_delay -f mpeg -vcodec h264 -i - " +
                                    "-video_size {0}x{1} -f rawvideo -vcodec rawvideo -pix_fmt rgba - " +
                                    "2>/storage/emulated/0/log.txt";

        private Queue<byte[]> framebuffers = new Queue<byte[]>();

        public VideoDecodingSession(int w, int h)
        {
            _width = w;
            _height = h;

            var a = string.Format(argu, w, h);
            _ffmpeg = new ffmpegSession(a, true);

            _sessionActive = true;
            _ffmpeg.Launch();
            _frameFetcherThread = new Thread(FrameFetcher);
            _frameFetcherThread.Start();
        }

        void FrameFetcher()
        {
            int bs = _width * _height * 4;
            byte[] fb = new byte[bs];

            while (_sessionActive)
            {

                int nbl = bs;
                int bnrd = 0;
                do
                {
                    var act = _ffmpeg.outputStream.Read(fb, bnrd, nbl);
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

        public string EndSession()
        {
            return _ffmpeg.EndSession();
        }

        public void PushStream(byte[] buf, int offset, int count)
        {
            _ffmpeg.inputStream.Write(buf, offset, count);
            _ffmpeg.inputStream.Flush();
        }

        public bool ConsumeFrame(Texture2D texture)
        {
            if (texture.width != _width || texture.height != _height || texture.format != TextureFormat.RGBA32)
            {
                Debug.Log(string.Format("texture.width: {0}, texture.height: {1}, _width: {2}, _height: {3}", texture.width, texture.height, _width, _height));
                throw new ArgumentException("Bad texture object.");
            }

            byte[] fb = null;
            lock (framebuffers)
            {
                if (framebuffers.Count > 0)
                    fb = framebuffers.Dequeue();
            }

            if (fb == null) return false;

            texture.LoadRawTextureData(fb);
            texture.Apply();
            return true;
        }
    }

}
