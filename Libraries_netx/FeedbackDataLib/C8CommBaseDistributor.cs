﻿using FeedbackDataLib.Modules;
using System.Diagnostics;
using WindControlLib;


namespace FeedbackDataLib
{
    public partial class C8CommBase
    {
        // Define a custom struct or class to hold input data and TCS
        public class CommandRequest
        {
            public EnNeuromasterCommand Command { get; set; }
            public EnModuleCommand ModuleCommand { get; set; } = EnModuleCommand.None;
            public byte[] SendData { get; set; }
#pragma warning disable IDE0301 // Simplify collection initialization
            public byte[] ResponseData { get; set; } = Array.Empty<byte>();
#pragma warning restore IDE0301 // Simplify collection initialization
            public bool Success { get; set; } = false;
            public byte HWcn { get; set; } = 0xff;
            public DateTime RunningEnd { get; set; } = DateTime.MinValue;

            public CommandRequest(EnNeuromasterCommand command, byte[] sendData)
            {
                Command = command;
                SendData = sendData;
            }

            public CommandRequest()
            {
                Command = EnNeuromasterCommand.None;
#pragma warning disable IDE0301 // Simplify collection initialization
                SendData = Array.Empty<byte>();
#pragma warning restore IDE0301 // Simplify collection initialization
            }
        }


        private readonly TimeSpan TsCommandTimeout = TimeSpan.FromSeconds(WaitCommandResponseTimeOutMs / 1000);
#pragma warning disable IDE0301 // Simplify collection initialization
        private CommandRequest RunningCommand = new(EnNeuromasterCommand.None, Array.Empty<byte>());
#pragma warning restore IDE0301 // Simplify collection initialization

        /// <summary>
        /// Time when next Alive Signal is due
        /// </summary>
        private DateTime NextAliveSignalToSend = DateTime.Now;

        private readonly TimeSpan AliveSignalToSendInterv = new(0, 0, 0, 0, AliveSignalToSendIntervMs);


        private readonly CFifoConcurrentQueue<CommandRequest> _sendingQueue = new();
        public CFifoConcurrentQueue<CommandRequest> SendingQueue => _sendingQueue;

        
        private readonly CFifoConcurrentQueue<CommandRequest> _runningCommandsQueue = new();
        private CFifoConcurrentQueue<CommandRequest> RunningCommandsQueue => _runningCommandsQueue;

        public event EventHandler<CommandRequest>? CommandProcessed;
        protected virtual void OnCommandProcessed(CommandRequest e)
        {
            CommandProcessed?.Invoke(this, e);
        }

        private void SendCommand(EnNeuromasterCommand neuromasterCommand, byte[]? additionalData, CommandRequest? cr= null)
        {
            // Validate AdditionalDataToSend size early
#pragma warning disable IDE0301 // Simplify collection initialization
            additionalData ??= Array.Empty<byte>();
#pragma warning restore IDE0301 // Simplify collection initialization
            if (additionalData.Length > 250)
            {
                throw new ArgumentException("Size of AdditionalDataToSend must be <= 250", nameof(additionalData));
            }

            const int overhead = 4; // CommandCode, Command, Length, CRC
            int lengthWithCRC = additionalData.Length + 1;
            int bytesToSend = overhead + additionalData.Length;
            byte[] buf = new byte[bytesToSend];

            // Build the command buffer
            buf[0] = CommandCode;    // Base command code
            buf[1] = (byte)neuromasterCommand;                   // Specific command code
            buf[2] = (byte)lengthWithCRC;                        // Length byte (+CRC)

            // Copy additional data into the buffer
            Buffer.BlockCopy(additionalData, 0, buf, overhead - 1, additionalData.Length);

            // Calculate and set CRC
            buf[^1] = CRC8.Calc_CRC8(buf, buf.Length - 1);

            // Create or update the CommandRequest and enqueue it
            cr ??= new CommandRequest
            {
                Command = neuromasterCommand,
                SendData = buf
            };

            _sendingQueue.Push(cr);
        }



        #region DistributorThread
        /// <summary>
        /// This thread continously gets the data from the RS232ReceiverThread and raises OnDataReadyComm
        /// Thread is required since RS232WorkerThread should not call events due to unpredictable time delays
        /// </summary>
        private async Task DistributorThreadAsync(CancellationToken cancellationToken)
        {
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "DistributorThread";

            if (c8Receiver.Connection == null) throw new Exception("c8Receiver.Connection not allowed to be null");

            if (!c8Receiver.Connection.SerialPort.IsOpen) c8Receiver.Connection.SerialPort.GetOpen();

            NextAliveSignalToSend = DateTime.Now;
            receiver = new CRS232Receiver(0x0f, c8Receiver.Connection.SerialPort);

            // Start the RS232ReceiverThread and pass the cancellation token
            _ = receiver.StartRS232ReceiverThreadAsync(cancellationToken).ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    // Handle exception (log, etc.)
                    Debug.WriteLine(t.Exception);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);


            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    DateTime Now = DateTime.Now;
                    //Any data coming in?
                    if (!receiver.CommandResponseQueue.IsEmpty)
                    {
                        //Correct response?
                        var pk = receiver.CommandResponseQueue.Peek();
                        if (pk != null && pk[0] == (byte)RunningCommand.Command)
                        {
                            //Yes, get it
                            byte[]? res = receiver.CommandResponseQueue.Pop();
                            if (res != null)
                            {
                                RunningCommand.ResponseData = res;
                                RunningCommand.Success = true;
                                EvalCommandResponse(RunningCommand);
                            }
                            else
                            {
                                OnCommandProcessed(new CommandRequest());
                            }
                            RunningCommand = new();
                        }
                    }

                    if (Now > RunningCommand.RunningEnd)
                    {
                        //Timeout
                        _ = receiver.CommandResponseQueue.Pop();
                        RunningCommand = new CommandRequest();
                        OnCommandProcessed(RunningCommand);
                    }

                    //Incoming: Command to PC 
                    if (!receiver.CommandToPCQueue.IsEmpty)
                    {
                        //Fire event
                        var buf = receiver.CommandToPCQueue.Pop();
                        if (buf != null)
                            EvalCommunicationToPC(buf);
                    }


                    // Incoming: Distribute measurement data 
                    if (!receiver.MeasurementDataQueue.IsEmpty)
                    {
                        CDataIn[]? buffer = receiver.MeasurementDataQueue.PopAll();
                        if (buffer?.Length > 0)
                        {
                            EvalMeasurementData(new List<CDataIn>(buffer));
                        }
                    }

                    // Outgoing: Send data 
                    if (!SendingQueue.IsEmpty)
                    {
                        CommandRequest? cr = SendingQueue.Pop();
                        if (cr is not null && cr.SendData.Length > 0)
                        {
                            RunningCommand = cr;
                            c8Receiver.Connection.SerialPort.Write(cr.SendData, 0, cr.SendData.Length); // Adjust cancellation token as needed
                            RunningCommand.RunningEnd = DateTime.Now + TsCommandTimeout;
                        }
                    }

                    // Send "alive" signal periodically
                    if (Now > NextAliveSignalToSend)
                    {
                        CommandRequest cr = new(EnNeuromasterCommand.DeviceAlive, AliveSequToSend());
                        SendingQueue.Push(cr);
                        NextAliveSignalToSend = Now + AliveSignalToSendInterv;
                    }

                    else
                    {
                        await Task.Delay(10, cancellationToken); // Avoid high CPU usage
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DistributorThreadAsync Error: " + ex.Message);
            }
            finally
            {
                receiver.StopRS232ReceiverThread();
                c8Receiver.Connection.SerialPort.Close();
                Debug.WriteLine("DistributorThreadAsync Closed");
            }
        }


        protected virtual void EvalCommandResponse(CommandRequest rc)
        {
            //Prepare message text for display
            ColoredText msg;
            string? nm = Enum.GetName(typeof(EnNeuromasterCommand), RunningCommand.Command);
            if (rc.Success)
            {
                msg = new(nm + ": " + "OK", Color.Green);
            }
            else
            {
                msg = new(nm + ": " + "Failed", Color.Red);
            }

            switch (rc.Command)
            {
                case EnNeuromasterCommand.SetConnectionClosed:
                    OnSetConnectionClosedResponse(rc.Success, msg);
                    break;

                case EnNeuromasterCommand.GetFirmwareVersion:
                    if (rc.Success)
                    {
                        CNMFirmwareVersion NMFirmwareVersion = new();
                        NMFirmwareVersion.UpdateFromByteArray(rc.ResponseData, 0);
                        OnGetFirmwareVersionResponse(NMFirmwareVersion, msg);
                    }
                    else
                        OnGetFirmwareVersionResponse(null, msg);
                    break;
                case EnNeuromasterCommand.ScanModules:
                    OnScanModulesResponse(rc.Success, msg);
                    break;

                case EnNeuromasterCommand.GetModuleConfig:
                    if (rc.Success)
                    {
                        //Collect data of all HW channels
                        cntDeviceConfigs--;
                        if (rc.ResponseData != null)
                            allDeviceConfigData.AddRange(rc.ResponseData);

                        if (cntDeviceConfigs == 0)
                        {
                            //All data in
                            try
                            {
                                UpdateModuleInfoFromByteArray([.. allDeviceConfigData]);
                                Calculate_SkalMax_SkalMin(); // Calculate max and mins
                                OnGetDeviceConfigResponse(ModuleInfos, msg);

                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("C8KanalReceiverV2_CommBase_#01: " + ex.Message);
                                rc.Success = false;
                                OnGetDeviceConfigResponse([], msg);
                            }
                        }
                    }
                    break;

                case EnNeuromasterCommand.SetModuleConfig:
                    if (rc.Success)
                    {

                    }
                    break;
                case EnNeuromasterCommand.SetConfigAllModules:
                    if (rc.Success)
                    {
                    }
                    break;
                case EnNeuromasterCommand.WrRdModuleCommand:
                    if (rc.Success)
                    {
                        try
                        {
                            switch (RunningCommand.ModuleCommand)
                            {
                                case EnModuleCommand.ModuleGetInfoSpecific:
                                    byte[] btin = new byte[CModuleBase.ModuleSpecific_sizeof];
                                    Buffer.BlockCopy(rc.ResponseData, 1, btin, 0, CModuleBase.ModuleSpecific_sizeof);
                                    //12.11.2020 Check CRC
                                    string s = "GetModuleInfoSpecific: ";
                                    for (int i = 0; i < btin.Length; i++)
                                    {
                                        s += btin[i].ToString("X2") + ", ";
                                    }
                                    byte crc = CRC8.Calc_CRC8(btin, btin.Length - 2);

                                    s += "    /  CalcCRC=" + crc.ToString("X2");
                                    Debug.WriteLine(s);

                                    if (UpdateModuleInfo)
                                        ModuleInfos[HWcnGetModuleInfoSpecific].SetModuleSpecific(btin);

                                    OnModuleGetInfoSpecificResponse(btin, msg);

                                    break;
                                case EnModuleCommand.ModuleSetInfoSpecific:
                                    break;

                            }
                        }
                        catch
                        {
                        }
                    }

                    //SendSuccess();
                    break;

                case EnNeuromasterCommand.GetClock:
                    if (rc.Success)
                    {
                        DeviceClock.UpdateFrom_ByteArray(rc.ResponseData, 0);
                        OnGetClockResponse(DeviceClock.Dt, msg);
                    }
                    else
                        OnGetClockResponse(null, msg);
                    break;
                case EnNeuromasterCommand.SetClock:
                    OnSetClockResponse(rc.Success, msg);
                    break;
            }
        }



        /// <summary>
        /// NM is communicating back to PC
        /// </summary>
        /// <param name="buf">The buf.</param>
        protected virtual void EvalCommunicationToPC(byte[] buf)
        {
            switch (buf[1])
            {
                case CNMtoPCCommands.cModuleError:
                    {
                        if (buf.Length > 2)
                            OnDeviceToPC_ModuleError(buf[2]);
                        break;
                    }

                case CNMtoPCCommands.cBufferFull:
                    {
                        OnDeviceToPC_BufferFull();
                        break;
                    }
                case CNMtoPCCommands.cBatteryStatus:
                    {
                        //buf[2] ... Battery Low    
                        //buf[3] ... Battery High [1/10V]
                        //buf[4] ... Supply Low
                        //buf[5] ... Supply High  [1/10V] 

                        uint BatteryVoltage_mV = buf[3];
                        BatteryVoltage_mV = ((BatteryVoltage_mV << 8) + buf[2]) * 10;


                        uint SupplyVoltage_mV = buf[5];
                        SupplyVoltage_mV = ((SupplyVoltage_mV << 8) + buf[4]) * 10;

                        uint BatteryPercentage = (uint)BatteryVoltage.GetPercentage(((double)BatteryVoltage_mV) / 1000);

                        OnDeviceToPC_BatteryStatus(BatteryVoltage_mV, BatteryPercentage, SupplyVoltage_mV);
                        break;
                    }
                case CNMtoPCCommands.cNMOffline:   //28.7.2014
                    {
                        Close();  //Kommumikation beenden
                        break;
                    }
            }
        }

        /// <summary>
        /// Receives data, takes care of over all data synchronicity
        /// Updates time and forwards event
        /// </summary>
        /// <remarks>
        /// Only forwards data if the first Sync Value is received
        /// </remarks>
        private void EvalMeasurementData(List<CDataIn> DataIn)
        {
            if (DataIn == null) return;

            List<CDataIn> dataIn = [];
            foreach (CDataIn di in DataIn)
            {
                //27.1.2020
                if (di.SyncFlag == 1)
                {
                    if (ReceivingStarted == DateTime.MinValue)
                    {
                        //First Sync Packet of this channel
                        ReceivingStarted = di.LastSync;
                        OldLastSync = di.LastSync;
                        cntSyncPackages = 0;
                    }
                    else
                    {
                        if (di.LastSync != OldLastSync)
                        {
                            OldLastSync = di.LastSync;
                            cntSyncPackages++;
                        }
                    }
                    ModuleInfos[di.HWcn].SWChannels[di.SWcn].SWChan_Started = ReceivingStarted;
                    ModuleInfos[di.HWcn].SWChannels[di.SWcn].SynPackagesreceived = cntSyncPackages;
                }

                UpdateTime(di);
                di.VirtualID = ModuleInfos[di.HWcn].SWChannels[di.SWcn].VirtualID;

                if (di.ChannelStarted != DateTime.MinValue)
                {
                    //Process Data Module specific
                    dataIn.AddRange(ModuleInfos[di.HWcn].Processdata(di));
                }
            }
            OnDataReadyResponse(dataIn);
        }



        //private static void FireAndForgetTask(Func<Task> asyncFunc)
        //{
        //    asyncFunc().ContinueWith(task =>
        //    {
        //        if (task.IsFaulted)
        //        {
        //            // Log or handle the exception if needed
        //            Debug.WriteLine($"Error in task: {task.Exception?.GetBaseException().Message}");
        //        }
        //    }, TaskScheduler.Default);
        //}

        private CancellationTokenSource? cancellationTokenDistributor;

        public void StartDistributorThreadAsync()
        {
            cancellationTokenDistributor = new CancellationTokenSource();
            Task.Run(() => DistributorThreadAsync(cancellationTokenDistributor.Token));
        }

        public void StopDistributorThreadAsync()
        {
            cancellationTokenDistributor?.Cancel();
        }
        #endregion

    }
}