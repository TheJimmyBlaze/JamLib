﻿using JamLib.Packet.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JamLib.Packet
{
    public class JamPacket
    {
        private struct ReceiveState
        {
            public SslStream Stream { get; set; }
            public JamPacket Packet { get; set; }
            public byte[] HeaderBuffer { get; set; }

            public int BytesRead { get; set; }
        }

        private AutoResetEvent sendCompleted = new AutoResetEvent(false);
        private static AutoResetEvent receiveCompleted = new AutoResetEvent(false);

        public JamPacketHeader Header { get; set; }
        public byte[] Data { get; set; }
        public bool ContainsData { get; set; } = false;

        public JamPacket() { }

        public JamPacket(Guid recipient, Guid sender, int dataType, byte[] data)
        {
            Header = new JamPacketHeader()
            {
                Receipient = recipient,
                Sender = sender,
                SendTimeUtc = DateTime.UtcNow,
                DataType = dataType,
                DataLength = data.Length
            };

            Data = data;
            ContainsData = true;
        }

        public override string ToString()
        {
            return Encoding.ASCII.GetString(Data);
        }

        public int Send(SslStream stream)
        {
            byte[] headerBytes = Header.GetBytes();
            byte[] sendBytes = new byte[headerBytes.Length + Data.Length];

            headerBytes.CopyTo(sendBytes, 0);
            Data.CopyTo(sendBytes, headerBytes.Length);

            stream.BeginWrite(sendBytes, 0, sendBytes.Length, SendCallback, stream);
            sendCompleted.WaitOne();

            return sendBytes.Length;
        }

        private void SendCallback(IAsyncResult result)
        {
            SslStream stream = result.AsyncState as SslStream;
            stream.EndWrite(result);
            stream.Flush();

            sendCompleted.Set();
        }

        public static JamPacket Receive(SslStream stream)
        {
            ReceiveState state = new ReceiveState()
            {
                Stream = stream,
                Packet = new JamPacket(),
                HeaderBuffer = new byte[Marshal.SizeOf(typeof(JamPacketHeader))]
            };
            stream.BeginRead(state.HeaderBuffer, 0, state.HeaderBuffer.Length, ReceiveHeaderCallback, state);
            receiveCompleted.WaitOne();

            state.Packet.ContainsData = true;
            return state.Packet;
        }

        private static void ReceiveHeaderCallback(IAsyncResult result)
        {
            try
            {
                ReceiveState state = (ReceiveState)result.AsyncState;
                state.BytesRead += state.Stream.EndRead(result);
                int bytesRequired = Marshal.SizeOf(state.Packet.Header.GetType());

                if (state.BytesRead != bytesRequired)
                {
                    state.Stream.BeginRead(state.HeaderBuffer, state.BytesRead, state.HeaderBuffer.Length - state.BytesRead, ReceiveHeaderCallback, state);
                    return;
                }

                state.Packet.Header = new JamPacketHeader(state.HeaderBuffer);
                state.Packet.Data = new byte[state.Packet.Header.DataLength];
                state.BytesRead = 0;

                state.Stream.BeginRead(state.Packet.Data, 0, state.Packet.Header.DataLength, ReceiveDataCallback, state);
            }
            catch (IOException) { }
        }

        private static void ReceiveDataCallback(IAsyncResult result)
        {
            ReceiveState state = (ReceiveState)result.AsyncState;
            state.BytesRead += state.Stream.EndRead(result);
            int bytesRequired = state.Packet.Header.DataLength;

            if (state.BytesRead != bytesRequired)
            {
                state.Stream.BeginRead(state.Packet.Data, state.BytesRead, state.Packet.Header.DataLength - state.BytesRead, ReceiveDataCallback, state);
                return;
            }

            receiveCompleted.Set();
        }
    }
}
