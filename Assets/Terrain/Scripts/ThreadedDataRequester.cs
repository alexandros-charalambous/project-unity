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
    private bool isRunning;
    private List<Thread> threadPool = new List<Thread>();
    private readonly object workQueueLock = new object();
    private readonly object dataQueueLock = new object();
    private readonly AutoResetEvent workAvailable = new AutoResetEvent(false);

    // Processing every completed job in a single frame can cause large hitches when
    // many terrain chunks finish generating at once.
    const int maxCallbacksPerFrame = 4;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            // Keep the first instance; duplicates can happen if multiple scenes/prefabs include this.
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }

        EnsureStarted();
    }

    void OnEnable()
    {
        if (instance == null) instance = this;
        EnsureStarted();
    }

    private void EnsureStarted()
    {
        if (isRunning) return;

        isRunning = true;

        // Avoid oversubscription; Unity main thread also needs CPU time.
        int threadCount = Mathf.Clamp(System.Environment.ProcessorCount - 1, 1, 8);
        for (int i = 0; i < threadCount; i++)
        {
            Thread thread = new Thread(Worker);
            thread.IsBackground = true;
            thread.Start();
            threadPool.Add(thread);
        }
    }

    private static ThreadedDataRequester EnsureInstance()
    {
        if (instance != null) return instance;
        if (!Application.isPlaying) return null;

        // Find an existing instance in the active scene.
        instance = FindAnyObjectByType<ThreadedDataRequester>();
        if (instance != null)
        {
            instance.EnsureStarted();
            return instance;
        }

        // Auto-create one if missing.
        var go = new GameObject("ThreadedDataRequester");
        instance = go.AddComponent<ThreadedDataRequester>();
        return instance;
    }

    public static void RequestData(Func<object> generateData, Action<object> callback)
    {
        var inst = EnsureInstance();
        if (inst == null)
        {
            Debug.LogError("ThreadedDataRequester is not present in the scene.");
            return;
        }

        Action work = () =>
        {
            object data = null;
            try
            {
                data = generateData();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            lock (instance.dataQueueLock)
            {
                instance.dataQueue.Enqueue(new ThreadInfo(callback, data));
            }
        };

        lock (inst.workQueueLock)
        {
            inst.workQueue.Enqueue(work);
        }

        inst.workAvailable.Set();
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
                workAvailable.WaitOne(50);
            }
        }
    }

    void Update()
    {
        // Dequeue a small, bounded number of callbacks per frame.
        for (int i = 0; i < maxCallbacksPerFrame; i++)
        {
            ThreadInfo threadInfo;
            lock (dataQueueLock)
            {
                if (dataQueue.Count == 0) return;
                threadInfo = dataQueue.Dequeue();
            }
            threadInfo.callback(threadInfo.parameter);
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        isRunning = false;

        // Wake all workers so they can exit.
        for (int i = 0; i < threadPool.Count; i++)
        {
            workAvailable.Set();
        }

        foreach (var thread in threadPool)
        {
            if (thread == null) continue;
            if (!thread.IsAlive) continue;
            thread.Join(200);
        }

        threadPool.Clear();
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
