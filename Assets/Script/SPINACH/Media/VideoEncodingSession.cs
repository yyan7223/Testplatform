using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SPINACH.Media
{

    public class VideoEncodingSession
    {
        public static ffmpegSession _ffmpeg;

        private int _width;
        private int _height;

        // Windows
        //private const string argu = "-y -f rawvideo -vcodec rawvideo -pixel_format rgba " +
        //                            "-video_size {0}x{1} -r {4} -i - " +
        //                            "-preset ultrafast -vcodec libx264 " +
        //                            "-tune zerolatency -maxrate {2}k -bufsize {3}k -r {4} -f flv -";

        // Adnroid
        private const string argu = "-y -f rawvideo -vcodec rawvideo -pixel_format rgba " +
                                    "-video_size {0}x{1} -r {4} -i - " +
                                    "-preset ultrafast -vcodec libx264 " +
                                    "-tune zerolatency -maxrate {2}k -bufsize {3}k -r {4} -f mpeg -";

        public VideoEncodingSession(int w, int h, int bitrate, int bufsize, int framerate)
        {
            _width = w;
            _height = h;

            var a = string.Format(argu, w, h, bitrate, bufsize, framerate);
            _ffmpeg = new ffmpegSession(a, true);

            _ffmpeg.Launch();
        }

        public string EndSession()
        {
            return _ffmpeg.EndSession();
        }

        /// <summary>
        /// Push a raw frame image in RGBA32 format to encode.
        /// </summary>
        /// <param name="buf">RGBA32 frame buffer</param>
        public void PushFrame(byte[] buf)
        {
            if (buf.Length != _width * _height * 4) throw new ArgumentException("bad frame buffer.");
            _ffmpeg.inputStream.Write(buf, 0, buf.Length);
            _ffmpeg.inputStream.Flush();
        }

        public int ConsumeEncodedStream(byte[] buf, int offset, int maxLength)
        {
            return _ffmpeg.outputStream.Read(buf, offset, maxLength);
        }

    }

}
