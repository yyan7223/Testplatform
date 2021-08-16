using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;


namespace SPINACH.Networking
{
    public class AsyncPipedMemoryStream
    {
        private byte[] _streamBuffer = new byte[0];
        private int _streamBufferDataLength => _streamBuffer.Length - _streamBufferCursor;
        private Queue<byte[]> _streamBufferQueue = new Queue<byte[]>();
        private int _streamBufferCursor = 0;
        

        private Mutex _sbMutex = new Mutex();
        private AutoResetEvent _newDataAvailable = new AutoResetEvent(false);
        
        
        /// <summary>
        /// Block until length amount of data is available and written to the buffer.
        /// allows thread-safe writes to the buffer while reading thread is blocked.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        public void Read(byte[] buffer, int offset, int length)
        {
            if (offset + length > buffer.Length)
            {
                throw new IndexOutOfRangeException("FUCK OFF!");
            }
            
            int cursor = 0;
            while (cursor < length)
            {
                _sbMutex.WaitOne();
                
                if (_streamBufferDataLength <= 0)
                {
                    if (_streamBufferQueue.Count > 0)
                    {
                        _streamBuffer = _streamBufferQueue.Dequeue();
                        _streamBufferCursor = 0;
                    }
                    else
                    {
                        
                        _sbMutex.ReleaseMutex();
                        _newDataAvailable.WaitOne();
                        continue;
                    }
                    
                }

                var l2R = Math.Min(_streamBufferDataLength, length - cursor);
                
                Buffer.BlockCopy(_streamBuffer,_streamBufferCursor + offset, buffer, cursor, l2R);
                cursor += l2R;
                _streamBufferCursor += l2R;
                
                _sbMutex.ReleaseMutex();
            }
        }

        public byte ReadByte()
        {
            var oneByte = new byte[1];
            Read(oneByte, 0, 1);
            return oneByte[0];
        }
        
        public void Write(byte[] buffer, int offset, int length)
        {
            _sbMutex.WaitOne();
            var tb = new byte[length];
            Buffer.BlockCopy(buffer, offset, tb, 0, length);
            _streamBufferQueue.Enqueue(tb);
            _newDataAvailable.Set();
            _sbMutex.ReleaseMutex();
        }
    }
}