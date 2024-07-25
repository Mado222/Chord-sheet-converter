﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using WindControlLib;

namespace XBeeLib
{
    /// <summary>
    /// implements the API Type Remote AT Command Request (0x17)
    /// </summary>
    /// <remarks>base class: CATCommands</remarks>
    public class CRemoteATCommandRequest : CATCommand
    {

        private UInt64 _DestinationAddress64 = 0;
        /// <summary>
        /// destination address (64bit) 
        /// </summary>
        /// <remarks>Serial number of the remote device</remarks>
        public UInt64 DestinationAddress64
        {
            get { return _DestinationAddress64; }
            set
            {
                if (value != 0)
                {
                    _DestinationAddress16 = CXBAPICommands.Default16BitAddress;
                    _DestinationAddress64 = value;
                }
            }
        }

        private UInt16 _DestinationAddress16 = CXBAPICommands.Default16BitAddress;
        /// <summary>
        /// destination address (16bit)
        /// </summary>
        /// <remarks>MY-Address of the Remote Device</remarks>
        public UInt16 DestinationAddress16
        {
            get { return _DestinationAddress16; }
            set { _DestinationAddress16 = value; }
        }

        private bool _ApplyChangesOnRemote = true;
        /// <summary>
        /// Whether changes are immediately applied
        /// </summary>
        public bool ApplyChangesOnRemote
        {
            get { return _ApplyChangesOnRemote; }
            set { _ApplyChangesOnRemote = value; }
        }

        /// <summary>
        /// constructor
        /// </summary>
        /// <remarks>inits the AP ID</remarks>
        public CRemoteATCommandRequest()
        {
            APID = CXBAPICommands.RemoteATCommandRequest;
        }

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="ApplyChangesOnRemote">wheter the changes should be applied to remote</param>
        /// <param name="ATCommandResponse">wheter the remote should send an response</param>
        /// <param name="DestinationAddress64">serial number (64bit) of the remote device</param>
        /// <param name="ATCommand">command in AT format</param>
        public CRemoteATCommandRequest(bool ApplyChangesOnRemote, bool ATCommandResponse, ulong DestinationAddress64, String ATCommand) : this()
        {
            this.ApplyChangesOnRemote = ApplyChangesOnRemote;
            this.ATCommandResponse = ATCommandResponse;
            this.DestinationAddress64 = DestinationAddress64;
            this.ATCommand = ATCommand;
        }

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="ApplyChangesOnRemote">wheter the changes should be applied to remote</param>
        /// <param name="ATCommandResponse">wheter the remote should send an response</param>
        /// <param name="DestinationAddress16">MY address (16bit) of the remote device</param>
        /// <param name="ATCommand">command in AT format</param>
        public CRemoteATCommandRequest(bool ApplyChangesOnRemote, bool ATCommandResponse, ushort DestinationAddress16, String ATCommand) : this()
        {
            this.ApplyChangesOnRemote = ApplyChangesOnRemote;
            this.ATCommandResponse = ATCommandResponse;
            this.DestinationAddress16 = DestinationAddress16;
            this.ATCommand = ATCommand;
        }

        /// <summary>
        /// makes the basic data frame
        /// </summary>
        /// <param name="FrameData">frame data</param>
        /// <returns>byte array, ready to send over the serial port</returns>
        override protected byte[] MakeBasicDataFrame(List<byte> FrameData)
        {
            return base.MakeBasicDataFrame(FrameData);
        }

        /// <summary>
        /// Returns complete byte [] to be sent to configure remote device
        /// </summary>
        /// <param name="ApiMode">API-Mode</param>
        /// <returns>byte array, ready to send over the serial port</returns>
        override public byte[] Get_CommandRequest_DataFrame(XBAPIMode ApiMode)
        {
            List<byte> FrameData = new List<byte>
            {
                //Start to build FrameData

                //FrameData Byte 0
                APID,

                //FrameData Byte 1
                frameId
            };

            //FrameData Byte 2-9: 64 Bit Destination Address
            byte[] be = BitConverter.GetBytes(_DestinationAddress64);
            for (int i = be.Length - 1; i >= 0; i--)
                FrameData.Add(be[i]);

            //FrameData Byte 10-11: 16 Bit Destination Address
            be = BitConverter.GetBytes(_DestinationAddress16);
            for (int i = be.Length - 1; i >= 0; i--)
                FrameData.Add(be[i]);

            //FrameData Byte 14: Command Options
            if (ApplyChangesOnRemote)
                FrameData.Add(2);
            else
                FrameData.Add(0);

            FrameData.AddRange(Command2ByteList());
            
            return MakeBasicDataFrame(FrameData, ApiMode);
        }

        /// <summary>
        /// checks if the received response corresponse to the request
        /// </summary>
        /// <param name="response">received response</param>
        /// <returns>
        /// true: if the response corresponses the the request
        /// false: if not</returns>
        override public bool checkResponse(CBasicAPIResponse response)
        {
            bool IsResponseCorrect = false;
            switch (response.APID)
            {
                case (CXBAPICommands.RemoteCommandResponse):
                    
                    CRemoteCommandResponse responseNew = (CRemoteCommandResponse)response;
                    //if (responseNew.status == RXCommandResponseStatus.OK)
                    //{
                        //is frameId equal
                        if (this.frameId == responseNew.frameId)
                        {
                            //is command equal
                            if (this.ATCommand.Substring(2, 2) == responseNew.command)
                            {
                                //is address equal
                                if ((this.DestinationAddress64 == responseNew.ResponderAddress64) ||
                                    (this.DestinationAddress16 == responseNew.ResponderAddress16))
                                {
                                    IsResponseCorrect = true;
                                }
                            }
                        }
                    //}
                    break;
                
                default:
                    IsResponseCorrect = false;
                    break;
            }
            return IsResponseCorrect;
        }
    }

}