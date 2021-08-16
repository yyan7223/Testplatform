using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SPINACH.Networking;
using UnityEngine;

namespace SPINACH.Networking
{
    public class TcpTransportListener : NetworkTransportListener
    {
        private Socket _socket;
        private Thread _acceptingThread;

        private bool _accepting = false;

        public TcpTransportListener(int port) : base(port)
        {
            var endpoint = new IPEndPoint(IPAddress.Any, port);
            _socket = new Socket(IPAddress.Any.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socket.Bind(endpoint);
        }

        public override void StartListening()
        {
            _accepting = true;
            _socket.Listen(100);
            _acceptingThread = new Thread(_Accepting);
            _acceptingThread.Start();
        }

        void _Accepting()
        {
            while (_accepting && _socket != null)
            {
                var peerSocket = _socket.Accept();
                _newPeerCallback?.Invoke(new TCPPeerChannel(peerSocket));
            }
        }

        public override void Stop()
        {
            _accepting = false;
            if(_acceptingThread != null)_acceptingThread.Abort();
            _acceptingThread = null;
        }

        public override void Dispose()
        {
            Stop();
            _socket.Close();
            _socket.Dispose();

            _socket = null;
        }


    }
}