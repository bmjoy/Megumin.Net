using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        [TestMethod]
        public void TestWaitAsync()
        {
            Wait().Wait();
        }

        private static async Task Wait()
        {
            var c = await Task.Delay(100).WaitAsync(150);
            Assert.AreEqual(true, c);
            var c2 = await Task.Delay(200).WaitAsync(150);
            Assert.AreEqual(false, c2);
        }
    }
}
