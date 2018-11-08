using System;
using System.IO;
using Message;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;

namespace UnitFunc
{
    [TestClass]
    public class UnitTestMessage
    {
        [TestMethod]
        public void TestMethod1()
        {
            Login2Gate login2Gate = new Login2Gate()
            {
                Account = "test",
                Password = "123456"
            };

            {
                var b = MessagePack.MessagePackSerializer.Serialize(login2Gate);
                var res = MessagePack.MessagePackSerializer.Deserialize<Login2Gate>(b);
                Assert.AreEqual(login2Gate.Account, res.Account);
                Assert.AreEqual(login2Gate.Password, res.Password);
            }

            {
                using (MemoryStream ms = new MemoryStream(1024))
                {
                    Serializer.Serialize(ms, login2Gate);
                    ms.Seek(0, SeekOrigin.Begin);
                    var res = Serializer.Deserialize<Login2Gate>(ms);
                    Assert.AreEqual(login2Gate.Account, res.Account);
                    Assert.AreEqual(login2Gate.Password, res.Password);
                }
            }
        }
    }
}
