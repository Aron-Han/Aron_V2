using System;
using System.Threading;
using HslCommunication;

namespace Aron_V2
{
    public class S7HeartbeatService : IDisposable
    {
        private readonly S7AddressTester _tester;
        private readonly S7BasicSetting _basic;
        private Timer _timer;
        private bool _isRunning;

        public bool IsRunning
        {
            get { return _isRunning; }
        }

        public event Action<string> LogMessage;

        public S7HeartbeatService(S7AddressTester tester, S7BasicSetting basic)
        {
            if (tester == null)
                throw new ArgumentNullException("tester");

            if (basic == null)
                throw new ArgumentNullException("basic");

            _tester = tester;
            _basic = basic;
        }

        public void Start()
        {
            if (!_basic.HeartbeatEnable)
            {
                WriteLog("Heartbeat is disabled.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_basic.HeartbeatAddress))
            {
                WriteLog("Heartbeat address is empty.");
                return;
            }

            if (_basic.HeartbeatIntervalMs <= 0)
                _basic.HeartbeatIntervalMs = 1000;

            Stop();

            _timer = new Timer(TimerCallback, null, 0, _basic.HeartbeatIntervalMs);
            _isRunning = true;

            WriteLog("Heartbeat started. Address=" + _basic.HeartbeatAddress
                + ", Value=" + _basic.HeartbeatValue
                + ", Interval=" + _basic.HeartbeatIntervalMs.ToString() + "ms");
        }

        public void Stop()
        {
            try
            {
                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                }

                if (_isRunning)
                    WriteLog("Heartbeat stopped.");

                _isRunning = false;
            }
            catch
            {
                _timer = null;
                _isRunning = false;
            }
        }

        private void TimerCallback(object state)
        {
            try
            {
                OperateResult result = _tester.WriteHeartbeatOnce();
                if (!result.IsSuccess)
                {
                    WriteLog("Heartbeat write failed: " + result.Message);
                }
            }
            catch (Exception ex)
            {
                WriteLog("Heartbeat exception: " + ex.Message);
            }
        }

        private void WriteLog(string message)
        {
            Action<string> handler = LogMessage;
            if (handler != null)
                handler(message);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}