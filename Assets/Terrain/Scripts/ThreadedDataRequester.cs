using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;

public class ThreadedDataRequester : MonoBehaviour
{
    static ThreadedDataRequester instance;
    Queue<ThreadInfo> dataQueue = new Queue<ThreadInfo>();
    Queue<Action> workQueue = new Queue<Action>();
    private bool isRunning = true;
    private List<Thread> threadPool = new List<Thread>();
    private readonly object workQueueLock = new object();
    private readonly object dataQueueLock = new object();

    void Awake()
    {
        instance = this;
        int threadCount = System.Environment.ProcessorCount;
        for (int i = 0; i < threadCount; i++)
        {
            Thread thread = new Thread(Worker);
            thread.Start();
            threadPool.Add(thread);
        }
    }

    public static void RequestData(Func<object> generateData, Action<object> callback)
    {
        Action work = () => {
            object data = generateData();
            lock (instance.dataQueueLock)
            {
                instance.dataQueue.Enqueue(new ThreadInfo(callback, data));
            }
        };

        lock (instance.workQueueLock)
        {
            instance.workQueue.Enqueue(work);
        }
    }
    
    private void Worker()
    {
        while (isRunning)
        {
            Action work = null;
            lock (workQueueLock)
            {
                if (workQueue.Count > 0)
                {
                    work = workQueue.Dequeue();
                }
            }

            if (work != null)
            {
                work();
            }
            else
            {
                Thread.Sleep(1); // Sleep to prevent busy waiting
            }
        }
    }

    void Update()
    {
        lock (dataQueueLock)
        {
            while (dataQueue.Count > 0)
            {
                ThreadInfo threadInfo = dataQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    void OnDestroy()
    {
        isRunning = false;
        foreach(var thread in threadPool)
        {
            thread.Join();
        }
    }

    struct ThreadInfo 
    {
        public readonly Action<object> callback;
        public readonly object parameter;

        public ThreadInfo(Action<object> callback, object parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}
