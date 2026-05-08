namespace Aron_V2
{
    /// <summary>
    /// 全局 S7 通讯锁。
    /// 后续 4 个通道轮询、心跳、手动读写、结果输出，都必须统一经过这个锁。
    /// </summary>
    public static class S7CommunicationLock
    {
        public static readonly object SyncRoot = new object();
    }
}