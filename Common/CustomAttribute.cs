using System;
using System.Collections.Generic;
using System.Text;

namespace System
{
    /// <summary>
    /// 在多个重载方法中建议使用此方法
    /// <para>此属性仅为标记使用，没有实际意义</para>
    /// </summary>
    public class PopularAttribute : Attribute
    {

    }

    /// <summary>
    /// 在多个重载方法中有真正实现的方法 / 汇总方法 / 适合断点的方法
    /// <para>此属性仅为标记使用，没有实际意义</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class HubMethodAttribute : Attribute
    {

    }
}
