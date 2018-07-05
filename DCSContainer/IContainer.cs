using System.Threading.Tasks;
using MMONET.Remote;

namespace MMONET.DCS
{
    public interface IContainer
    {
        IRemote Remote { get; }

        Task<rpcResult> Send<rpcResult>(object testMessage);
    }
}