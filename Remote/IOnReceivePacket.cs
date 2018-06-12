using System;
using System.Collections.Generic;
using System.Text;

namespace MMONET.Sockets
{
    internal interface IOnReceivePacket
    {
        void OnReceive(IReceivedPacket packet);
    }
}
