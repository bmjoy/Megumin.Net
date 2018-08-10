using System;
using System.Collections.Generic;

namespace MMONET.Message
{
    ///// <summary>
    ///// 消息类查找表
    ///// </summary>
    //public interface ILookUpTabal
    //{
    //    /// <summary>
    //    /// 反序列化器查找表
    //    /// </summary>
    //    IEnumerable<KeyValuePair<int, Deserilizer>> DeserilizerKV { get; }
    //    /// <summary>
    //    /// 序列化器查找表 Delegate Seiralizer必须是
    //    /// <see cref="MMONET.Sockets.Seiralizer{T}"/>
    //    /// </summary>
    //    IEnumerable<KeyValuePair<Type, (int MessageID, Delegate Seiralizer)>> SeiralizerKV { get; }
    //}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="buffer"></param>
    /// <returns></returns>
    public delegate dynamic Deserilizer(ArraySegment<byte> buffer);
    /// <summary>
    /// 将消息从0位置开始 序列化 到 指定buffer中,返回序列化长度
    /// </summary>
    /// <param name="message">消息实例</param>
    /// <param name="buffer">给定的buffer,长度为16384</param>
    /// <returns>序列化消息的长度</returns>
    public delegate ushort Seiralizer<in T>(T message,byte[] buffer);


}