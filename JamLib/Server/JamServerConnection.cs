﻿using JamLib.Database;
using JamLib.Domain;
using JamLib.Domain.Serialization;
using JamLib.Packet;
using JamLib.Packet.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace JamLib.Server
{
    public class JamServerConnection: IDisposable
    {
        public readonly JamServer Server;

        public readonly TcpClient Client;
        private readonly SslStream stream;
        private bool alive;

        private readonly ConcurrentQueue<JamPacket> packetSendQueue = new ConcurrentQueue<JamPacket>();

        public Account Account { get; private set; }

        public string AppSigniture { get; private set; }
        public bool IsService { get; private set; }
        public readonly ISerializer Serializer;

        public JamServerConnection(TcpClient client, SslStream stream, JamServer server)
        {
            const int DISCONNECT_POLL_FREQUENCY = 500;

            Server = server;
            Serializer = server.Serializer;

            Client = client;
            this.stream = stream;
            alive = true;

            Task.Run(() => Listen());
            Task.Run(() => SendPacketsFromQueue());
            Task.Run(() => PollConnection(DISCONNECT_POLL_FREQUENCY));

            Server.OnClientConnected(new JamServer.ConnectionEventArgs() { ServerConnection = this, RemoteEndPoint = Client.Client.RemoteEndPoint });
        }
        
        public void Dispose()
        {
            if (alive)
            {
                alive = false;
                Server.OnClientDisconnected(new JamServer.IdentifiedConnectionEventArgs() { ServerConnection = this, RemoteEndPoint = Client.Client.RemoteEndPoint, Account = Account });

                stream.Close();

                if (Account != null)
                    Server.DeleteConnection(Account.AccountID);
            }
        }

        public void RespondToLogin(JamPacket loginPacket)
        {
            if (loginPacket.Header.DataType != LoginRequest.DATA_TYPE)
                return;

            LoginRequest request = new LoginRequest(loginPacket.Data, Serializer);

            LoginResponse response;
            try
            {
                Account = AccountFactory.Authenticate(request.Username, request.Password, Server.HashFactory);
                AppSigniture = request.AppSigniture;
                Server.OnClientIdentified(new JamServer.IdentifiedConnectionEventArgs() { ServerConnection = this, RemoteEndPoint = Client.Client.RemoteEndPoint, Account = Account });

                JamServerConnection existingConnection = Server.GetConnection(Account.AccountID);
                if (existingConnection != null)
                {
                    Server.OnClientConnectedElsewhere(new JamServer.IdentifiedConnectionEventArgs() { ServerConnection = this, RemoteEndPoint = existingConnection.Client.Client.RemoteEndPoint, Account = existingConnection.Account });
                    existingConnection.Dispose();
                }

                JamServerConnection appServiceConnection = Server.GetAppServiceConnection(AppSigniture);
                if (appServiceConnection == null && AppSigniture != Server.AppSigniture)
                {
                    response = new LoginResponse(LoginResponse.LoginResult.AppOffline, null, Guid.Empty, Serializer);
                    Server.OnClientOfflineAppRequest(new JamServer.IdentifiedConnectionEventArgs() { ServerConnection = this, RemoteEndPoint = Client.Client.RemoteEndPoint, Account = Account });
                }
                else
                {
                    Guid appServiceID = Guid.Empty;
                    if (appServiceConnection != null)
                        appServiceID = appServiceConnection.Account.AccountID;

                    response = new LoginResponse(LoginResponse.LoginResult.Good, Account, appServiceID, Serializer);
                    Server.AddConnection(this);
                }
            }
            catch (AccountFactory.InvalidUsernameException)
            {
                response = new LoginResponse(LoginResponse.LoginResult.BadUsername, null, Guid.Empty, Serializer);
                Server.OnClientInvalidUsername(new JamServer.ConnectionEventArgs() { ServerConnection = this, RemoteEndPoint = Client.Client.RemoteEndPoint });
            }
            catch (AccountFactory.InvalidAccessCodeException)
            {
                response = new LoginResponse(LoginResponse.LoginResult.BadPassword, null, Guid.Empty, Serializer);
                Server.OnClientInvalidPassword(new JamServer.ConnectionEventArgs() { ServerConnection = this, RemoteEndPoint = Client.Client.RemoteEndPoint });
            }
            catch (EntityException)
            {
                Server.Dispose();
                return;
            }

            JamPacket responsePacket = new JamPacket(Guid.Empty, Guid.Empty, LoginResponse.DATA_TYPE, response.GetBytes());
            Send(responsePacket);
        }

        public void RespondToPing(JamPacket pingPacket)
        {
            if (pingPacket.Header.DataType != PingRequest.DATA_TYPE)
                return;

            PingRequest request = new PingRequest(pingPacket.Data, Serializer);

            PingResponse response = new PingResponse(request.PingTimeUtc, DateTime.UtcNow, Serializer);

            JamPacket responsePacket = new JamPacket(Guid.Empty, Guid.Empty, PingResponse.DATA_TYPE, response.GetBytes());
            Send(responsePacket);
        }

        public void Logout()
        {
            Dispose();
        }

        public void Send(JamPacket packet)
        {
            packetSendQueue.Enqueue(packet);
        }

        private void SendPacketsFromQueue()
        {
            while (alive)
            {
                Thread.Sleep(50);

                if (packetSendQueue.Count > 0 && stream.CanWrite)
                {
                    try
                    {
                        if (packetSendQueue.TryDequeue(out JamPacket sendPacket))
                            sendPacket.Send(stream);
                    }
                    catch (IOException)
                    {
                        Dispose();
                    }
                }
            }
        }

        private void Listen()
        {
            while (alive)
            {
                JamPacket packet = JamPacket.Receive(stream);
                if (packet == null)
                {
                    Dispose();
                    return;
                }

                JamPacketRouter.Route(this, packet);
            }
        }

        private void PollConnection(int pollFrequency)
        {
            while (alive)
            {
                Thread.Sleep(pollFrequency);

                PingRequest pingRequest = new PingRequest(DateTime.UtcNow, Serializer);

                JamPacket pingPacket = new JamPacket(Guid.Empty, Guid.Empty, PingRequest.DATA_TYPE, pingRequest.GetBytes());
                Send(pingPacket);
            }
        }
    }
}
