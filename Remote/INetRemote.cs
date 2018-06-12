using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static MMONET.Sockets.Remote;

namespace MMONET.Sockets
{
    internal interface INetRemote
    {
        void ReceiveCallback(int messageID, short rpcID, object msg, RemoteChannal receiveChannal);
        Task BroadCastAsync(ArraySegment<byte> msgBuffer, RemoteChannal type);
    }

    internal interface IChannal : IDisposable, IReceive, IConnect, ISend, IListen
    {
        void SetRemote(Remote remote);

    }
}
