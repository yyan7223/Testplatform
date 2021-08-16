using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using UnityEngine;


namespace SPINACH.Networking
{

    public class PacketFrame
    {
        public readonly byte[] magic = {0xba, 0xbe};
        public int size;
        public byte type;
        
        /// <summary>
        /// PacketFrame types:
        /// 0x00: default
        /// 0x01: ServerPeerHandshakeFrame
        /// 0x8c: ObjectRoutablePacket
        /// </summary>
        
        public byte[] content;

        public PacketFrame(byte[] ctx, byte t)
        {
            content = new byte[ctx.Length];
            Buffer.BlockCopy(ctx, 0, content, 0, ctx.Length);

            type = t;
            size = content.Length;
        }

        private PacketFrame(byte[] ctx, int size, byte type)
        {
            content = ctx;
            this.size = size;
            this.type = type;
        }
        
        public byte[] EncodeStream()
        {
            var buf = new byte[size + 2 + 4 + 1];
            
            Buffer.BlockCopy(magic, 0, buf, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(size), 0, buf, 2, 4);
            buf[6] = type;
            Buffer.BlockCopy(content, 0, buf, 7, (int) size);

            return buf;
        }

        public static PacketFrame Consume(AsyncPipedMemoryStream stream)
        {
            var sizeBuf = new byte[4];
            
            if(stream.ReadByte() != 0xba || stream.ReadByte() != 0xbe) throw new DataException("Bad magic");
            
            stream.Read(sizeBuf, 0, 4);
            byte ptype = stream.ReadByte();
            
            
            var s = BitConverter.ToInt32(sizeBuf, 0);
            var ctxBuf = new byte[s];
            
            stream.Read(ctxBuf, 0, s);
            
            return new PacketFrame(ctxBuf, s, ptype);
        }
    }

    public class ServerPeerHandshakeFrame
    {
        public static readonly byte frameType = 0x01;

        public int peerID;

        public ServerPeerHandshakeFrame(int pid)
        {
            peerID = pid;
        }
        
        public ServerPeerHandshakeFrame(PacketFrame frame)
        {
            if(frame.type != frameType) throw new ArgumentException("Bad frame type");

            peerID = BitConverter.ToInt32(frame.content, 0);
        }
        
        public byte[] EncodeStream()
        {
            return BitConverter.GetBytes(peerID);
        }

        public PacketFrame EncodeFrame()
        {
            return new PacketFrame(EncodeStream(), frameType);
        }
    }
}