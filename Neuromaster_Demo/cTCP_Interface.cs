﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using WindControlLib;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace Neuromaster_Demo_Library_Reduced__netx
{
    public class CTCPInterface
    {
        private TcpListener? tcpListener;
        private readonly int TCPPort = 23561;
        System.Threading.Timer? tmrCheckIncomingConns;

        /// <summary>
        /// List of connected clients
        /// </summary>
        List<ClientHandler> ClientHandlers = [];

        public delegate void StatusMessageEventHandler(string data, Color color);
        public event StatusMessageEventHandler? StatusMessage;
        protected virtual void OnStatusMessage(string data, Color color)
        {
            // Check if there are any subscribers to the event
            StatusMessage?.Invoke(data, color);
        }


        public CTCPInterface()
        {
        }

        public void Init()
        {
            // Erstellen eines neuen Listeners.
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());

            IPAddress IPtoUse = new (new byte[] { 127, 0, 0, 1 });

            tcpListener = new TcpListener(IPtoUse, TCPPort);
            OnStatusMessage("Listening on " + IPtoUse.ToString() + ": " + TCPPort.ToString(), Color.Green);
            tcpListener.Start();
            ClientHandlers = [];

            TimerCallback timerCallback = TmrCheckIncomingConnsCallbackMethod;
            tmrCheckIncomingConns ??= new System.Threading.Timer(timerCallback, null, 1000, 1000);
        }

        public void Write (CDataIn data)
        {
            foreach (ClientHandler handler in ClientHandlers)
            {
                handler.Write (data);
            }
        }

        public void Close ()
        {
           tmrCheckIncomingConns?.Dispose();
            if (ClientHandlers != null)
            {
                foreach (ClientHandler handler in ClientHandlers)
                {
                    handler.Close();
                }
            }
            tcpListener?.Stop();
        }

        private void TmrCheckIncomingConnsCallbackMethod(object? state)
        {
            if (tcpListener != null && tcpListener.Pending())
            {
                TcpClient tcpClient = tcpListener.AcceptTcpClient();

                // Ein neues Objekt erstellen, um diese Verbindung zu bearbeiten, create unique ID
                string g = CMyTools.RandomString(8);
                ClientHandler TCP_ClientHandler = new(tcpClient, g);
                ClientHandlers.Add(TCP_ClientHandler);

                OnStatusMessage("Connected to: " + tcpClient.Client.RemoteEndPoint + "(" + g + ")", Color.Orange);

                TCP_ClientHandler.MessageReady += new ClientHandler.MessageReadyEventHandler(TCP_ClientHandler_MessageReady);
                TCP_ClientHandler.ConnectionClosed += new ClientHandler.ConnectionClosedEventHandler(TCP_ClientHandler_ConnectionClosed);

                // Dieses Objekt in einem anderen Thread ausführen.
                Thread handlerThread = new (new ThreadStart(TCP_ClientHandler.Start))
                {
                    IsBackground = true
                };
                handlerThread.Start();
            }
        }

        void TCP_ClientHandler_MessageReady(object sender, string ID, CTCPDataPacket TCPPacket)
        {
        }

        /************************************************************/
        /******************** ConnectionClosed **************************/
        /************************************************************/
        void TCP_ClientHandler_ConnectionClosed(object sender, string ID)
        {
            int idx = GetClientHandler_index(ID);
            OnStatusMessage("IP: " + ClientHandlers[idx].RemoteAddress.ToString() + " closed" + " (" + ID + ")", Color.Red);

            //Remove from List
            if (idx >= 0)
                ClientHandlers.RemoveAt(idx);
        }

        private int GetClientHandler_index(string ID)
        {
            int ret = -1;
            for (int i = 0; i < ClientHandlers.Count; i++)
            {
                if (ClientHandlers[i].ID == ID)
                {
                    ret = i;
                    break;
                }
            }
            return ret;
        }
    }

    /// <summary>
    /// Handler, der mehrere TCP-Clients gleichzeitig bearbeiten kann. 
    /// </summary>
    public class ClientHandler(TcpClient client, string ID)
    {
        private readonly TcpClient? tcpClient = client;
        private NetworkStream? stream;

        private readonly string _ID = ID;
        public string ID
        {
            get { return _ID; }
        }


        private bool _Close = false;

        public void Close()
        {
            _Close = true;
        }

        private IPAddress _Address = IPAddress.None;
        public IPAddress Address
        {
            get { return _Address; }
            set { _Address = value; }
        }

        public IPAddress RemoteAddress
        {
            get
            {
                if (tcpClient != null && tcpClient.Client != null && tcpClient.Client.RemoteEndPoint != null)
                {
                    var ip = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
                    return IPAddress.Parse(ip);
                }
                return IPAddress.None;
            }
        }

        public int Port { get; set; }


        public void Connect()
        {
            if (tcpClient == null) return;
            try
            {
                tcpClient.Connect(Address, Port);
                if (tcpClient.Connected)
                    OnConnectionOpened();
            }
            catch (Exception ee)
            {
                CTCPErrorPacket TCPErrorPacket = new()
                {
                    ErrorString = ee.Message
                };
                OnErrorMessageReady(TCPErrorPacket);
            }
        }



        public delegate void MessageReadyEventHandler(object sender, string ID, CTCPDataPacket TCPPacket);
        public event MessageReadyEventHandler? MessageReady;
        protected virtual void OnMessageReady(CTCPDataPacket TCPPacket)
        {
            MessageReady?.Invoke(this, _ID, TCPPacket);
        }

        public delegate void ErrorMessageReadyEventHandler(object sender, string ID, CTCPErrorPacket TCPErrorPacket);
        public event ErrorMessageReadyEventHandler? ErrorMessageReady;
        protected virtual void OnErrorMessageReady(CTCPErrorPacket TCPErrorPacket)
        {
            ErrorMessageReady?.Invoke(this, _ID, TCPErrorPacket);
        }

        public delegate void ConnectionClosedEventHandler(object sender, string ID);
        public event ConnectionClosedEventHandler? ConnectionClosed;
        protected virtual void OnConnectionClosed()
        {
            ConnectionClosed?.Invoke(this, _ID);
        }

        public delegate void ConnectionOpenedEventHandler(object sender, string ID);
        public event ConnectionOpenedEventHandler? ConnectionOpened;
        protected virtual void OnConnectionOpened()
        {
            ConnectionOpened?.Invoke(this, _ID);
        }

        public void Write(CDataIn DataIn)
        {
            byte[] bu = CDataInJSONSerializer.Serialize(DataIn);
            byte[] lengthBytes = BitConverter.GetBytes(bu.Length);
            stream?.Write(lengthBytes, 0, lengthBytes.Length);
            stream?.Write(bu, 0, bu.Length);
        }

        public void Start()
        {
            // Den Netzwerk-Stream abrufen.
            if (tcpClient == null)
            {
                throw new Exception("tcpClient mustbe created before calling Start()");
            }
            stream = tcpClient.GetStream();
            stream.Flush();

            CTCPPacketHeader TCPPacketHeader = new ();
            byte[] barr = new byte[1];
            int bytesread;
            bool ReadHeader = true;

            while (!_Close)
            {
                //Read Data
                try
                {
                    if (ReadHeader)
                    {
                        //Read Header, size is known
                        barr = new byte[TCPPacketHeader.PacketSize];
                        bytesread = stream.Read(barr, 0, barr.Length);
                    }
                    else
                    {
                        //Read Messaged, size is known from previous Header Packet
                        barr = new byte[TCPPacketHeader.LengthOfFollowingPacket];
                        bytesread = stream.Read(barr, 0, barr.Length);
                    }
                    if (bytesread == 0)
                    {
                        //the client has disconnected from the server
                        break;
                    }
                }
                catch (Exception ee)
                {
                    //Socket Error
                    if (ee.Message.Contains("WSACancelBlockingCall"))
                    {
                    }
                    else
                        break;
                }

                //One packet is in
                if (ReadHeader)
                {
                    //Header Packet is in

                    //Read Message next
                    ReadHeader = false;
                    try
                    {
                        TCPPacketHeader.UpdateFrom_ByteArray(barr, 0, 0);
                    }
                    catch
                    {
                        //failed, was not a header packet
                        ReadHeader = true;
                    }
                }
                else
                {
                    //Message packet is in
                    try
                    {
                        switch (TCPPacketHeader.TCPPacketType)
                        {
                            case CTCPPacketHeader.PacketType_Data:
                                {
                                    CTCPDataPacket TCPDataPacket = new();
                                    //TCPDataPacket.UpdateFrom_ByteArray(barr, 0, TCPPacketHeader.LengthOfFollowingPacket);
                                    TCPDataPacket.Update(TCPPacketHeader);
                                    OnMessageReady(TCPDataPacket);
                                    break;
                                }

                            case CTCPPacketHeader.PacketType_Error:
                                {
                                    CTCPErrorPacket TCPErrorPacket = new();
                                    CTCPErrorPacket.UpdateDataFromByteArray(barr, 0);
                                    TCPErrorPacket.Update(TCPPacketHeader);
                                    OnErrorMessageReady(TCPErrorPacket);
                                    break;
                                }
                        }
                    }
                    catch (Exception ee)
                    {
                        Debug.WriteLine(ee.Message);
                    }
                    //Read Headder next
                    ReadHeader = true;
                }
            }
            OnConnectionClosed();
            // Den Verbindungs-Socket schließen.
            tcpClient.Close();
        }
    }
}
