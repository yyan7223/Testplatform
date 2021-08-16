using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SPINACH.Networking
{

    public class Utils
    {

        public static void EncodeVector2(Vector2 v, byte[] buf, int offset)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(v.x), 0, buf, offset + 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(v.y), 0, buf, offset + 4, 4);
        }

        public static Vector2 DecodeVector2(byte[] buf, int offset)
        {
            return new Vector2(BitConverter.ToSingle(buf, offset),
                BitConverter.ToSingle(buf, offset + 4));
        }

        public static void EncodeVector3(Vector3 v, byte[] buf, int offset)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(v.x), 0, buf, offset + 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(v.y), 0, buf, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(v.z), 0, buf, offset + 8, 4);
        }

        public static Vector3 DecodeVector3(byte[] buf, int offset)
        {
            return new Vector3(BitConverter.ToSingle(buf, offset),
                BitConverter.ToSingle(buf, offset + 4),
                BitConverter.ToSingle(buf, offset + 8));
        }

        public static void EncodeQuaternion(Quaternion v, byte[] buf, int offset)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(v.x), 0, buf, offset + 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(v.y), 0, buf, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(v.z), 0, buf, offset + 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(v.w), 0, buf, offset + 12, 4);
        }

        public static Quaternion DecodeQuaternion(byte[] buf, int offset)
        {
            return new Quaternion(BitConverter.ToSingle(buf, offset),
                BitConverter.ToSingle(buf, offset + 4),
                BitConverter.ToSingle(buf, offset + 8),
                BitConverter.ToSingle(buf, offset + 12));
        }

        public static byte[] EncodeRenderTexture(RenderTexture renderT)
        {
            int width = renderT.width;
            int height = renderT.height;
            Texture2D tex2d = new Texture2D(width, height, TextureFormat.ARGB32, false);
            RenderTexture.active = renderT;
            tex2d.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex2d.Apply();

            byte[] b = tex2d.EncodeToPNG();
            Debug.Log("encoding finished");
            return b;
        }

        public static Texture2D DecodeRenderTexture(byte[] buf, int width, int height)
        {
            Texture2D tex = new Texture2D(width, height);
            tex.LoadRawTextureData(buf);
            Debug.Log("decoding finished");
            return tex;
        }
    }
}