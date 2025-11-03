using UnityEngine;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class NetworkObjectManager : MonoBehaviour
{
    public static NetworkObjectManager Instance;

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

        networkObjects = FindObjectsOfType<NetworkObject>().ToDictionary(obj => obj.objectId, obj => obj);
    
        Debug.Log($"NetworkObjectManager initialized with {networkObjects.Count} objects.");
    }

    public void ApplyGameState(GameStateData_B state)
    {
        foreach (ObjectState_B objState in state.objects)
        {
            if (networkObjects.ContainsKey(objState.objectId))
            {
                networkObjects[objState.objectId].UpdateState(objState.position, objState.rotation);
            }
            else
            {
                // instanciar un objeto?
                Debug.LogWarning($"NetworkObject with ID {objState.objectId} not found.");
            }
        }

    }
}
