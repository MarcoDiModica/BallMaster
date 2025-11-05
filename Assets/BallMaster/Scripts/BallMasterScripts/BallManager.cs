using UnityEngine;
using System.Collections.Generic;

public class BallManager : MonoBehaviour
{
    public static BallManager Instance;

    public GameObject ballPrefab;
    public Transform[] ballSpawnPoints;
    public float networkSendRate = 0.05f;

    private Dictionary<string, Ball> balls = new Dictionary<string, Ball>();
    private int nextBallId = 0;
    private float nextSendTime = 0f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.isHost)
        {
            SpawnInitialBalls();
        }
    }

    void Update()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.isHost)
        {
            if (Time.time >= nextSendTime)
            {
                SyncAllBalls();
                nextSendTime = Time.time + networkSendRate;
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
        {
            Debug.LogWarning($"Ball {ballId} already exists");
            return;
        }

        GameObject ballObj = Instantiate(ballPrefab, position, Quaternion.identity);
        NetworkObject netObj = ballObj.GetComponent<NetworkObject>();
        netObj.objectId = ballId;

        Ball ball = ballObj.GetComponent<Ball>();
        balls[ballId] = ball;

        if (NetworkObjectManager.Instance != null)
        {
            NetworkObjectManager.Instance.RegisterNetworkObject(netObj);
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
    }

    void SyncAllBalls()
    {
        List<BallStateData> ballStates = new List<BallStateData>();

        foreach (var kvp in balls)
        {
            Ball ball = kvp.Value;
            Rigidbody rb = ball.GetComponent<Rigidbody>();

            ballStates.Add(new BallStateData
            {
                ballId = kvp.Key,
                position = ball.transform.position,
                rotation = ball.transform.rotation,
                velocity = rb.linearVelocity,
                state = (byte)ball.currentState,
                ownerPlayerId = ball.ownerPlayerId,
                bounceCount = ball.maxBouncesWithoutGravity
            });
        }

        if (ballStates.Count > 0 && NetworkManager.Instance != null)
        {
            NetworkManager.Instance.SendBallStates(ballStates);
        }
    }

    public void ApplyBallStates(List<BallStateData> ballStates)
    {
        foreach (var state in ballStates)
        {
            if (balls.ContainsKey(state.ballId))
            {
                Ball ball = balls[state.ballId];
                ball.UpdateNetworkState(
                    state.position,
                    state.rotation,
                    state.velocity,
                    (Ball.BallState)state.state,
                    state.ownerPlayerId,
                    state.bounceCount
                );
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