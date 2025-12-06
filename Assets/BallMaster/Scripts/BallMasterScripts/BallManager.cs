using UnityEngine;
using System.Collections.Generic;

public class BallManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private NetworkObjectManager networkObjectManager;

    public GameObject ballPrefab;
    public Transform[] ballSpawnPoints;
    
    private Dictionary<string, Ball> balls = new Dictionary<string, Ball>();
    private int nextBallId = 0;
    
    
    void Start()
    {
        if (networkManager == null)
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager != null)
            {
                networkManager.RegisterBallManager(this);
            }
        }

        if (networkManager != null && networkManager.isHost)
        {
            SpawnInitialBalls();
        }
    }

    void Update()
    {
        if (networkManager != null && networkManager.isHost)
        {
            List<BallStateData> dirtyBalls = new List<BallStateData>();
            
            foreach(var ball in balls.Values)
            {
                if(ball.currentState == Ball.BallState.Hot || ball.transform.hasChanged)
                {
                    ball.transform.hasChanged = false;
                    
                    Rigidbody rb = ball.GetComponent<Rigidbody>();
                    dirtyBalls.Add(new BallStateData
                    {
                        ballId = ball.GetComponent<NetworkObject>().objectId,
                        position = ball.transform.position,
                        rotation = ball.transform.rotation,
                        velocity = rb.linearVelocity,
                        state = (byte)ball.currentState,
                        ownerPlayerId = ball.ownerPlayerId,
                        bounceCount = ball.maxBouncesWithoutGravity
                    });
                }
            }
            
            if (dirtyBalls.Count > 0)
            {
                networkManager.SendBallStates(dirtyBalls);
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
            return;
        }

        GameObject ballObj = Instantiate(ballPrefab, position, Quaternion.identity);
        NetworkObject netObj = ballObj.GetComponent<NetworkObject>();
        netObj.objectId = ballId;

        Ball ball = ballObj.GetComponent<Ball>();
        ball.Initialize(this, networkObjectManager);
        balls[ballId] = ball;

        if (networkObjectManager != null)
        {
            networkObjectManager.RegisterNetworkObject(netObj);
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

    public void OnBallHitPlayer(PlayerController player)
    {
        if (networkManager != null && networkManager.isHost)
        {
             player.RequestRespawn();
             
             foreach(var ball in balls.Values)
             {
                 if(ball.ownerPlayerId == "") continue;
             }
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

    public ExistingBallsData GetExistingBallsData()
    {
        ExistingBallsData ballsData = new ExistingBallsData();

        foreach (var kvp in balls)
        {
            Ball ball = kvp.Value;
            Rigidbody rb = ball.GetComponent<Rigidbody>();

            ballsData.balls.Add(new ExistingBallData
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

        return ballsData;
    }

    public void SpawnExistingBalls(ExistingBallsData ballsData)
    {
        foreach (var ballData in ballsData.balls)
        {
            if (!balls.ContainsKey(ballData.ballId))
            {
                GameObject ballObj = Instantiate(ballPrefab, ballData.position, ballData.rotation);
                NetworkObject netObj = ballObj.GetComponent<NetworkObject>();
                netObj.objectId = ballData.ballId;

                Ball ball = ballObj.GetComponent<Ball>();
                ball.currentState = (Ball.BallState)ballData.state;
                ball.ownerPlayerId = ballData.ownerPlayerId;

                Rigidbody rb = ball.GetComponent<Rigidbody>();
                rb.linearVelocity = ballData.velocity;

                if (ball.currentState == Ball.BallState.Hot)
                {
                    rb.useGravity = false;
                }

                balls[ballData.ballId] = ball;

                if (networkObjectManager != null)
                {
                    networkObjectManager.RegisterNetworkObject(netObj);
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