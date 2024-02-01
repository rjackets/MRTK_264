using System.Collections.Generic;
using UnityEngine;

public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher _instance;
    private Queue<System.Action> _actionsQueue = new Queue<System.Action>();

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static void Enqueue(System.Action action)
    {
        lock (_instance._actionsQueue)
        {
            _instance._actionsQueue.Enqueue(action);
        }
    }

    // Add this property to expose the size of the actions queue
    public static int QueueSize
    {
        get
        {
            lock (_instance._actionsQueue)
            {
                return _instance._actionsQueue.Count;
            }
        }
    }

    private void Update()
    {
        // Following used to show it we are building up in the queue - pretty much should always only be 1
        // as we are clearing the queue after each frame draw
        //Debug.Log($"MainThreadDispatcher queue size: {QueueSize}");


        while (_actionsQueue.Count > 0)
        {
            System.Action action;
            lock (_actionsQueue)
            {
                action = _actionsQueue.Dequeue();
            }
            action?.Invoke();
        }
    }
}
