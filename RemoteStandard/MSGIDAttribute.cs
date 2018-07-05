using System;
using System.Collections.Generic;
using System.Text;

namespace MMONET.Message
{
    /// <summary>
    /// 使用MessageID来为每一个消息指定一个唯一ID
    /// </summary>
    public sealed class MSGID : Attribute
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="attribute"></param>
        public static implicit operator int(MSGID attribute) => attribute.ID;

        /// <summary>
        /// 消息ID
        /// </summary>
        /// <param name="id"></param>
        public MSGID(int id)
        {
            this.ID = id;
        }

        /// <summary>
        /// 消息类唯一编号
        /// </summary>
        public int ID { get; }

        //public static implicit operator MSGIDAttribute(int id)
        //{
        //    return new MSGIDAttribute(id);
        //}
    }
}
