using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace UnitFunc
{
    [TestClass]
    public class UnitTestDicEx
    {
        [TestMethod]
        public void TestRemoveAll()
        {
            Dictionary<int, int> test = new Dictionary<int, int>();
            test.Add(1, 1);
            test.Add(2, 2);
            test.Add(3, 2);
            test.Add(4, 3);

            Func<KeyValuePair<int,int>,bool> predicate = (kv) =>
            {
                return kv.Value >= 2;
            };

            test.RemoveAll(predicate);
            Assert.AreEqual(false,test.Any(predicate));
        }
    }
}
