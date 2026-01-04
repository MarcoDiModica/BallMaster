using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BallManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private NetworkManager networkManager;

    [SerializeField]
    private NetworkObjectManager networkObjectManager;

    private ReplicationManagerServer replicationServer;

    public GameObject ballPrefab;
    public Transform[] ballSpawnPoints;

    private Dictionary<string, Ball> balls = new Dictionary<string, Ball>();
    private int nextBallId = 0;

    void Start()
    {
        if (networkManager == null)
            networkManager = FindFirstObjectByType<NetworkManager>();

        if (networkManager != null)
            networkManager.RegisterBallManager(this);

        replicationServer = FindFirstObjectByType<ReplicationManagerServer>();

        if (networkManager != null && networkManager.isHost)
        {
            SpawnInitialBalls();
        }
    }

    void Update()
    {
        if (networkManager != null && networkManager.isHost)
        {
            if (Keyboard.current.yKey.wasPressedThisFrame)
            {
                Vector3 spawnPos = new Vector3(0, 5, 0);
                if (Camera.main != null)
                {
                    spawnPos = Camera.main.transform.position + Camera.main.transform.forward * 3f;
                }
                else
                {
                    spawnPos = new Vector3(Random.Range(-5f, 5f), 5f, Random.Range(-5f, 5f));
                }

                SpawnBall("ball_debug_" + nextBallId, spawnPos);
                nextBallId++;
            }

            if (replicationServer != null)
            {
                foreach (var ball in balls.Values)
                {
                    if (ball.transform.hasChanged)
                    {
                        ball.transform.hasChanged = false;
                        NetworkObject netObj = ball.GetComponent<NetworkObject>();
                        if (netObj != null)
                        {
                            netObj.MarkDirty();
                            replicationServer.MarkDirty(netObj.objectId);
                        }
                    }
                }
            }
        }
    }

    void SpawnInitialBalls()
    {
        for (int i = 0; i < ballSpawnPoints.Length; i++)
        {
            SpawnBall("ball_" + nextBallId, ballSpawnPoints[i].position);
            nextBallId++;
        }
    }

    public void SpawnBall(string ballId, Vector3 position)
    {
        if (balls.ContainsKey(ballId))
            return;

        GameObject ballObj = Instantiate(ballPrefab, position, Quaternion.identity);
        NetworkObject netObj = ballObj.GetComponent<NetworkObject>();
        if (netObj == null)
            netObj = ballObj.AddComponent<NetworkObject>();

        netObj.objectId = ballId;

        Ball ball = ballObj.GetComponent<Ball>();
        ball.Initialize(this, networkObjectManager);
        balls[ballId] = ball;

        if (networkObjectManager != null)
        {
            networkObjectManager.RegisterNetworkObject(netObj);
        }

        if (replicationServer != null)
        {
            replicationServer.RegisterObject(ballId, ReplicatedObjectType.Ball);
        }
    }

    public void RespawnBall(string ballId)
    {
        if (!balls.ContainsKey(ballId))
            return;

        Ball ball = balls[ballId];
        Vector3 spawnPos = ballSpawnPoints[Random.Range(0, ballSpawnPoints.Length)].position;

        ball.transform.position = spawnPos;
        ball.transform.rotation = Quaternion.identity;
        ball.currentState = Ball.BallState.Cold;
        ball.ownerPlayerId = "";

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;

        NetworkObject netObj = ball.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.MarkDirty();
    }

    public void OnBallHitPlayer(PlayerController player, string killerId = "")
    {
        if (networkManager != null && networkManager.isHost)
        {
            NetworkObject playerNetObj = player.GetComponent<NetworkObject>();
            string victimId = playerNetObj != null ? playerNetObj.objectId : "";

            if (LeaderboardManager.Instance != null && !string.IsNullOrEmpty(killerId) && !string.IsNullOrEmpty(victimId))
            {
                LeaderboardManager.Instance.RegisterKill(killerId, victimId);

                var killerStats = LeaderboardManager.Instance.GetPlayerStats(killerId);
                var victimStats = LeaderboardManager.Instance.GetPlayerStats(victimId);
                if (killerStats != null && victimStats != null)
                {
                    networkManager.SendKillEvent(
                        killerId, killerStats.playerName,
                        victimId, victimStats.playerName
                    );
                }
            }

            if (playerNetObj != null && playerNetObj.objectId.StartsWith("Player_"))
            {
                PlayerManager pm = FindFirstObjectByType<PlayerManager>();
                if (pm != null && pm.spawnPoints.Length > 0)
                {
                    int randomIndex = Random.Range(0, pm.spawnPoints.Length);
                    Vector3 spawnPos = pm.spawnPoints[randomIndex].position;
                    networkManager.SendPlayerRespawn(playerNetObj.objectId, spawnPos);
                    player.Respawn(spawnPos);
                }
            }
            else
            {
                player.RequestRespawn();
            }
        }
    }

    public void EquipBallNetworked(string ballId, string playerId)
    {
        if (balls.ContainsKey(ballId))
        {
            Ball ball = balls[ballId];
            NetworkObject playerObj = networkObjectManager.GetNetworkObject(playerId);

            if (playerObj != null)
            {
                PlayerController pc = playerObj.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.EquipBall(ball);
                }
            }
        }
    }

    public Ball GetBall(string ballId)
    {
        return balls.ContainsKey(ballId) ? balls[ballId] : null;
    }

    public Dictionary<string, Ball> GetAllBalls()
    {
        return balls;
    }
}
