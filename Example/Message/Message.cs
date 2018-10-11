using System;
using MessagePack;
using Megumin.Message;

namespace Message
{
    [MessagePackObject, MSGID(1000)]
    public class Message
    {
    }

    [MessagePackObject, MSGID(1001)]
    public class Login
    {
        [Key(0)]
        public string IP { get; set; }
    }

    [MessagePackObject, MSGID(1002)]
    public class LoginResult
    {
        [Key(0)]
        public string TempKey { get; set; }
    }

    [MessagePackObject, MSGID(1003)]
    public class Login2Gate
    {
        [Key(0)]
        public string Account { get; set; }
        [Key(1)]
        public string Password { get; set; }
    }

    [MessagePackObject, MSGID(1004)]
    public class Login2GateResult
    {
        [Key(0)]
        public bool IsSuccess { get; set; }
    }
}
