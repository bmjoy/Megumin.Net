using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Text;
using Network.Remote;
using IRemoteDic = MMONET.IDictionary<int, System.Net.IPEndPoint, Network.Remote.IRemote>;

namespace MMONET.Remote
{
    public static class RemotePool
    {
        static RemotePool()
        {
            MainThreadScheduler.Add(StaticUpdate);
        }

        /// <summary>
        /// remote 在构造函数中自动添加到RemoteDic 中,需要手动移除 或者调用<see cref="IConnect.Disconnect(bool)"/> + <see cref="MainThreadScheduler.Update(double)"/>移除。
        /// </summary>
        static IRemoteDic remoteDic = new K2Dictionary<int, IPEndPoint, IRemote>();
        /// <summary>
        /// 添加队列，防止多线程阻塞
        /// </summary>
        static readonly ConcurrentQueue<IRemote> tempAddQ = new ConcurrentQueue<IRemote>();

        public static readonly CoolDownTime UpdateDelta = new CoolDownTime();
        public static readonly CoolDownTime CheckRemoveUpdateDelta = new CoolDownTime() { MinDelta = TimeSpan.FromSeconds(2) };
        static void StaticUpdate(double delta)
        {
            if (UpdateDelta.CoolDown)
            {
                lock (remoteDic)
                {
                    foreach (var item in remoteDic)
                    {
                        if (!item.Value.IsVaild)
                        {
                            continue;
                        }

                        if (item.Value is IUpdateRpcResult rpcResult)
                        {
                            rpcResult.UpdateRpcResult(delta);
                        }
                    }
                }

                while (tempAddQ.Count > 0)
                {
                    if (tempAddQ.TryDequeue(out var remote))
                    {
                        if (remote != null)
                        {
                            if (remoteDic.ContainsKey(remote.Guid) || remoteDic.ContainsKey(remote.IPEndPoint))
                            {
                                ///理论上不会冲突
                                Console.WriteLine($"remoteDic 键值冲突");
                            }
                            remoteDic[remote.Guid,remote.IPEndPoint] = remote;
                        }
                    }
                }
            }

            if (CheckRemoveUpdateDelta.CoolDown)
            {
                ///移除释放的连接
                remoteDic.RemoveAll(r => !r.Value.IsVaild);
            }
        }

        public static void Add(IRemote remote)
        {
            tempAddQ.Enqueue(remote);
        }

        public static void AddToPool(this IRemote remote)
        {
            Add(remote);
        }

        public static bool TryGet(int Guid, out IRemote remote)
        {
            return remoteDic.TryGetValue(Guid, out remote);
        }

        public static bool TryGet(IPEndPoint ep, out IRemote remote)
        {
            return remoteDic.TryGetValue(ep, out remote);
        }
    }
}
