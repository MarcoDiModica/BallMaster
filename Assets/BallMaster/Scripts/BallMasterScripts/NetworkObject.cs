using UnityEngine;
using System;

public class NetworkObject : MonoBehaviour
{
    public string objectId;
    
    public event Action<Vector3, Quaternion> OnStateUpdated;

    public bool isDirty = false;

    public void MarkDirty()
    {
        isDirty = true;
    }

    public void UpdateState(Vector3 pos, Quaternion rot)
    {
        OnStateUpdated?.Invoke(pos, rot);
    }
}