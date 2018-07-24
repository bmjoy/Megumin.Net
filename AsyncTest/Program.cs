using System;
using System.Threading.Tasks;
using System.Threading;

namespace AsyncTest
{
    class Program
    {
        public static TaskScheduler current { get; private set; }

        static void Main(string[] args)
        {
            //NewMethod1();

            Test3 test3 = new Test3();
            test3.Test2();
            test3.source.TrySetCanceled();

            Console.ReadLine();
        }

        private static void NewMethod1()
        {
            Console.WriteLine("Hello World!");
            //NewMethod();
            Console.WriteLine($"5------当前线程{Thread.CurrentThread.ManagedThreadId}");
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            current = TaskScheduler.FromCurrentSynchronizationContext();
            Test2 test2 = new Test2();
            test2.TestAsync();

            Console.WriteLine($"6------当前线程{Thread.CurrentThread.ManagedThreadId}");
            Thread.Sleep(10000);
            Console.WriteLine($"7------当前线程{Thread.CurrentThread.ManagedThreadId}");
        }

        private static void NewMethod()
        {
            Test test = new Test();
            test.Test1Async();
            Task.Run(() =>
            {
                Console.WriteLine($"1------当前线程{Thread.CurrentThread.ManagedThreadId}");
                Thread.Sleep(1000);
                Console.WriteLine($"4------当前线程{Thread.CurrentThread.ManagedThreadId}");
                test.source.SetResult(0);
                Console.WriteLine($"5------当前线程{Thread.CurrentThread.ManagedThreadId}");
            });
            Thread.Sleep(10000);
            Console.WriteLine($"2------当前线程{Thread.CurrentThread.ManagedThreadId}");
        }
    }

    internal class Test2
    {
        internal async Task TestAsync()
        {
            await Task.Run(()=>
            {
                Console.WriteLine($"1------当前线程{Thread.CurrentThread.ManagedThreadId}");
                Thread.Sleep(1000);
                Console.WriteLine($"2------当前线程{Thread.CurrentThread.ManagedThreadId}");
            }).ContinueWith((task)=> 
            {
                Console.WriteLine($"3------当前线程{Thread.CurrentThread.ManagedThreadId}");
            }, Program.current);
            Console.WriteLine($"4------当前线程{Thread.CurrentThread.ManagedThreadId}");


            await Test11111();
        }

        private Task Test11111()
        {
            return null;
        }
    }

    public class Test
    {
        public TaskCompletionSource<int> source;

        public async Task Test1Async()
        {
            Console.WriteLine($"6------当前线程{Thread.CurrentThread.ManagedThreadId}");
            int a = await Test2();
            Console.WriteLine($"3------当前线程{Thread.CurrentThread.ManagedThreadId}");
        }

        private Task<int> Test2()
        {
            source = new TaskCompletionSource<int>();
            return source.Task;
        }
    }

    public class Test3
    {
        public TaskCompletionSource<int> source;

        public Task<int> Test1Async()
        {
            source = new TaskCompletionSource<int>();
            ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.Sleep(10000);
                source.SetResult(1);
            });
            return source.Task;
        }

        public async void Test2()
        {
            int v = await Test1Async();
            Console.WriteLine(v);
        }
    }


    public class SocketTest
    {
        public void Send()
        {

        }
    }
}
