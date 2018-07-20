using System;
using System.Collections.Generic;
using System.Text;

namespace MMONET.Remote
{
    /// <summary>
    /// 框架占用的常量
    /// </summary>
    public class FrameworkConst
    {

        /// <summary>
        /// Udp握手连接使用的消息ID编号
        /// </summary>
        public const int UdpConnectMessageID = 101;
        /// <summary>
        /// 心跳包ID，255好识别，buffer[2-5]=[255,0,0,0]
        /// </summary>
        public const int HeartbeatsMessageID = 255;

    }
}
