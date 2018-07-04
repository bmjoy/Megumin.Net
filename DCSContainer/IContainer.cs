using System.Threading.Tasks;
using MMONET.Sockets;

namespace MMONET.DCS
{
    public interface IContainer
    {
        IRemote Remote { get; }

        Task<rpcResult> Send<rpcResult>(object testMessage);
    }
}