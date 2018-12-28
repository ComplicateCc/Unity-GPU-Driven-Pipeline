using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;
public class LoadingThread : MonoBehaviour
{
    private struct Command
    {
        public object obj;
        public Action<object> func;
    }
    public static LoadingThread current { get; private set; }
    private static List<Command> commands = new List<Command>();
    private List<Command> localCommands = new List<Command>();
    private AutoResetEvent resetEvent;
    private Thread thread;
    private bool isRunning;
    void Awake()
    {
        if (current != null)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(this);
        current = this;
        isRunning = true;
        resetEvent = new AutoResetEvent(false);
        thread = new Thread(Run);
        thread.Start();
    }

    public static void AddCommand(Action<object> act, object obj)
    {
        lock (commands)
        {
            commands.Add(new Command
            {
                obj = obj,
                func = act
            });
            current.resetEvent.Set();
        }
    }
    private void Run()
    {
        while (isRunning)
        {
            resetEvent.WaitOne();
            lock(commands)
            {
                localCommands.AddRange(commands);
                commands.Clear();
            }
            foreach(var i in localCommands)
            {
                i.func(i.obj);   
            }
            localCommands.Clear();
        }
    }

    private void OnDestroy()
    {
        if (current != this) return;
        current = null;
        isRunning = false;
        resetEvent.Set();
        thread.Join();
        resetEvent.Dispose();
    }
}
