using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NetworkObjectManager : MonoBehaviour
{
    private Dictionary<string, NetworkObject> networkObjects =
        new Dictionary<string, NetworkObject>();

    void Awake()
    {
        RefreshNetworkObjects();
    }

    public void RefreshNetworkObjects()
    {
        NetworkObject[] objects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        networkObjects.Clear();

        foreach (var obj in objects)
        {
            if (!string.IsNullOrEmpty(obj.objectId))
            {
                networkObjects[obj.objectId] = obj;
            }
        }
    }

    public IEnumerable<NetworkObject> GetAllNetworkObjects()
    {
        return networkObjects.Values;
    }

    void Start()
    {
        NetworkManager nm = FindFirstObjectByType<NetworkManager>();
        if (nm != null)
        {
            nm.RegisterNetworkObjectManager(this);
        }
    }

    public void RegisterNetworkObject(NetworkObject obj)
    {
        if (obj == null || string.IsNullOrEmpty(obj.objectId))
            return;

        if (!networkObjects.ContainsKey(obj.objectId))
        {
            networkObjects[obj.objectId] = obj;
        }
    }

    public void UnregisterNetworkObject(string objectId)
    {
        if (networkObjects.ContainsKey(objectId))
        {
            networkObjects.Remove(objectId);
        }
    }

    public NetworkObject GetNetworkObject(string objectId)
    {
        return networkObjects.ContainsKey(objectId) ? networkObjects[objectId] : null;
    }

    public int GetTrackedObjectCount()
    {
        return networkObjects.Count;
    }
}
