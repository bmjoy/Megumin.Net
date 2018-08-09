using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MMONET;

namespace UnitFunc
{
    [TestClass]
    public class UnitTestEx
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

        [TestMethod]
        public void TestBufferPool()
        {
            byte[] buffer = null;
            buffer = BufferPool.Pop(15);
            Assert.AreEqual(32, buffer.Length);
            buffer = BufferPool.Pop(16);
            Assert.AreEqual(32, buffer.Length);
            buffer = BufferPool.Pop(31);
            Assert.AreEqual(32, buffer.Length);
            buffer = BufferPool.Pop(32);
            Assert.AreEqual(32, buffer.Length);
            buffer = BufferPool.Pop(63);
            Assert.AreEqual(64, buffer.Length);
            buffer = BufferPool.Pop(64);
            Assert.AreEqual(64, buffer.Length);
            buffer = BufferPool.Pop(127);
            Assert.AreEqual(128, buffer.Length);
            buffer = BufferPool.Pop(128);
            Assert.AreEqual(128, buffer.Length);
            buffer = BufferPool.Pop(255);
            Assert.AreEqual(256, buffer.Length);
            buffer = BufferPool.Pop(256);
            Assert.AreEqual(256, buffer.Length);
            buffer = BufferPool.Pop(511);
            Assert.AreEqual(512, buffer.Length);
            buffer = BufferPool.Pop(512);
            Assert.AreEqual(512, buffer.Length);
            buffer = BufferPool.Pop(1023);
            Assert.AreEqual(1024, buffer.Length);
            buffer = BufferPool.Pop(1024);
            Assert.AreEqual(1024, buffer.Length);
            buffer = BufferPool.Pop(2047);
            Assert.AreEqual(2048, buffer.Length);
            buffer = BufferPool.Pop(2048);
            Assert.AreEqual(2048, buffer.Length);
            buffer = BufferPool.Pop(4095);
            Assert.AreEqual(4096, buffer.Length);
            buffer = BufferPool.Pop(4096);
            Assert.AreEqual(4096, buffer.Length);
            buffer = BufferPool.Pop(8191);
            Assert.AreEqual(8192, buffer.Length);
            buffer = BufferPool.Pop(8192);
            Assert.AreEqual(8192, buffer.Length);
            buffer = BufferPool.Pop(16383);
            Assert.AreEqual(16384, buffer.Length);
            buffer = BufferPool.Pop(16384);
            Assert.AreEqual(16384, buffer.Length);
            buffer = BufferPool.Pop(16385);
            Assert.AreEqual(16384, buffer.Length);
        }
    }
}
