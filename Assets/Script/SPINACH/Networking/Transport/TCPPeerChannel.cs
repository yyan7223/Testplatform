using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace SPINACH.Networking
{
    public class TCPPeerChannel : PeerChannel
    {

        private Socket _socket;
        
        private Thread _receivingThread;

        private Queue<byte[]> _outQueue = new Queue<byte[]>();
        private bool _sendAsyncStarted = false;
        private bool _receiveAsyncStarted = false;

        private byte[] _receiveBuf = new byte[512];
        
        public TCPPeerChannel(Socket peerSocket)
        {
            _socket = peerSocket;
            connected = true; //a tcp socket is connected before constructing this object.
        }

        public TCPPeerChannel(string ip, int port)
        {
            var ipaddr = IPAddress.Parse(ip);
            var endpoint = new IPEndPoint(ipaddr, port);
            var tcpsocket = new Socket(ipaddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            tcpsocket.Connect(endpoint);

            _socket = tcpsocket;
        }

        void AsyncSendCallback(IAsyncResult ar)
        {
            _socket.EndSend(ar);
            lock (_outQueue)
            {
                if (_outQueue.Count > 0)
                {
                    var bufref = _outQueue.Dequeue();
                    _socket.BeginSend(bufref, 0, bufref.Length, SocketFlags.None, AsyncSendCallback, _socket);
                    
                    return;
                }

                _sendAsyncStarted = false;
            }
        }
        
        public override void Send(byte[] data)
        {
            if (_sendAsyncStarted)
            {
                var buf = new byte[data.Length];
                Buffer.BlockCopy(data,0,buf,0,data.Length);
                lock (_outQueue)
                {
                    _outQueue.Enqueue(buf);
                }
                
                return;
            }

            _sendAsyncStarted = true;
            _socket.BeginSend(data, 0, data.Length, SocketFlags.None, AsyncSendCallback, _socket);
        }

        void AsyncProcessReceived(IAsyncResult ar)
        {
            if(wasted) return;
            
            var size = _socket.EndReceive(ar);
            
            
            _receiveCallback?.Invoke(_receiveBuf, size);

            if (size == 0)
            {
                Waste();
                return;
            }

            _socket.BeginReceive(_receiveBuf, 0, _receiveBuf.Length, 
                SocketFlags.None, AsyncProcessReceived, _socket);
        }
        
        public override void RegisterReceiveCallback(Action<byte[], int> cb)
        {
            _receiveCallback = cb;
            if (_receiveAsyncStarted) return;
            
           
            
            _socket.BeginReceive(_receiveBuf, 0, _receiveBuf.Length, 
                SocketFlags.None, AsyncProcessReceived, _socket);

            _receiveAsyncStarted = true;
        }
        
        public override void Waste()
        {
            _socket.Close();
            _socket.Dispose();
            wasted = true;
        }
    }

}