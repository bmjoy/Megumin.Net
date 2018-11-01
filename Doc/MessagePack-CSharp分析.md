# MessagePack-CSharp分析

## 序列化

内存分配部分：	
	一次拷贝（开销很小，几乎没有优化必要）	一次内存分配（可以避免）

	public static byte[] Serialize<T>(T obj)
	{
		序列过程中：使用线程不共享byte buffer pool，64K。
		序列完成使用Buffer.BlockCopy拷贝到新buffer ，新的bytebuffer 直接new的，所以有内存分配。
	}
	
	public static ArraySegment<byte> SerializeUnsafe<T>(T obj)
	{
		序列过程中：使用线程不共享byte buffer pool，64K。
		序列完成使用ArraySegment<byte> ，没有额外的内存分配。
		但是因为使用的是线程不共享byte buffer pool，所以不能跨线程发送，而且必须立即使用，下一次序列化内容时将会覆盖数组。
		项目中基本都是异步发送，因此无法使用。
	}
		
## 反序列化

直接new对象
		