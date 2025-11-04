using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NetworkObjectManager : MonoBehaviour
{
    public static NetworkObjectManager Instance;

    public GameObject playerPrefab;
    public GameObject ballPrefab;

    private Dictionary<string, NetworkObject> networkObjects = new Dictionary<string, NetworkObject>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

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
        
        Debug.Log($"Refreshed NetworkObjectManager: {networkObjects.Count} objects tracked.");
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
            Debug.Log($"Registered NetworkObject: {obj.objectId}");
        }
    }

    public void UnregisterNetworkObject(string objectId)
    {
        if (networkObjects.ContainsKey(objectId))
        {
            networkObjects.Remove(objectId);
            Debug.Log($"Unregistered NetworkObject: {objectId}");
        }
    }

    public void ApplyGameState(GameStateData state)
    {
        HashSet<string> receivedObjectsIds = new HashSet<string>();
        foreach (var objState in state.objects)
        {
            receivedObjectsIds.Add(objState.objectId);
        }

        foreach (ObjectState objState in state.objects)
        {
            if (networkObjects.ContainsKey(objState.objectId))
            {
                string myId = PlayerManager.Instance.GetMyPlayerId();
                if (objState.objectId == myId)
                {
                    continue;
                }

                networkObjects[objState.objectId].UpdateState(objState.position, objState.rotation);
            }
            else
            {
                Debug.Log($"New object received {objState.objectId}, instantiating...");
                SpawnClientObject(objState);
            }
        }

        List<string> localKeys = new List<string>(networkObjects.Keys);

        foreach(string localId in localKeys)
        {
            if (!receivedObjectsIds.Contains(localId))
            {
                Debug.Log($"Object {localId} not in game state, removing locally.");
                GameObject objToDestroy = networkObjects[localId].gameObject;

                Destroy(objToDestroy);
            }
        }
    }

    private void SpawnClientObject(ObjectState state)
    {
        GameObject prefabToSpawn = null;
        bool isPlayer = false;

        if (state.objectId.StartsWith("player_"))
        {
            prefabToSpawn = playerPrefab;
            isPlayer = true;
        }
        else if (state.objectId.StartsWith("ball_"))
        {
            prefabToSpawn = ballPrefab;
        }
        else
        {
            Debug.LogWarning($"Unknown object type for ID {state.objectId}, cannot spawn.");
            return;
        }
        if (prefabToSpawn != null) {
            Debug.LogError($"Null prefab for {state.objectId}");
            return;
        }

        GameObject newObj = Instantiate(prefabToSpawn, state.position, state.rotation);
        NetworkObject netObj = newObj.GetComponent<NetworkObject>();
        netObj.objectId = state.objectId;

        if (isPlayer)
        {
            PlayerController pc = newObj.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.SetAsRemotePlayer();
            }
        }

        RegisterNetworkObject(netObj);
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