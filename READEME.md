# 这是什么？  
  这是一个dotnetStandard2.0的网络库。设计目的为应用程序网络层提供统一的简单的接口。NetRemoteStandard.dll提供了API定义，Megumin.Remote.dll是它的具体实现。应用程序可以使用NetRemoteStandard.dll编码，然后使用Megumin.Remote.dll里的具体实现类注入，当需要切换协议或者序列化类库时，应用程序逻辑无需改动。

  **简单来说：NetRemoteStandard是标准，Megumin.Remote是实现。类比于dotnetStandard和dotnetCore的关系。**

  **Megumin.Remote是以MMORPG为目标实现的。对于非MMORPG可能不是最佳选择。** 在遥远的未来也许会针对不用游戏类型写出NetRemoteStandard的不同实现。

## [``路线图``](https://trello.com/b/KkikpHim/meguminnet)

# 它是开箱即用的么？
是的。但是注意，需要搭配序列化库，不同的序列化库可能有额外的要求。

# 核心方法3个

设计原则：最常用的代码最简化，复杂的地方都封装起来。  
发送一个消息，等待一个消息返回。

## IRpcSendMessage.SendAsync

    ///实际使用中的例子
    public async void TestSend()
    {
        Person person = new Person() { Name = "LiLei", Age = 10 };
        IRemote remote = new TCPRemote();
        ///省略连接代码
        ///                                         泛型类型为期待返回的类型
        var (result, exception) = await remote.SendAsync<TestPacket1>(person);
        ///如果没有遇到异常，那么我们可以得到远端发回的返回值
        if (exception == null)
        {
            Console.WriteLine(result);
        }
    }

## ISafeAwaitSendMessage.SendAsyncSafeAwait
方法签名： IMiniAwaitable<RpcResult> SendAsyncSafeAwait<RpcResult>(object message, Action<Exception> OnException = null);  
结果值是保证有值的，如果结果值为空或其他异常,触发异常回调函数，不会抛出异常，所以不用try catch。异步方法的后续部分不会触发，所以后续部分可以省去空检查。  
（注意：这依赖于具体Remote实现）

    public async void TestSend()
    {
        Person person = new Person() { Name = "LiLei", Age = 10 };
        IRemote remote = new TCPRemote();
        ///省略连接代码
        ///                                         泛型类型为期待返回的类型
        var testPacket1 = await remote.SendAsyncSafeAwait<TestPacket1>(person);
        ///后续代码 不用任何判断，也不用担心异常。
        Console.WriteLine(testPacket1);
    }

## ``public delegate ValueTask<object> ReceiveCallback (object message,IReceiveMessage receiver);``
接收端回调函数

    public static async ValueTask<object> DealMessage(object message,IReceiveMessage receiver)
    {
        switch (message)
        {
            case TestPacket1 packet1:
                Console.WriteLine($"接收消息{nameof(TestPacket1)}--{packet1.Value}"); 
                return null;
            case TestPacket2 packet2:
                Console.WriteLine($"接收消息{nameof(TestPacket2)}--{packet2.Value}");
                return new TestPacket2 { Value = packet2.Value };
            default:
                break;
        }
        return null;
    }

# 重要
- **线程调度**  
  Remote 使用MessagePipeline.Post2ThreadScheduler标志决定消息回调函数在哪个线程执行，true时所有消息被汇总到Megumin.ThreadScheduler.Update。  
  你需要轮询此函数来处理接收回调，它保证了按接收消息顺序触发回调（如果出现乱序，请提交一个BUG）。false时接收消息回调使用Task执行，不保证有序。  
  
        ///建立主线程 或指定的任何线程 轮询。（确保在unity中使用主线程轮询）
        ///ThreadScheduler保证网络底层的各种回调函数切换到主线程执行以保证执行顺序。
        ThreadPool.QueueUserWorkItem((A) =>
        {
            while (true)
            {
                ThreadScheduler.Update(0);
                Thread.Yield();
            }
        });

- **``Message.dll``**  
  [（AOT/IL2CPP）当序列化类以dll的形式导入unity时，必须加入link文件，防止序列化类属性的get,set方法被il2cpp剪裁。](https://docs.unity3d.com/Manual/IL2CPP-BytecodeStripping.html)**``重中之重，因为缺失get,set函数不会显示报错，错误通常会被定位到序列化库的多个不同位置（我在这里花费了16个小时）。``** 

        <linker>
            <assembly fullname="Message" preserve="all"/>
        </linker>

# MessagePipeline是什么？
MessagePipeline 是 Megumin.Remote 的一部分功能，MessagePipeline 不包含在NetRemoteStandard中。  
它决定了消息收发具体经过了那些流程，可以自定义MessagePipeline并注入到Remote,用来满足一些特殊需求。  
如，消息反序列化前转发；使用返回消息池来实现接收过程构造返回消息实例无Alloc（这需要序列化类库的支持和明确的生命周期管理）。
``你可以为每个Remote指定一个MessagePipeline实例，如果没有指定，默认使用MessagePipeline.Default。``

# 一些细节
- Megumin只是个前缀，没有含义。
- 内置了RPC功能，保证了请求和返回消息一对一匹配。
- 内置了内存池，发送过程是全程无Alloc的，接收过程构造返回消息实例需要Alloc。
- 发送过程数据拷贝了1次，接收过程数据无拷贝。
- 内置内存池在初始状态就会分配一些内存（大约150KB）。随着使用继续扩大，最大到3MB左右，详细情况参考源码。目前不支持配置大小。
- 序列化时使用type做Key查找函数，反序列化时使用MSGID(int)做Key查找函数。
- 内置了string,int,long,float,double 5个内置类型，即使不使用序列化类库，也可以直接发送它们。你可以使用MessageLUT.Regist<T>函数添加其他类型。

# 支持的序列化库(陆续添加中)
每个库有各自的限制，对IL2CPP支持也不同。框架会为每个支持的库写一个兼容于MessageStandard/MessageLUT的dll.  
由于各个序列化库对Span\<byte>的支持不同，所以中间层可能会有轻微的性能损失.

对于序列化函数有三种形式：
1. 代码生成器生成代码   
   { protobuf ，[MessagePack mpc.exe](https://github.com/neuecc/MessagePack-CSharp#pre-code-generationunityxamarin-supports) }
2. 通过反射每个字段组合   
   { protobuf-net .NET Standard 1.0 }
3. JIT 生成  
   { protobuf-net ， MessagePack}

## [protobuf-net](https://github.com/mgravell/protobuf-net)
- IL2CPP 请使用[.NET Standard 1.0](https://github.com/mgravell/protobuf-net#supported-runtimes)，其他运行时可能无法构建。虽然是反射模式，但是对于客户端来说并没有性能问题，于此同时服务器可以使用 .NET Standard 2.0。  
  unity无头模式服务器应该考虑其他库。

## [protobuf](https://github.com/protocolbuffers/protobuf)

## [MessagePack](https://github.com/neuecc/MessagePack-CSharp)

# 效率
没有精确测试，Task的使用确实影响了一部分性能，但是是值得的。经过简单测试和个人经验判断可以支持WOW级别的MMORPG游戏。
本机测试单进程维持了15000 + Tcp连接。

# 其他信息
写框架途中总结到的知识或者猜测。
- public virtual MethodInfo MakeGenericMethod(params Type[] typeArguments);  
  在IL2CPP下可用，但是不能创造新方法。如果这个泛型方法在编译期间确定，那么此方法可用。否则找不到方法。