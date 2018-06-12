using System.Threading.Tasks;

namespace MMONET.Sockets
{
    internal interface IListen
    {
        Task<IChannal> ListenAsync(int mainPort);
    }
}