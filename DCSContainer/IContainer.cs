using System.Threading.Tasks;
using MMONET.Remote;
using Network.Remote;

namespace MMONET.DCS
{
    public interface IContainer
    {
        IRemote Remote { get; }

        Task<rpcResult> Send<rpcResult>(object testMessage);
    }
}