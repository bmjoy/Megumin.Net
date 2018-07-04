using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace System
{

    public static class InterlockedID<T>
    {
        static int id = 0;
        public static int NewID()
        {
            return Interlocked.Increment(ref id);
        }
    }

}
