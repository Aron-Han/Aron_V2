using Aron_V2.UI_Update;
using Cognex.VisionPro;
using Cognex.VisionPro.Comm;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Aron_V2
{
    enum CommType
    {
        Profinet,
        EthernetIP
    }

    class CC24_Comm
    {

        #region 成员变量
        private CogCommCards mCards;             //定义通讯卡集合类
        private CogCommCard mCard;               //定义单个卡
        public CogNdm mNdm;                      //定义网络通讯数据模型
        private CogEthernetPort mEthernetPort;   //通信端口
        private CogEthernetPortSettings mEthernetPortSettings;    //IP地址和子网掩码
		private System.Timers.Timer waitForProtocol;              //连接等待时长
		private const int ProtocolConnectTimeoutMs = 30000;
		private readonly object _protocolTimerLock = new object();
		private DateTime _ffpStartTime = DateTime.MinValue;

		private string _ip = "192.168.0.2";
        private string _sunet = "255.255.255.0";
        private string _hostName = "";
        private bool _isConnected;
		private volatile bool _operatingOnline = true;

		public void SetOperatingOnline(bool online)
		{
			lock (sendLock)
			{
				_operatingOnline = online;
				if (!_isConnected || mNdm == null) return;
				PublishOperatingState();
			}
		}

		private void PublishOperatingState()
		{
			if (_operatingOnline)
			{
				lock (Global.PlcBufferLock) Array.Clear(Global.Result_Send, 0, Global.Result_Send.Length);
				mNdm.NotifySystemStatus(false, false);
				for (int ch = 0; ch < CommunicationConfigHelper.ProfinetChannelCount; ch++) mNdm.NotifyAcquisitionDisabled(ch);
				mNdm.NotifyRunning();
				// NDM combines local acquisition readiness with the PLC Trigger Enable
				// bit. NotifyAcquisitionReady arms the channel; it cannot bypass that bit.
				for (int ch = 0; ch < CommunicationConfigHelper.ProfinetChannelCount; ch++) mNdm.NotifyAcquisitionReady(ch);
				mNdm.NotifySystemStatus(true, false);
			}
			else
			{
				for (int ch = 0; ch < CommunicationConfigHelper.ProfinetChannelCount; ch++) mNdm.NotifyAcquisitionDisabled(ch);
				mNdm.NotifySystemStatus(false, false);
				mNdm.NotifyStopped((CogNdmStoppedCodeConstants)0);
			}
		}
        public CommType commType = CommType.Profinet;
        public event Action<object, byte[], int> NewUserData;
        public event Action<object, string> NewTrigger;
        public event Action<object, int> JobChange;
        private static readonly object sendLock = new object();

        public bool IsConnected
        {
            get { return _isConnected; }
        }
        #endregion

        private static CC24_Comm _CC24_ProfinetSingleton = null;
        private static readonly object locker = new object();

        /// <summary>
        /// 单例启动
        /// </summary>
        /// <returns></returns>
        public static CC24_Comm Instance()
        {
            if (_CC24_ProfinetSingleton == null)
            {
                lock (locker)
                {
                    _CC24_ProfinetSingleton = new CC24_Comm();
                }

            }
            return _CC24_ProfinetSingleton;
        }

        #region 通信帮助类
        /// <summary>
        /// 启动通信卡
        /// </summary>
        /// <returns></returns>
        public bool InitCommCard()
        {
            // This singleton owns the port for the lifetime of the process.
            // Reuse it after Stop/Start instead of creating a second port 0.
            if(mCard!=null && mEthernetPort!=null) return true;
            mCards = new CogCommCards();     //初始化Profinet卡，查找安装的CC24卡的张数
            if (mCards.Count == 0)
            {
                LogChangeEventArgs.Set("Log", "Do not find Communicate Card！", Color.Red);
                return false;
            }
            mCard = mCards[0];  //用找到的第一个卡

            if (mCard.FfpAccess == null)
            {
                System.Windows.MessageBox.Show("Need FFP");
                return false;
            }

            mEthernetPort = mCard.EthernetPortAccess.CreateEthernetPort(0);

            mEthernetPortSettings = mEthernetPort.ReadSettings();

            this._ip = mEthernetPortSettings.IPAddress.ToString();
            this._sunet = mEthernetPortSettings.SubnetMask.ToString();
            this._hostName = mEthernetPortSettings.HostName;

            if (mEthernetPort.IsInterfaceUp)
            {
                CogEthernetPortSettings eActiveSettings = mEthernetPort.ReadActiveSettings();
            }
            return true;
        }

        /// <summary>
        /// 添加NDM事件
        /// </summary>
        private void AddEventHandlers()
        {
            mNdm.ClearError += new CogNdmClearErrorEventHandler(mNdm_ClearError);
            //mNdm.JobChangeRequested += new CogNdmJobChangeRequestedEventHandler(mNdm_JobChangeRequested);
            mNdm.NewUserData += new CogNdmNewUserDataEventHandler(mNdm_NewUserData);
            mNdm.OfflineRequested += new CogNdmOfflineRequestedEventHandler(mNdm_OfflineRequested);
            mNdm.ProtocolStatusChanged += new CogNdmProtocolStatusChangedEventHandler(mNdm_ProtocolStatusChanged);
            mNdm.TriggerAcquisition += new CogNdmTriggerAcquisitionEventHandler(mNdm_TriggerAcquisition);
            mNdm.TriggerAcquisitionDisabledError += new CogNdmTriggerAcquisitionDisabledErrorEventHandler(mNdm_TriggerAcquisitionDisabledError);
            mNdm.TriggerAcquisitionNotReadyError += new CogNdmTriggerAcquisitionNotReadyErrorEventHandler(mNdm_TriggerAcquisitionNotReadyError);
            mNdm.TriggerAcquisitionStop += new CogNdmTriggerAcquisitionStopEventHandler(mNdm_TriggerAcquisitionStop);
            //mNdm.TriggerSoftEvent += new CogNdmTriggerSoftEventEventHandler(mNdm_TriggerSoftEvent);
            mNdm.TriggerSoftEventOff += new CogNdmTriggerSoftEventOffEventHandler(mNdm_TriggerSoftEventOff);
        }

        private void RemoveEventHandlers()
        {
            mNdm.ClearError -= new CogNdmClearErrorEventHandler(mNdm_ClearError);
            //mNdm.JobChangeRequested -= new CogNdmJobChangeRequestedEventHandler(mNdm_JobChangeRequested);
            mNdm.NewUserData -= new CogNdmNewUserDataEventHandler(mNdm_NewUserData);
            mNdm.OfflineRequested -= new CogNdmOfflineRequestedEventHandler(mNdm_OfflineRequested);
            mNdm.ProtocolStatusChanged -= new CogNdmProtocolStatusChangedEventHandler(mNdm_ProtocolStatusChanged);
            mNdm.TriggerAcquisition -= new CogNdmTriggerAcquisitionEventHandler(mNdm_TriggerAcquisition);
            mNdm.TriggerAcquisitionDisabledError -= new CogNdmTriggerAcquisitionDisabledErrorEventHandler(mNdm_TriggerAcquisitionDisabledError);
            mNdm.TriggerAcquisitionNotReadyError -= new CogNdmTriggerAcquisitionNotReadyErrorEventHandler(mNdm_TriggerAcquisitionNotReadyError);
            mNdm.TriggerAcquisitionStop -= new CogNdmTriggerAcquisitionStopEventHandler(mNdm_TriggerAcquisitionStop);
            //mNdm.TriggerSoftEvent -= new CogNdmTriggerSoftEventEventHandler(mNdm_TriggerSoftEvent);
            mNdm.TriggerSoftEventOff -= new CogNdmTriggerSoftEventOffEventHandler(mNdm_TriggerSoftEventOff);
        }

        #endregion


        #region NDM 信号响应事件

        #region 数据发送
        /// <summary>
        /// 发送结果
        /// </summary>
        /// <param name="resultData">发送结果byte数组</param>
        /// <param name="offset">结果偏移位</param>
        /// <param name="InspectionIndex">通道</param>
        public void SendData(byte[] resultData, int offset, int InspectionIndex)
        {
            lock (sendLock)
            {
                CogNdmInspectionResult res = new CogNdmInspectionResult();  //检测结果
                res.InspectionIndex = InspectionIndex;          //InspectionIndex 0-3
                res.InspectionPassed = true;                    //检测结果OK/NG

                //结果数据区
                res.ResultData = resultData;
                res.ResultDataOffset = offset;

                // 检测结果编码
                res.ResultCode = 5;

                // Construct an object which identifies which image(s) were processed to create the inspection result.
                CogNdmUsedAcquisitionIDCollection ids = new CogNdmUsedAcquisitionIDCollection() { new CogNdmUsedAcquisitionID(0, InspectionIndex) };

                // Add the processed image ids to the result object.
                res.UsedAcquisitionIDs = ids;

                // finally, notify the PLC of the completed inspection result.

                mNdm.NotifyInspectionComplete(res);
            }

        }

		/// <summary>
		/// Takes the shared Result snapshot only after the NDM send lock is held.
		/// This preserves interleaved channel fields when inspections complete at
		/// nearly the same time: an older snapshot can never be sent after a newer one.
		/// </summary>
		public void SendSharedResultData(int offset, int inspectionIndex)
		{
			lock (sendLock)
			{
				byte[] snapshot;
				lock (Global.PlcBufferLock)
				{
					snapshot = new byte[Global.Result_Send.Length];
					Buffer.BlockCopy(Global.Result_Send, 0, snapshot, 0, snapshot.Length);
				}

				CogNdmInspectionResult result = new CogNdmInspectionResult();
				result.InspectionIndex = inspectionIndex;
				result.InspectionPassed = true;
				result.ResultData = snapshot;
				result.ResultDataOffset = offset;
				result.ResultCode = 5;
				result.UsedAcquisitionIDs = new CogNdmUsedAcquisitionIDCollection() { new CogNdmUsedAcquisitionID(0, inspectionIndex) };
				mNdm.NotifyInspectionComplete(result);
			}
		}
        internal void SendJobPosEcho(int channel,byte job,byte pos)
        {
            lock(sendLock)
            {
                if(!_isConnected||mNdm==null||!_operatingOnline) throw new InvalidOperationException("Profinet Job/Pos echo unavailable: NDM not connected/Online.");
                var result=CreateJobPosEchoFrame(channel,job,pos);
                // A metadata result frame is explicitly requested by the PLC workflow.
                // This sends no acquisition notification. NDM owns Result Valid/completion/ACK buffering.
                mNdm.NotifyInspectionComplete(result);
            }
        }
        internal static CogNdmInspectionResult CreateJobPosEchoFrame(int channel,byte job,byte pos)
        {
            byte[] snapshot=Profinet.ProfinetDataMapper.PrepareJobPosEcho(channel,job,pos);
            return new CogNdmInspectionResult { InspectionIndex=channel,InspectionPassed=true,ResultCode=5,ResultData=snapshot,ResultDataOffset=0,
                // The 9.6 SR2 API rejects an empty ID collection. Metadata-only
                // frames use ID 0 on the corresponding channel; no camera is triggered.
                UsedAcquisitionIDs=new CogNdmUsedAcquisitionIDCollection { new CogNdmUsedAcquisitionID(0,channel) } };
        }
        internal void NotifyAcquisitionCompleted(int channel)
        {
            lock(sendLock)
            {
                if(!_isConnected||mNdm==null||!_operatingOnline)return;
                // NDM sets Exposure Complete and re-arms Trigger Ready. It still
                // honors PLC Trigger Enable; this does not publish inspection results.
                mNdm.NotifyAcquisitionComplete(channel,0);
            }
        }
        #endregion

        /// <summary>
        /// The NDM raises the TriggerAcquisition event to inform the vision 
        /// system that the remote device has requested an image Acquisition.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void mNdm_TriggerAcquisition(object sender, CogNdmTriggerAcquisitionEventArgs e)
        {
            string data = e.CameraIndex.ToString();
            Global.Manual_Trigger_Lock[int.Parse(data)] = false;
            NewTrigger?.Invoke(this, data);
        }

        /// <summary>
        /// The NDM raises the TriggerAcquisitionStop event to tell the 
        /// vision system that the Acquisition trigger has been reset. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void mNdm_TriggerAcquisitionStop(Object sender, CogNdmTriggerAcquisitionStopEventArgs e)
        {
        }

        /// <summary>
        ///  The NDM raises the TriggerSoftEvent event to inform the vision system 
        /// that the remote device has requested that a soft event execute.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void mNdm_TriggerSoftEvent(object sender, CogNdmTriggerSoftEventEventArgs e)
        {
        }

        /// <summary>
        /// The NDM raises the TriggerSoftEventOff event to tell the vision system
        /// that the soft event trigger bit has been reset.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void mNdm_TriggerSoftEventOff(object sender, CogNdmTriggerSoftEventOffEventArgs e)
        {
            // The event is raised from a non-GUI thread.
            // Invoke this function on the GUI thread.
            //if (InvokeRequired)
            //{
            //    Invoke(new CogNdmTriggerSoftEventOffEventHandler(mNdm_TriggerSoftEventOff), new Object[] { sender, e });
            //    return;
            //}
        }

        /// <summary>
        /// The NDM raises the TriggerAcquisitionDisabledError event to tell the
        /// vision system that an acquisition trigger was set but the acquisition 
        /// trigger was not enabled. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void mNdm_TriggerAcquisitionDisabledError(object sender, CogNdmTriggerAcquisitionDisabledErrorEventArgs e)
        {

        }

        /// <summary>
        /// The NDM raises the TriggerAcquisitionNotReadyError event to tell the
        /// vision system that an acquisition trigger was set on the PLC but the
        /// vision system was not ready to receive it. 
        /// </summary>
        void mNdm_TriggerAcquisitionNotReadyError(object sender, CogNdmTriggerAcquisitionNotReadyErrorEventArgs e)
        {

        }

		/// <summary>
		/// The NDM raises the ProtocolStatusChanged event to inform the vision
		/// system that the status of the PLC protocol connection has changed. 
		/// 通讯卡连接状态触发事件
		/// </summary>
		/// <summary>
		/// The NDM raises the ProtocolStatusChanged event to inform the vision
		/// system that the status of the PLC protocol connection has changed.
		/// 通讯卡连接状态触发事件
		/// </summary>
		void mNdm_ProtocolStatusChanged(object sender, CogNdmProtocolStatusChangedEventArgs e)
		{
			string remoteAddress = "N/A";

			try
			{
				if (e.RemoteAddress != null)
					remoteAddress = e.RemoteAddress.ToString();
			}
			catch
			{
				remoteAddress = "N/A";
			}

			LogChangeEventArgs.Set("Log",
				$"ProtocolStatusChanged: MessageID={e.MessageID}, Status={e.ProtocolStatus}, Remote={remoteAddress}",
				Color.Black);

			if (e.ProtocolStatus == CogNdmConnectionStatusConstants.Connected)
			{
				_isConnected = true;

				lock (_protocolTimerLock)
				{
					if (waitForProtocol != null)
						waitForProtocol.Enabled = false;
				}

				double costSeconds = 0;
				if (_ffpStartTime != DateTime.MinValue)
					costSeconds = (DateTime.Now - _ffpStartTime).TotalSeconds;

				LogChangeEventArgs.Set("Log",
					$"Profinet Connected. Cost={costSeconds:F1}s",
					Color.Green);

				try
				{
					lock (sendLock) PublishOperatingState();

					DataChangedEventArgs.Set("Profinet", "Online");
				}
				catch (Exception ex)
				{
					LogChangeEventArgs.Set("Log", "Initial Profinet notify failed: " + ex.Message, Color.Red);
				}
			}
			else
			{
				_isConnected = false;

				lock (_protocolTimerLock)
				{
					if (waitForProtocol != null)
						waitForProtocol.Enabled = true;
				}
				DataChangedEventArgs.Set("Profinet", "Offline");

				LogChangeEventArgs.Set("Log",
					"Profinet Offline. Waiting for PLC/PNIO controller connection...",
					Color.Orange);
			}
		}

		/// <summary>
		/// The NDM raises the OfflineRequested event to tell the vision system 
		/// that it should go offline. 
		/// </summary>
		void mNdm_OfflineRequested(object sender, CogNdmOfflineRequestedEventArgs e)
        {

        }

        /// <summary>
        /// The NDM raises the NewUserData event to tell the vision system that
        /// new user data has arrived from the remote device.
        /// </summary>
        void mNdm_NewUserData(object sender, CogNdmNewUserDataEventArgs e)
        {
            try
            {
                byte[] data = mNdm.ReadUserData(
                    0,
                    Profinet.ProfinetCommConfigHelper.ProcessDataLength);
                NewUserData?.Invoke(this, data, e.ChannelIndex);
            }
            catch (Exception ex)
            {
                LogChangeEventArgs.Set("Log",
                    "Read Profinet UserData failed. CH=" + e.ChannelIndex + ", " + ex.Message,
                    Color.Red);
            }
        }

        /// <summary>
        /// The NDM raises the JobChangeRequested event to inform the vision system
        /// that the remote device has requested a job change.
        /// </summary>
        void mNdm_JobChangeRequested(object sender, CogNdmJobChangeRequestedEventArgs e)
        {
            int data = e.JobID;
            JobChange?.Invoke(this, data);
        }

        /// <summary>
        /// The NDM raises the ClearError event to inform the vision system that 
        /// the remote device has been notified of an error reported by the vision
        /// system and the error has been be cleared. 
        /// </summary>
        void mNdm_ClearError(object sender, CogNdmClearErrorEventArgs e)
        {

        }
		#endregion

		#region 设置-启动


		/// <summary>
		/// Initialize FFP for a specific protocol.
		/// </summary>
		/// <summary>
		/// Initialize FFP for a specific protocol.
		/// </summary>
		public bool InitFFP()
		{
			try
			{
				if (mCard == null)
				{
					LogChangeEventArgs.Set("Log", "InitFFP failed: CC24 card is null.", Color.Red);
					return false;
				}

				if (mCard.FfpAccess == null)
				{
					LogChangeEventArgs.Set("Log", "InitFFP failed: FFP access is null.", Color.Red);
					return false;
				}

                if(mNdm!=null) { RemoveEventHandlers(); }
				else if (commType == CommType.Profinet)
				{
					mNdm = mCard.FfpAccess.CreateNetworkDataModel(CogFfpProtocolConstants.Profinet);
				}
				else if (commType == CommType.EthernetIP)
				{
					mNdm = mCard.FfpAccess.CreateNetworkDataModel(CogFfpProtocolConstants.EthernetIp);
				}
				else
				{
					LogChangeEventArgs.Set("Log", "InitFFP failed: unknown communication type.", Color.Red);
					return false;
				}
			}
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "Could not initialize Comm Card Network Data Model interface. " + ex.Message, Color.Red);
				return false;
			}

			try
			{
				// 先创建 Timer，再挂事件，再 Start NDM
				// 避免 mNdm.Start() 后 ProtocolStatusChanged 先触发，而 waitForProtocol 还没创建
				lock (_protocolTimerLock)
				{
					if (waitForProtocol != null)
					{
						waitForProtocol.Stop();
						waitForProtocol.Elapsed -= waitForProtocol_Elapsed;
						waitForProtocol.Dispose();
						waitForProtocol = null;
					}

					waitForProtocol = new System.Timers.Timer(ProtocolConnectTimeoutMs);
					waitForProtocol.AutoReset = false; // 只触发一次，避免 30s 一直重复刷日志
					waitForProtocol.Elapsed += new System.Timers.ElapsedEventHandler(waitForProtocol_Elapsed);
				}

				AddEventHandlers();

				_isConnected = false;
				_ffpStartTime = DateTime.Now;

				LogChangeEventArgs.Set("Log",
					"FFP Start, waiting for PLC connection. StartTime: " + _ffpStartTime.ToString("HH:mm:ss.fff"),
					Color.Black);

				// Start NDM 放在 Timer 创建之后
				mNdm.Start();

				lock (_protocolTimerLock)
				{
					if (waitForProtocol != null)
						waitForProtocol.Enabled = true;
				}
                return true;
			}
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "InitFFP start failed: " + ex.ToString(), Color.Red);

				try
				{
					RemoveEventHandlers();
				}
				catch { }

				lock (_protocolTimerLock)
				{
					if (waitForProtocol != null)
					{
						waitForProtocol.Stop();
						waitForProtocol.Elapsed -= waitForProtocol_Elapsed;
						waitForProtocol.Dispose();
						waitForProtocol = null;
					}
				}
                return false;
			}
		}

		/// <summary>
		/// 通讯卡连接失败事件
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		/// <summary>
		/// 通讯卡连接等待超时事件
		/// 注意：这个 Timer 不是重连定时器，只是连接等待超时提醒。
		/// </summary>
		void waitForProtocol_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			_isConnected = false;

			lock (_protocolTimerLock)
			{
				if (waitForProtocol != null)
					waitForProtocol.Enabled = false;
			}

			double costSeconds = 0;
			if (_ffpStartTime != DateTime.MinValue)
				costSeconds = (DateTime.Now - _ffpStartTime).TotalSeconds;

			LogChangeEventArgs.Set("Log",
				$"Profinet not connected after {costSeconds:F1}s. This timer only monitors connection timeout, it does not restart NDM.",
				Color.Orange);

			DataChangedEventArgs.Set("Profinet", "Offline");
		}

		/// <summary>
		/// Called when the user tries to close the application.
		/// </summary>
		public void Close()
		{
			lock (_protocolTimerLock)
			{
				if (waitForProtocol != null)
				{
					waitForProtocol.Stop();
					waitForProtocol.Elapsed -= waitForProtocol_Elapsed;
					waitForProtocol.Dispose();
					waitForProtocol = null;
				}
			}

			if (mNdm != null)
			{
				try
				{
					RemoveEventHandlers();
				}
				catch (Exception ex)
				{
					LogChangeEventArgs.Set("Log", "Remove NDM event handlers failed: " + ex.Message, Color.Red);
				}

                // Stop the protocol, retain the process-owned Ethernet port/NDM
                // for a later restart. An asynchronous interface-down could race
                // the next Start and disconnect a newly resumed session.

				try
				{
					mNdm.Stop();
				}
				catch (Exception ex)
				{
					LogChangeEventArgs.Set("Log", "NDM Stop failed: " + ex.Message, Color.Red);
				}
			}

			_isConnected = false;
		}
		#endregion
	}
}
