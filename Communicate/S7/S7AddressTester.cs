using System;
using System.Text;
using HslCommunication;
using HslCommunication.Profinet.Siemens;

namespace Aron_V2
{
    public class S7AddressTester : IDisposable
    {
        private SiemensS7Net _client;
        private S7BasicSetting _basic;

        public bool IsConnected
        {
            get { return _client != null; }
        }

        public S7AddressTester(S7BasicSetting basic)
        {
            if (basic == null)
                throw new ArgumentNullException("basic");

            _basic = basic;
        }

        public OperateResult Connect()
        {
            lock (S7CommunicationLock.SyncRoot)
            {
                try
                {
                    CloseWithoutLock();

                    SiemensPLCS plcType = ConvertPlcType(_basic.PlcType);
                    _client = new SiemensS7Net(plcType, _basic.IpAddress);

                    _client.Port = _basic.Port;
                    _client.Rack = _basic.Rack;
                    _client.Slot = _basic.Slot;
                    _client.ConnectTimeOut = _basic.ConnectTimeoutMs;

                    OperateResult result = _client.ConnectServer();
                    if (!result.IsSuccess)
                        CloseWithoutLock();

                    return result;
                }
                catch (Exception ex)
                {
                    CloseWithoutLock();
                    return new OperateResult(ex.Message);
                }
            }
        }

        public void Close()
        {
            lock (S7CommunicationLock.SyncRoot)
            {
                CloseWithoutLock();
            }
        }

        private void CloseWithoutLock()
        {
            try
            {
                if (_client != null)
                {
                    _client.ConnectClose();
                    _client = null;
                }
            }
            catch
            {
                _client = null;
            }
        }

        public OperateResult<string> ReadAsString(string address, S7PointDataType dataType, ushort stringLength)
        {
            lock (S7CommunicationLock.SyncRoot)
            {
                try
                {
                    if (_client == null)
                        return new OperateResult<string>("S7 is not connected.");

                    switch (dataType)
                    {
                        case S7PointDataType.Bool:
                            {
                                OperateResult<bool> result = _client.ReadBool(address);
                                if (!result.IsSuccess) return OperateResult.CreateFailedResult<string>(result);
                                return OperateResult.CreateSuccessResult(result.Content.ToString());
                            }

                        case S7PointDataType.Byte:
                            {
                                OperateResult<byte[]> result = _client.Read(address, 1);
                                if (!result.IsSuccess) return OperateResult.CreateFailedResult<string>(result);
                                if (result.Content == null || result.Content.Length == 0)
                                    return new OperateResult<string>("Read byte failed: content is empty.");
                                return OperateResult.CreateSuccessResult(result.Content[0].ToString());
                            }

                        case S7PointDataType.Int16:
                            {
                                OperateResult<short> result = _client.ReadInt16(address);
                                if (!result.IsSuccess) return OperateResult.CreateFailedResult<string>(result);
                                return OperateResult.CreateSuccessResult(result.Content.ToString());
                            }

                        case S7PointDataType.Int32:
                            {
                                OperateResult<int> result = _client.ReadInt32(address);
                                if (!result.IsSuccess) return OperateResult.CreateFailedResult<string>(result);
                                return OperateResult.CreateSuccessResult(result.Content.ToString());
                            }

                        case S7PointDataType.Float:
                            {
                                OperateResult<float> result = _client.ReadFloat(address);
                                if (!result.IsSuccess) return OperateResult.CreateFailedResult<string>(result);
                                return OperateResult.CreateSuccessResult(result.Content.ToString("0.######"));
                            }

                        case S7PointDataType.Double:
                            {
                                OperateResult<double> result = _client.ReadDouble(address);
                                if (!result.IsSuccess) return OperateResult.CreateFailedResult<string>(result);
                                return OperateResult.CreateSuccessResult(result.Content.ToString("0.######"));
                            }

                        case S7PointDataType.String:
                            {
                                OperateResult<string> result = _client.ReadString(address, stringLength);
                                if (!result.IsSuccess) return OperateResult.CreateFailedResult<string>(result);
                                return OperateResult.CreateSuccessResult(result.Content);
                            }

                        default:
                            return new OperateResult<string>("Unsupported data type: " + dataType.ToString());
                    }
                }
                catch (Exception ex)
                {
                    return new OperateResult<string>(ex.Message);
                }
            }
        }

        public OperateResult WriteByString(string address, S7PointDataType dataType, string value)
        {
            lock (S7CommunicationLock.SyncRoot)
            {
                try
                {
                    if (_client == null)
                        return new OperateResult("S7 is not connected.");

                    if (value == null)
                        value = string.Empty;

                    switch (dataType)
                    {
                        case S7PointDataType.Bool:
                            return _client.Write(address, ParseBool(value));

                        case S7PointDataType.Byte:
                            return _client.Write(address, new byte[] { ParseByte(value) });

                        case S7PointDataType.Int16:
                            return _client.Write(address, Convert.ToInt16(value));

                        case S7PointDataType.Int32:
                            return _client.Write(address, Convert.ToInt32(value));

                        case S7PointDataType.Float:
                            return _client.Write(address, Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture));

                        case S7PointDataType.Double:
                            return _client.Write(address, Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture));

                        case S7PointDataType.String:
                            return _client.Write(address, value);

                        default:
                            return new OperateResult("Unsupported data type: " + dataType.ToString());
                    }
                }
                catch (Exception ex)
                {
                    return new OperateResult(ex.Message);
                }
            }
        }

        public OperateResult WriteHeartbeatOnce()
        {
            if (_basic == null)
                return new OperateResult("S7 basic setting is null.");

            return WriteHeartbeatChar(_basic.HeartbeatAddress, _basic.HeartbeatValue);
        }

        public OperateResult WriteHeartbeatChar(string address, string value)
        {
            lock (S7CommunicationLock.SyncRoot)
            {
                try
                {
                    if (_client == null)
                        return new OperateResult("S7 is not connected.");

                    if (string.IsNullOrWhiteSpace(address))
                        return new OperateResult("Heartbeat address is empty.");

                    if (string.IsNullOrEmpty(value))
                        value = "A";

                    char ch = value[0];
                    byte[] bytes = Encoding.ASCII.GetBytes(new char[] { ch });

                    return _client.Write(address.Trim(), bytes);
                }
                catch (Exception ex)
                {
                    return new OperateResult(ex.Message);
                }
            }
        }

        public static S7PointDataType ParseDataType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return S7PointDataType.Bool;

            try
            {
                return (S7PointDataType)Enum.Parse(typeof(S7PointDataType), typeName.Trim(), true);
            }
            catch
            {
                return S7PointDataType.Bool;
            }
        }

        public static SiemensPLCS ConvertPlcType(string plcType)
        {
            if (string.IsNullOrWhiteSpace(plcType))
                return SiemensPLCS.S1200;

            switch (plcType.Trim().ToUpper())
            {
                case "S200SMART":
                    return SiemensPLCS.S200Smart;

                case "S300":
                    return SiemensPLCS.S300;

                case "S400":
                    return SiemensPLCS.S400;

                case "S1500":
                    return SiemensPLCS.S1500;

                case "S1200":
                default:
                    return SiemensPLCS.S1200;
            }
        }

        private static bool ParseBool(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string v = value.Trim().ToLower();
            return v == "1" || v == "true" || v == "on" || v == "yes";
        }

        private static byte ParseByte(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            string v = value.Trim();

            if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Convert.ToByte(v.Substring(2), 16);

            if (v.Length == 1 && !char.IsDigit(v[0]))
                return Encoding.ASCII.GetBytes(v)[0];

            return Convert.ToByte(v);
        }

        public void Dispose()
        {
            Close();
        }
    }
}