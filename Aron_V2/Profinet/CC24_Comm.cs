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
        private string _ip = "192.168.0.2";
        private string _sunet = "255.255.255.0";
        private string _hostName = "";
        private bool _isConnected;
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
                CogNdmUsedAcquisitionIDCollection ids = new CogNdmUsedAcquisitionIDCollection() { new CogNdmUsedAcquisitionID(1, 0) };

                // Add the processed image ids to the result object.
                res.UsedAcquisitionIDs = ids;

                // finally, notify the PLC of the completed inspection result.

                mNdm.NotifyInspectionComplete(res);
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
        void mNdm_ProtocolStatusChanged(object sender, CogNdmProtocolStatusChangedEventArgs e)
        {
            if (e.ProtocolStatus == CogNdmConnectionStatusConstants.Connected)
            {
                if (waitForProtocol != null)
                {
                    _isConnected = true;                   //通讯卡连接上
                    waitForProtocol.Enabled = false;       //卡连接上后timer给停止，不触发timer的回调函数
                    mNdm.NotifyRunning();
                    mNdm.NotifySystemStatus(true, false);
                    mNdm.NotifyAcquisitionReady(0);
                    mNdm.NotifyAcquisitionReady(1);
                    mNdm.NotifyAcquisitionReady(2);
                    mNdm.NotifyAcquisitionReady(3);
                    DataChangedEventArgs.Set("Profinet", "Online");
                }
            }
            else
            {
                _isConnected = false;                  //通讯卡断开
				waitForProtocol.Enabled = true;
				DataChangedEventArgs.Set("Profinet", "Offline");
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
            byte[] data = mNdm.ReadUserData(0, 240);  //读取数据，数据区长度240字节
            //byte[] subDate = data.Skip(5).Take(10).ToArray();
            //string a = string.Join("", data.Select(b => b.ToString()));
            //string channel = e.ChannelIndex.ToString();
            //a = channel + a;
            NewUserData?.Invoke(this, data,e.ChannelIndex);
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
        public void InitFFP()
        {
            //Create the FFP interface.
            try
            {
                if (commType == CommType.Profinet)
                {
                    mNdm = mCard.FfpAccess.CreateNetworkDataModel(CogFfpProtocolConstants.Profinet);
                }
                else if (commType == CommType.EthernetIP)
                {
                    mNdm = mCard.FfpAccess.CreateNetworkDataModel(CogFfpProtocolConstants.EthernetIp);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Could not initialize Comm Card Network Data Model interface." + ex.Message);
                return;
            }

            //Sign up foe input event notification
            AddEventHandlers();

            //Start the NDM
            mNdm.Start();

            // Start a timer to wait for the sample to connect to the PLC
            // Wait for 30 seconds.
            waitForProtocol = new System.Timers.Timer(30000);
            waitForProtocol.Elapsed += new System.Timers.ElapsedEventHandler(waitForProtocol_Elapsed);
            waitForProtocol.Enabled = true;
        }

        /// <summary>
        /// 通讯卡连接失败事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void waitForProtocol_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _isConnected = false;                //无法连接通讯卡
            waitForProtocol.Enabled = false;
        }

        /// <summary>
        /// Called when the user tries to close the application.
        /// </summary>
        public void Close()
        {
            if (mNdm != null)
            {
                // Cancel notification of input events change.
                RemoveEventHandlers();

                // bring the interface down
                mEthernetPort.BringInterfaceDownAsync();

                // Stop the NDM
                mNdm.Stop();
            }
        }
        #endregion
    }
}
