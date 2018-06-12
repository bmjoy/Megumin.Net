using System;
using System.Collections.Generic;
using System.Text;

namespace MMONET
{
    /// <summary>
    /// 池元素
    /// </summary>
    public interface IPoolElement
    {
        /// <summary>
        /// 返回对象池中
        /// </summary>
        void Push2Pool();
    }
}
