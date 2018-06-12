using System.Threading.Tasks;
using MMONET.Sockets;

namespace MMONET.DCS
{
    public interface IContainer
    {
        Sockets.Remote Remote { get; }

        Task<rpcResult> Send<rpcResult>(object testMessage);
    }
}