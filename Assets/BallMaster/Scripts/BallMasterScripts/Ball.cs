using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class Ball : MonoBehaviour
{
    public enum BallState
    {
        Cold,
        Hot,
    }

    public BallState currentState = BallState.Cold;
    public string ownerPlayerId = "";
    public float hotSpeed = 20f;
    public int maxBouncesWithoutGravity = 3;
    public float normalGravity = -9.81f;
    public float equipTransitionDuration = 0.15f;
    public float pickupCooldown = 0.5f;

    private Rigidbody rb;
    private NetworkObject networkObject;
    private Vector3 velocity;
    private int bounceCount = 0;
    private bool isEquipped = false;
    private Transform equipTransform;
    private float lastLaunchTime = -999f;
    private Collider lastLauncherCollider;

    public string equippedPlayerId { get; private set; } = "";

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        networkObject = GetComponent<NetworkObject>();
        currentState = BallState.Cold;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private BallManager ballManager;
    private NetworkObjectManager networkObjectManager;

    public void Initialize(BallManager bManager, NetworkObjectManager noManager)
    {
        this.ballManager = bManager;
        this.networkObjectManager = noManager;
    }

    public void Launch(Vector3 direction, string launcherId, Vector3? launchPosition = null)
    {
        currentState = BallState.Hot;
        ownerPlayerId = launcherId;
        bounceCount = 0;
        velocity = direction.normalized * hotSpeed;
        lastLaunchTime = Time.time;

        isEquipped = false;
        equipTransform = null;
        equippedPlayerId = "";

        transform.SetParent(null);

        if (launchPosition.HasValue)
        {
            transform.position = launchPosition.Value;
        }

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.linearVelocity = velocity;

        GetComponent<Collider>().enabled = true;

        if (networkObjectManager != null)
        {
            NetworkObject launcher = networkObjectManager.GetNetworkObject(launcherId);
            if (launcher != null)
            {
                Collider launcherCollider = launcher.GetComponent<Collider>();
                if (launcherCollider != null)
                {
                    lastLauncherCollider = launcherCollider;
                    Physics.IgnoreCollision(GetComponent<Collider>(), launcherCollider, true);
                    StartCoroutine(RestoreCollisionAfterDelay(launcherCollider, pickupCooldown));
                }
            }
        }
    }

    private IEnumerator RestoreCollisionAfterDelay(Collider launcherCollider, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (launcherCollider != null && GetComponent<Collider>() != null)
        {
            Physics.IgnoreCollision(GetComponent<Collider>(), launcherCollider, false);
        }

        lastLauncherCollider = null;
    }

    public void Drop(Vector3 direction, string droppperId, Vector3? dropPosition = null)
    {
        currentState = BallState.Cold;
        ownerPlayerId = "";
        bounceCount = 0;
        velocity = direction.normalized * (hotSpeed * 0.5f);

        velocity = direction;

        lastLaunchTime = Time.time;

        isEquipped = false;
        equipTransform = null;
        equippedPlayerId = "";

        transform.SetParent(null);

        if (dropPosition.HasValue)
        {
            transform.position = dropPosition.Value;
        }

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.linearVelocity = velocity;

        GetComponent<Collider>().enabled = true;

        if (networkObjectManager != null)
        {
            NetworkObject droppper = networkObjectManager.GetNetworkObject(droppperId);
            if (droppper != null)
            {
                Collider dropperCollider = droppper.GetComponent<Collider>();
                if (dropperCollider != null)
                {
                    lastLauncherCollider = dropperCollider;
                    Physics.IgnoreCollision(GetComponent<Collider>(), dropperCollider, true);
                    StartCoroutine(RestoreCollisionAfterDelay(dropperCollider, pickupCooldown));
                }
            }
        }
    }

    public void Equip(Transform parent, string playerId = "")
    {
        equippedPlayerId = playerId;
        StartCoroutine(EquipCoroutine(parent));
    }

    private IEnumerator EquipCoroutine(Transform parent)
    {
        isEquipped = true;
        equipTransform = parent;

        GetComponent<Collider>().enabled = false;

        if (lastLauncherCollider != null)
        {
            Physics.IgnoreCollision(GetComponent<Collider>(), lastLauncherCollider, false);
            lastLauncherCollider = null;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.interpolation = RigidbodyInterpolation.None;
        rb.isKinematic = true;
        rb.useGravity = false;

        transform.SetParent(parent);

        yield return null;

        // Smooth transition to equip position
        Vector3 startPos = transform.localPosition;
        Quaternion startRot = transform.localRotation;
        float elapsed = 0f;

        while (elapsed < equipTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / equipTransitionDuration);
            transform.localPosition = Vector3.Lerp(startPos, Vector3.zero, t);
            transform.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, t);
            yield return null;
        }

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        Physics.SyncTransforms();
    }

    public void Unequip()
    {
        isEquipped = false;
        equipTransform = null;
        equippedPlayerId = "";

        transform.SetParent(null);

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.isKinematic = false;
        rb.useGravity = true;

        GetComponent<Collider>().enabled = true;

        Physics.SyncTransforms();
    }

    public bool CanBePickedUp(string playerId)
    {
        if (Time.time - lastLaunchTime < pickupCooldown)
            return false;

        if (currentState == BallState.Hot && playerId != ownerPlayerId)
            return false;

        return true;
    }

    void FixedUpdate()
    {
        if (isEquipped)
            return;

        if (currentState == BallState.Hot && bounceCount < maxBouncesWithoutGravity)
        {
            rb.linearVelocity = velocity;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isEquipped)
            return;

        if (currentState == BallState.Hot)
        {
            PlayerController player = collision.gameObject.GetComponent<PlayerController>();

            if (player != null)
            {
                NetworkObject playerNetObj = player.GetComponent<NetworkObject>();

                if (playerNetObj != null && playerNetObj.objectId != ownerPlayerId)
                {
                    if (ballManager != null)
                    {
                        ballManager.OnBallHitPlayer(player);
                    }
                    return;
                }
            }

            bounceCount++;

            if (bounceCount < maxBouncesWithoutGravity)
            {
                Vector3 reflection = Vector3.Reflect(velocity, collision.contacts[0].normal);
                velocity = reflection.normalized * hotSpeed;
                rb.linearVelocity = velocity;
            }
            else
            {
                TransitionToCold();
            }
        }
    }

    void TransitionToCold()
    {
        currentState = BallState.Cold;
        ownerPlayerId = "";
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    void RespawnBall()
    {
        if (ballManager != null)
        {
            ballManager.RespawnBall(networkObject.objectId);
        }
    }

    public void UpdateNetworkState(
        Vector3 pos,
        Quaternion rot,
        Vector3 vel,
        BallState state,
        string owner,
        int bounces
    )
    {
        if (isEquipped)
            return;

        transform.position = pos;
        transform.rotation = rot;

        velocity = vel;
        currentState = state;
        ownerPlayerId = owner;
        bounceCount = bounces;

        rb.isKinematic = false;

        if (state == BallState.Hot && bounces < maxBouncesWithoutGravity)
        {
            rb.useGravity = false;
        }
        else
        {
            rb.useGravity = true;
        }

        rb.linearVelocity = vel;
    }
}
