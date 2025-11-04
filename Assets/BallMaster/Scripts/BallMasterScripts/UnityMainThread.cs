using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThread : MonoBehaviour
{
    private static UnityMainThread instance;
    private static readonly Queue<Action> actions = new Queue<Action>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    void Update()
    {
        lock (actions)
        {
            while (actions.Count > 0)
                actions.Dequeue()?.Invoke();
        }
    }

    public static void ExecuteInUpdate(Action action)
    {
        lock (actions)
        {
            actions.Enqueue(action);
        }
    }
}