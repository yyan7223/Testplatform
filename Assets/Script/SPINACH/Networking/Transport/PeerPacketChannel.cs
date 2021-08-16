using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SPINACH.Networking;
using UnityEngine;

namespace SPINACH.Networking
{
    public class PeerPacketChannel
    {

        public PeerChannel channel;

        private Thread _consumingThread;
        // private bool _cosumeThreadRunning = false;
        private Queue<PacketFrame> _received = new Queue<PacketFrame>();

        private AsyncPipedMemoryStream stream;
        
        public PeerPacketChannel(PeerChannel pc)
        {
            channel = pc;
            stream = new AsyncPipedMemoryStream();
            channel.RegisterReceiveCallback((bytes, i) =>
            {
                stream.Write(bytes,0,i);
                
            });
            
            _consumingThread = new Thread(ConsumingReceiveFeed);
            _consumingThread.Start();
        }


        void ConsumingReceiveFeed()
        {
            while (true)
            {
                var p = PacketFrame.Consume(stream);
                
                lock (_received)
                {
                    _received.Enqueue(p);
                }
            }
            
            // _cosumeThreadRunning = false;
        }


        public PacketFrame DequeueFrame()
        {
            lock (_received)
            {
                if (_received.Count <= 0) return null;
                return _received.Dequeue();
            }
        }

        public void Send(byte[] data)
        {
            channel.Send(new PacketFrame(data, 0).EncodeStream());
        }

        public void SendPacket(PacketFrame frame)
        {
            channel.Send(frame.EncodeStream());
        }
    }
}