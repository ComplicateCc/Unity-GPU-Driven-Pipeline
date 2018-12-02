using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;
public class LoadingThread : MonoBehaviour
{
    public static LoadingThread current { get; private set; }
    private static List<Action> commands = new List<Action>();
    private List<Action> localCommands = new List<Action>();
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

    public static void AddCommand(Action act)
    {
        lock (commands)
        {
            commands.Add(act);
            current.resetEvent.Set();
        }
    }
    public static void AddCommands(Action[] acts)
    {
        lock (commands)
        {
            commands.AddRange(acts);
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
                i();   
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
