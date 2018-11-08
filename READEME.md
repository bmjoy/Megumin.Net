# 这是什么？  
  这是一个dotnetStandard2.0的网络库。设计目的为应用程序网络层提供统一的简单的接口。NetRemoteStandard.dll提供了API定义，Megumin.Remote.dll是它的具体实现。应用程序可以使用NetRemoteStandard.dll编码，然后使用Megumin.Remote.dll里的具体实现类注入，当需要切换协议或者序列化类库时，应用程序逻辑无需改动。

  **简单来说：NetRemoteStandard是标准，Megumin.Remote是实现。类比于dotnetStandard和dotnetCore的关系。**

  **Megumin.Remote是以MMORPG为目标实现的。对于非MMORPG可能不是最佳选择。** 在遥远的未来也许会针对不用游戏类型写出NetRemoteStandard的不同实现。

## [``路线图``](https://trello.com/b/KkikpHim/meguminnet)

# 它是开箱即用的么？
是的。但是注意，序列化库可能有额外的要求。

# 核心方法2个

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
        ISuperRemote remote = new TCPRemote();
        ///省略连接代码
        var testPacket1 = await remote.SendAsyncSafeAwait<TestPacket1>(person);
        ///后续代码 不用任何判断，也不用担心异常。
        Console.WriteLine(testPacket1);
    }

# 重要
- **线程调度**  
  Remote 使用MessagePipeline.Post2ThreadScheduler标志决定消息回调函数在哪个线程执行，true时所有消息被汇总到Megumin.ThreadScheduler.Update。  
  你需要轮询此函数来处理接收回调，它保证了按接收消息顺序触发回调（如果出现乱序，请提交一个BUG）。false时接收消息回调使用Task执行，不保证有序。  
  
  你可以为每个Remote指定一个MessagePipeline实例，如果没有指定，默认使用MessagePipeline.Default。

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

# MessagePipeline是什么？
MessagePipeline 是 Megumin.Remote 的一部分功能，MessagePipeline 不包含在NetRemoteStandard中。  
它决定了消息收发具体经过了那些流程，可以自定义MessagePipeline并注入到Remote,用来满足一些特殊需求。  
如，消息反序列化前转发；使用返回消息池来实现接收过程构造返回消息实例无Alloc（这需要序列化类库的支持和明确的生命周期管理）。

# 一些细节
- Megumin只是个前缀，没有含义。
- 内置了RPC功能，保证了请求和返回消息一对一匹配。
- 内置了内存池，发送过程是全程无Alloc的，接收过程构造返回消息实例需要Alloc。
- 发送过程数据拷贝了1次，接收过程数据无拷贝。
- 内置内存池在初始状态就会分配一些内存，所起即使很小的示例程序也会占用较多内存，目前没有提供设置选项。

# 支持的序列化库
每个库有各自的限制，对IL2CPP支持也不同。框架会为每个支持的库写一个兼容于MessageStandard/MessageLUT的dll.  
由于各个序列化库对Span\<byte>的支持不同，所以中间层可能会有轻微的性能损失.

## [protobuf-net](https://github.com/mgravell/protobuf-net)
- IL2CPP 请使用[.NET Standard 1.0](https://github.com/mgravell/protobuf-net#supported-runtimes)，其他运行时可能无法构建。

## [protobuf](https://github.com/protocolbuffers/protobuf)

## [MessagePack](https://github.com/neuecc/MessagePack-CSharp)

# 效率
没有精确测试，Task的使用确实影响了一部分性能，但是是值得的。经过简单测试和个人经验判断可以支持WOW级别的MMORPG游戏。
本机测试维持了15000 + Tcp连接。

# 其他信息
写框架途中总结到的知识或者猜测。
- public virtual MethodInfo MakeGenericMethod(params Type[] typeArguments);  
  在IL2CPP下可用，但是不能创造新方法。如果这个泛型方法在编译期间确定，那么此方法可用。否则找不到方法。