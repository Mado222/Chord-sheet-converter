using BMTCommunicationLib;
using WindControlLib;

namespace FeedbackDataLib
{
    /// <summary>
    /// Packs all C8KanalReceiverV2 with differnt Connections together
    /// </summary>
    public class C8KanalReceiverV2 : IDisposable
    {
        #region declarations
        /// <summary>
        /// Connection type enumerator
        /// </summary>
        public enum EnumNeuromasterConnectionType
        {
            NoConnection = 0x00,
            XBeeConnection = 0x01,
            RS232Connection = 0x02,
            SDCardConnection = 0x03
        }
        public string LastErrorString { get; private set; } = "";

        /// <summary>
        /// Represents the state Connect returns
        /// </summary>
        public enum EnumConnectionResult
        {
            NoConnection = 0x00,
            Error_during_Port_scan = 0x01,
            No_Active_Neurolink = 0x02,
            More_than_one_Neurolink_detected = 0x03,
            Connected_via_XBee = 0x04,
            Connected_via_USBCable = 0x05,
            Connected_via_RS232 = 0x06,
            Error_during_XBee_connection = 0x07,
            Error_during_USBcable_connection = 0x08,
            Error_read_ErrorString = 0x09,
            Connected_via_SDCard = 0x0A,
            Error_read_SDCard = 0x0B
        }

        private const CFTDI_D2xx.FTDI_Types Accepted_FTDI_Single_Device = CFTDI_D2xx.FTDI_Types.FT_DEVICE_232R;
        private const CFTDI_D2xx.FTDI_Types Accepted_FTDI_Dual_Device = CFTDI_D2xx.FTDI_Types.FT_DEVICE_2232;

        #endregion

        #region properties
        private C8KanalReceiverV2_RS232? _8KanalReceiverV2_RS232;
        private C8KanalReceiverV2_XBee? _8KanalReceiverV2_XBee;
        //private C8KanalReceiverV2_SDCard? _8KanalReceiverV2_SDCard;
        //private C8KanalReceiverV2_CommBase? _8KanalReceiverV2_SDCard;

        /// <summary>
        /// FTDI driver class
        /// </summary>
        public CFTDI_D2xx? FTDI_D2xx;

        //public CSDCardConnection? SDCardConnection;

        /// <summary>
        /// Signal Strength [%]
        /// </summary>
        /// <value>
        /// 0..100%
        /// </value>
        public byte RSSI_percent
        {
            get
            {
                if (_8KanalReceiverV2_XBee != null)
                {
                    return _8KanalReceiverV2_XBee.XBeeConnection.RSSI_percent;

                }
                else return 0;
            }
        }
        /// <summary>
        /// Gets the neurolink serial number.
        /// </summary>
        /// <value>
        /// The neurolink serial number.
        /// </value>
        public string NeurolinkSerialNumber { get; private set; } = "";

        /// <summary>
        /// FTDI Product ID (PID)
        /// </summary>
        public string PID
        {
            get
            {
                if (ComPortInfo != null)
                {
                    CVID_PID pv = new()
                    {
                        VID_PID = ComPortInfo.VID_PID
                    };
                    return pv.PID;
                }
                else
                {
                    if (FTDI_D2xx == null) return "";
                    return FTDI_D2xx.PID(FTDI_D2xx.IndexOfDeviceToOpen);
                }
            }
        }

        /// <summary>
        /// FTDI Vendor ID (VID)
        /// </summary>
        public string VID
        {
            get
            {
                if (ComPortInfo != null)
                {
                    CVID_PID pv = new()
                    {
                        VID_PID = ComPortInfo.VID_PID
                    };
                    return pv.VID;
                }
                else
                {
                    if (FTDI_D2xx == null) return "";
                    return FTDI_D2xx.VID(FTDI_D2xx.IndexOfDeviceToOpen);
                }
            }
        }

        /// <summary>
        /// VID & PID
        /// </summary>
        /// <value>
        /// "VID_0403&PID_6001"
        /// </value>
        public string VID_PID
        {
            get
            {
                if (ComPortInfo != null)
                    return ComPortInfo.VID_PID;
                else
                {
                    CVID_PID pv = new()
                    {
                        PID = PID,
                        VID = VID
                    };
                    return pv.VID_PID;
                }
            }
        }

        private EnumNeuromasterConnectionType ConnectionType = EnumNeuromasterConnectionType.NoConnection;

        /// <summary>
        /// Gets the name of the Com port FINALLY used (my be changed during the pairing process)
        /// </summary>
        public string PortName
        {
            get
            {
                switch (ConnectionType)
                {
                    case EnumNeuromasterConnectionType.XBeeConnection:
                        {
                            if (_8KanalReceiverV2_XBee is null) return "";
                            return _8KanalReceiverV2_XBee.XBeeConnection.PortName;
                        }
                    case EnumNeuromasterConnectionType.RS232Connection:
                        {
                            if (_8KanalReceiverV2_RS232 is null) return "";
                            return _8KanalReceiverV2_RS232.SerialPort.PortName;
                        }
                    default:
                        {
                            return "";
                        }
                }
            }
        }

        /// <summary>
        /// THE connection to the Neuromaster
        /// </summary>
        public C8KanalReceiverV2_CommBase? Connection
        {
            get
            {
                switch (ConnectionType)
                {
                    case EnumNeuromasterConnectionType.XBeeConnection:
                        {
                            return _8KanalReceiverV2_XBee;
                        }
                    case EnumNeuromasterConnectionType.RS232Connection:
                        {
                            return _8KanalReceiverV2_RS232;
                        }
                    //case EnumNeuromasterConnectionType.SDCardConnection:
                    //    {
                    //        return _8KanalReceiverV2_SDCard;
                    //    }
                    default:
                        {
                            return null;
                        }
                }
            }
        }


        private EnumConnectionStatus _ConnectionStatus = EnumConnectionStatus.Not_Connected;
        /// <summary>
        /// Connectionstatus
        /// </summary>
        public EnumConnectionStatus ConnectionStatus
        {
            get
            {
                if (_ConnectionStatus == EnumConnectionStatus.USB_disconnected ||
                    _ConnectionStatus == EnumConnectionStatus.USB_reconnected)
                    return _ConnectionStatus;
                else
                {
                    if (Connection is null) return EnumConnectionStatus.Not_Connected;
                    return Connection.GetConnectionStatus();
                }
            }
        }



        /// <summary>
        /// Enables / disables DataReadyEvent
        /// </summary>
        /// <value>
        /// <c>true</c> enabled; otherwise, <c>false</c>.
        /// </value>
        public bool EnableDataReadyEvent
        {
            get
            {
                if (Connection?.RS232Receiver is null) return false;
                return Connection.RS232Receiver.EnableDataReadyEvent;
            }
            set
            {

                if (Connection?.RS232Receiver is not null)
                    Connection.RS232Receiver.EnableDataReadyEvent = value;
            }
        }

        public CComPortInfo? ComPortInfo { get; private set; } = null;
        #endregion

        #region events

        public delegate void DeviceDisconnectedEventHandler(string PID_VID);
        /// <summary>
        /// Occurs when Neurolink is disconnected from USB
        /// </summary>
        public event DeviceDisconnectedEventHandler? DeviceDisconnected;
        /// <summary>
        /// Occurs when Neurolink is disconnected from USB
        /// </summary>
        /// <param name="PID_VID">PID_VID</param>
        protected virtual void OnDeviceDisconnected(string PID_VID)
        {
            DeviceDisconnected?.Invoke(PID_VID);
        }

        public delegate void DeviceConnectedEventHandler(EnumConnectionResult ConnectionResult);
        /// <summary>
        /// /// Occurs when Neurolink is re-connected to USB
        /// </summary>
        public event DeviceConnectedEventHandler? DeviceConnected;
        protected virtual void OnDeviceConnected(EnumConnectionResult ConnectionResult)
        {
            DeviceConnected?.Invoke(ConnectionResult);
        }

        #endregion

        /// <summary>
        /// Finalizes an instance of the <see cref="C8KanalReceiverV2"/> class.
        /// </summary>
        ~C8KanalReceiverV2()
        {
            //Make sure that USB monitoring and all other stuff is down
            Dispose();
        }

        //public EnumConnectionResult Init_via_SDCard()
        //{
        //    /*
        //    SDCardConnection ??= new CSDCardConnection();

        //    ConnectionType = EnumNeuromasterConnectionType.SDCardConnection;
        //    _8KanalReceiverV2_SDCard = new C8KanalReceiverV2_SDCard(SDCardConnection);*/
        //    return EnumConnectionResult.Connected_via_SDCard;
        //}


        /// <summary>
        /// Init function using D2xx driver
        /// Checks firsr cable connection: if a device is present, this connection is selected
        /// otherwiser
        /// XBee is selcted (if present) BUT NOT tested!!!
        /// Calling routine must take care of connection!!!
        /// </summary>
        /// <param name="DescriptionContains">Might search for other names than "Neurolink" too</param>
        /// <returns></returns>
        public EnumConnectionResult Init_via_D2XX(List<string>? DescriptionContains = null)
        {
            Close_All();
            List<int> idxNeurolinkDevices = [];
            EnumConnectionResult ret = EnumConnectionResult.No_Active_Neurolink;

            FTDI_D2xx ??= new CFTDI_D2xx();
            int numDevices = FTDI_D2xx.CheckForConnectedDevices();  //fast

            List<string> descriptionContains = [];

            if (DescriptionContains != null)
            {
                descriptionContains.AddRange(DescriptionContains);
            }
            else
            {
                descriptionContains.Add("Neurolink");
                descriptionContains.Add("Serial Converter");
            }

            if (numDevices != 0)
            {
                foreach (string _descriptionContains in descriptionContains)
                {
                    for (int i = 0; i < numDevices; i++)
                    {
                        if (((FTDI_D2xx.Type(i) == Accepted_FTDI_Dual_Device) && FTDI_D2xx.Description(i).Contains(_descriptionContains)) ||
                            ((FTDI_D2xx.Type(i) == Accepted_FTDI_Single_Device) && FTDI_D2xx.Description(i).Contains(_descriptionContains)))
                        {
                            idxNeurolinkDevices.Add(i);
                        }
                    }
                }

                if (idxNeurolinkDevices.Count > 2)
                {
                    ret = EnumConnectionResult.More_than_one_Neurolink_detected;
                }

                if (idxNeurolinkDevices.Count == 2)
                {
                    //Find out whether XBEE connection or RS232 connection to Neuromaster
                    //Differntiation between the 2 devices

                    int idxXBEeeConnection = -1;
                    int idxRS232Connection = -1;

                    for (int i = 0; i < idxNeurolinkDevices.Count; i++)
                    {
                        NeurolinkSerialNumber = FTDI_D2xx.SerialNumber(idxNeurolinkDevices[i]);
                        if (NeurolinkSerialNumber[^1] == 'A')
                        {
                            //XBee Device
                            idxXBEeeConnection = idxNeurolinkDevices[i];
                        }
                        if (NeurolinkSerialNumber[^1] == 'B')
                        {
                            //RS232Connection
                            idxRS232Connection = idxNeurolinkDevices[i];
                        }
                    }
                    NeurolinkSerialNumber = NeurolinkSerialNumber.Remove(NeurolinkSerialNumber.Length - 1);

                    //First look for RS232 because it is faster
                    //Open the related Port
                    ConnectionType = EnumNeuromasterConnectionType.RS232Connection;
                    _8KanalReceiverV2_RS232 ??= new C8KanalReceiverV2_RS232(FTDI_D2xx);

                    //FTDI_D2xx.BaudRate = C8KanalReceiverV2_RS232.RS232_Neurolink_BaudRate;
                    FTDI_D2xx.BaudRate = _8KanalReceiverV2_RS232.RS232_Neurolink_BaudRate;     //4.2.2016
                    FTDI_D2xx.IndexOfDeviceToOpen = idxRS232Connection;

                    if (_8KanalReceiverV2_RS232 is not null && _8KanalReceiverV2_RS232.RS232Receiver is not null)
                    {
                        if (_8KanalReceiverV2_RS232.RS232Receiver.Check4Neuromaster_RS232())   //fast
                        {
                            //RS232 Connection is OK
                            ConnectionType = EnumNeuromasterConnectionType.RS232Connection;

                            //Make sure that XBEE is in Sleep
                            CFTDI_D2xx FTDI_D2xx_temp = new()
                            {
                                IndexOfDeviceToOpen = idxXBEeeConnection
                            };


                            _8KanalReceiverV2_XBee ??= new C8KanalReceiverV2_XBee(FTDI_D2xx_temp);

                            if (_8KanalReceiverV2_XBee is not null && _8KanalReceiverV2_XBee.RS232Receiver is not null)
                            {
                                _8KanalReceiverV2_XBee.RS232Receiver.Send_to_Sleep();
                                _8KanalReceiverV2_XBee.Close();
                            }
                            FTDI_D2xx_temp.Close();
                            FTDI_D2xx_temp.Dispose();
                            ret = EnumConnectionResult.Connected_via_USBCable;
                        }
                        else
                        {
                            //Go for XBEE
                            //It must be XBee ... leave Connection to the calling routine
                            _8KanalReceiverV2_RS232.Close();
                            FTDI_D2xx.IndexOfDeviceToOpen = idxXBEeeConnection;
                            ConnectionType = EnumNeuromasterConnectionType.XBeeConnection;
                            _8KanalReceiverV2_XBee = new C8KanalReceiverV2_XBee(FTDI_D2xx);
                            ret = EnumConnectionResult.Connected_via_XBee;
                        }
                    }
                }


                if (idxNeurolinkDevices.Count == 1)
                {
                    //We assume that it is an old USB/XBee dongle
                    FTDI_D2xx.IndexOfDeviceToOpen = 0;
                    ConnectionType = EnumNeuromasterConnectionType.XBeeConnection;
                    _8KanalReceiverV2_XBee = new C8KanalReceiverV2_XBee(FTDI_D2xx);
                    ret = EnumConnectionResult.Connected_via_XBee;
                }

                //Go for old part
                //return Init_via_USB_or_XBEE();
            }
            return ret;
        }

        /// <summary>
        /// Init function using vitual COM Ports
        /// </summary>
        /// <returns></returns>
        public EnumConnectionResult Init_via_VirtualCom()
        {
            EnumConnectionResult ret = EnumConnectionResult.No_Active_Neurolink;
            FTDI_D2xx = new CFTDI_D2xx();
            int numDevices = FTDI_D2xx.CheckForConnectedDevices();

            string com_XBEeeConnection = "";
            string com_RS232Connection = "";

            try
            {
                if (numDevices == 1)
                {
                    //Möglicherweise ist ein USB- ZigBee Dongle dran
                    if (FTDI_D2xx.Type(0) == Accepted_FTDI_Single_Device)
                    {
                        //XBee Device
                        com_XBEeeConnection = FTDI_D2xx.RelatedCom(0).ToString();
                        ConnectionType = EnumNeuromasterConnectionType.XBeeConnection;
                        _8KanalReceiverV2_XBee = new C8KanalReceiverV2_XBee(com_XBEeeConnection);
                        ret = EnumConnectionResult.Connected_via_XBee;
                    }
                }

                if (numDevices == 2)
                {
                    if (FTDI_D2xx.Type(0) == Accepted_FTDI_Dual_Device)
                    {
                        for (int i = 0; i < numDevices; i++)
                        {
                            string s = FTDI_D2xx.SerialNumber(i);
                            if (s[^1] == 'A')
                            {
                                //XBee Device
                                com_XBEeeConnection = FTDI_D2xx.RelatedCom(i).ToString();
                            }
                            if (s[^1] == 'B')
                            {
                                //RS232Connection
                                com_RS232Connection = FTDI_D2xx.RelatedCom(i).ToString();
                            }
                        }

                        //First look for RS232 because it is faster
                        if (com_RS232Connection != "")
                        {
                            ConnectionType = EnumNeuromasterConnectionType.RS232Connection;
                            _8KanalReceiverV2_RS232 = new C8KanalReceiverV2_RS232(com_RS232Connection);

                            if (_8KanalReceiverV2_RS232 != null && _8KanalReceiverV2_RS232.RS232Receiver != null &&
                                _8KanalReceiverV2_RS232.RS232Receiver.Check4Neuromaster_RS232())
                            {
                                //RS232 Connection is OK
                                ConnectionType = EnumNeuromasterConnectionType.RS232Connection;
                                ret = EnumConnectionResult.Connected_via_USBCable;

                                //Make sure that XBEE is in Sleep
                                CFTDI_D2xx FTDI_D2xx_temp = new();
                                _8KanalReceiverV2_XBee = new C8KanalReceiverV2_XBee(com_XBEeeConnection);
                                if (_8KanalReceiverV2_XBee != null && _8KanalReceiverV2_XBee.RS232Receiver != null)
                                {
                                    _8KanalReceiverV2_XBee.RS232Receiver.Send_to_Sleep();
                                    _8KanalReceiverV2_XBee.Close();
                                }
                            }
                        }

                        if ((com_XBEeeConnection != "") && (ret != EnumConnectionResult.Connected_via_USBCable))
                        {
                            //Go for XBEE
                            //It must be XBee ... leave Connection to the calling routine
                            _8KanalReceiverV2_RS232?.Close();
                            ConnectionType = EnumNeuromasterConnectionType.XBeeConnection;
                            _8KanalReceiverV2_XBee = new C8KanalReceiverV2_XBee(com_XBEeeConnection);
                            ret = EnumConnectionResult.Connected_via_XBee;
                        }
                    }
                }
                if (numDevices > 2)
                    ret = EnumConnectionResult.More_than_one_Neurolink_detected;
            }
            catch (Exception)
            { }
            finally
            {
                FTDI_D2xx?.Close(); //We dont need it any more ... since Com Ports are used later on
            }

            return ret;
        }

        /// <summary>
        /// This is the "old" Init function which connects via USB or XBEE
        /// </summary>
        /// <returns></returns>
        public EnumConnectionResult Init_via_USB_or_XBEE()
        {
            List<string>? coms_XBee = CGetComPorts.GetActiveComPorts(C8KanalReceiverV2_XBee.DriverSearchName);
            List<string> coms_USBCable = [];
            _ConnectionStatus = EnumConnectionStatus.Not_Connected; //Egal auf welchen Wert nur nicht USB_xxx

            if ((coms_XBee == null) && (coms_USBCable == null))
            {
                return EnumConnectionResult.Error_during_Port_scan;
            }

            if (coms_XBee != null && (coms_XBee.Count == 0) && (coms_USBCable.Count == 0))
            {
                //Lets try another name
                coms_USBCable = CGetComPorts.GetActiveComPorts("USB Serial Port");
                if ((coms_XBee.Count == 0) && (coms_USBCable.Count == 0))
                    return EnumConnectionResult.No_Active_Neurolink;
            }

            if (coms_XBee != null && (coms_XBee.Count > 1) || (coms_USBCable.Count > 1))
            {
                return EnumConnectionResult.More_than_one_Neurolink_detected;
            }

            if (coms_USBCable.Count == 1)
            {
                //Connection via USBCable possible
                _8KanalReceiverV2_RS232 = new C8KanalReceiverV2_RS232(coms_USBCable[0]);
                ConnectionType = EnumNeuromasterConnectionType.RS232Connection;

                //Get more information about the COM Port
                List<CComPortInfo> ci = CGetComPorts.GetComPortInfo(C8KanalReceiverV2_RS232.DriverSearchName);
                if ((ci != null) && (ci.Count >= 1))
                {
                    foreach (CComPortInfo c in ci)
                    {
                        if (c.ComName == coms_USBCable[0])
                        {
                            ComPortInfo = c;
                            break;
                        }
                    }
                }
                return EnumConnectionResult.Connected_via_USBCable;
            }
            else
            {
                //Connection via XBee possible
                if (coms_XBee != null && coms_XBee.Count > 0)
                {
                    _8KanalReceiverV2_XBee = new C8KanalReceiverV2_XBee(coms_XBee[0]);
                    ConnectionType = EnumNeuromasterConnectionType.XBeeConnection;

                    //Get more information about the COM Port
                    List<CComPortInfo> ci = CGetComPorts.GetComPortInfo(C8KanalReceiverV2_XBee.DriverSearchName);
                    if ((ci != null) && (ci.Count >= 1))
                    {
                        foreach (CComPortInfo c in ci)
                        {
                            if (c.ComName == coms_XBee[0])
                            {
                                ComPortInfo = c;
                                break;
                            }
                        }
                    }
                    return EnumConnectionResult.Connected_via_XBee;
                }
                return EnumConnectionResult.Error_during_XBee_connection;
            }
        }

        /// <summary>
        /// Connect to Neuromaster
        /// </summary>
        /// <remarks>
        /// Scans for XBee and USB cable connections
        /// In case both are present - cable connection is choosen
        /// Error message, if more than one USBcable connection or XBee connection are present
        /// </remarks>
        public EnumConnectionResult Connect()
        {
            _ConnectionStatus = EnumConnectionStatus.Not_Connected; //Egal auf welchen Wert nur nicht USB_xxx
            if (ConnectionType == EnumNeuromasterConnectionType.XBeeConnection)
            {
                //Connect via XBee
                if (_8KanalReceiverV2_XBee != null)
                {
                    bool XBeePairingScucceded;
                    try
                    {
                        XBeePairingScucceded = _8KanalReceiverV2_XBee.CheckConnection_Start_trytoConnectWorker();
                    }
                    catch
                    {
                        return EnumConnectionResult.Error_during_XBee_connection;
                    }

                    if (XBeePairingScucceded)
                    {
                        StartUSBMonitoring();
                        return EnumConnectionResult.Connected_via_XBee;
                    }
                    else
                    {
                        LastErrorString = _8KanalReceiverV2_XBee.LastXBeeErrorString;
                        return EnumConnectionResult.Error_read_ErrorString;
                    }
                }
                return EnumConnectionResult.Error_during_XBee_connection;
            }

            else if (ConnectionType == EnumNeuromasterConnectionType.RS232Connection)
            {
                if (_8KanalReceiverV2_RS232 != null)
                {
                    //Connect via USBCable
                    bool RS232PairingScucceded;
                    try
                    {
                        RS232PairingScucceded = _8KanalReceiverV2_RS232.CheckConnection_Start_trytoConnectWorker();
                        //RS232PairingScucceded = true;
                    }
                    catch
                    {
                        return EnumConnectionResult.Error_during_USBcable_connection;
                    }
                    if (RS232PairingScucceded)
                    {
                        StartUSBMonitoring();
                        return EnumConnectionResult.Connected_via_USBCable;
                    }
                }
                return EnumConnectionResult.Error_during_USBcable_connection;
            }
            //else if (ConnectionType == EnumNeuromasterConnectionType.SDCardConnection)
            //{
            //    if (_8KanalReceiverV2_SDCard != null)
            //    {
            //        _8KanalReceiverV2_SDCard.CheckConnection_Start_trytoConnectWorker();
            //        return EnumConnectionResult.Connected_via_SDCard;
            //    }
            //    return EnumConnectionResult.Error_read_SDCard;
            //}
            return EnumConnectionResult.NoConnection;
        }


        #region USBMonitoring
        private USBMonitor? usbm = null;
        private string VID_PID_opened = ""; //Tp back up PID VID for OnDeviceDisconnected Event - IF FTDI is gone, PID VID (this.VID_PID) are also gone
        //private bool USBMonitorIsConnected = false;
        /// <summary>
        /// Starts USB Monitoring via Windows Messages
        /// </summary>
        public void StartUSBMonitoring()
        {
            if (usbm == null)
            {
                usbm = new USBMonitor();
                usbm.USBDeviceConnectedEvent += new USBMonitor.USBDeviceConnectedHandler(Usbm_USBDeviceConnectedEvent);
                usbm.USBDeviceDisConnectedEvent += new USBMonitor.USBDeviceDisConnectedHandler(Usbm_USBDeviceDisConnectedEvent);
            }
            //usbm.StartUSBMonitoring("0403", "6010", "");
            VID_PID_opened = VID_PID;
            //usbm.StartUSBMonitoring("", "", VID_PID_opened);
        }

        /// <summary>
        /// Stops the usb monitoring.
        /// </summary>
        public void StopUSBMonitoring()
        {
            usbm?.Close();
        }

        /* Events kommen 3x!!!!         */
        private void Usbm_USBDeviceDisConnectedEvent()
        {
            if (_ConnectionStatus != EnumConnectionStatus.USB_disconnected)
            {
                _ConnectionStatus = EnumConnectionStatus.USB_disconnected;
                OnDeviceDisconnected(VID_PID_opened);
            }
        }

        private void Usbm_USBDeviceConnectedEvent()
        {
            if (_ConnectionStatus != EnumConnectionStatus.USB_reconnected)
            {
                _ConnectionStatus = EnumConnectionStatus.USB_reconnected;
                OnDeviceConnected(EnumConnectionResult.NoConnection);
            }
        }

        #endregion

        /// <summary>
        /// Closes this instance and communication
        /// </summary>
        public void Close_All()
        {
            StopUSBMonitoring();
            Close_Connection();
        }

        public void Close_Connection()
        {
            _8KanalReceiverV2_XBee?.Close();

            _8KanalReceiverV2_RS232?.Close();

            FTDI_D2xx?.Close();  //2nd Close

            //_8KanalReceiverV2_SDCard?.Close();
        }

        #region IDisposable Members

        public void Dispose()
        {
            DisposeDevice();
            GC.SuppressFinalize(this);
        }

        private void DisposeDevice()
        {
            _8KanalReceiverV2_XBee?.Dispose();

            _8KanalReceiverV2_RS232?.Dispose();

            FTDI_D2xx?.Dispose();
        }

        #endregion
    }

}

