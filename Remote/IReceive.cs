namespace MMONET.Sockets
{
    internal interface IReceive
    {
        bool IsReceive { get; }

        void Receive();
    }
}