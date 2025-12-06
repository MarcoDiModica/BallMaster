using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NetworkObjectManager : MonoBehaviour
{
    private Dictionary<string, NetworkObject> networkObjects = new Dictionary<string, NetworkObject>();

    void Awake()
    {
        RefreshNetworkObjects();
        Debug.Log($"NetworkObjectManager initialized with {networkObjects.Count} objects.");
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
        {
            Debug.LogWarning("Attempted to register invalid NetworkObject");
            return;
        }

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

    public void ApplyGameState(GameStateData state)
    {
        foreach (ObjectState objState in state.objects)
        {
            if (networkObjects.ContainsKey(objState.objectId))
            {
                networkObjects[objState.objectId].UpdateState(objState.position, objState.rotation);
            }
            else
            {
                Debug.LogWarning($"NetworkObject with ID {objState.objectId} not found in tracked objects.");
            }
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