using System;
using UnityEngine;

public class NetworkObject : MonoBehaviour
{
    public string objectId;

    public event Action<Vector3, Quaternion> OnStateUpdated;

    public bool isDirty = false;

    private const int BUFFER_SIZE = 10;
    private const float INTERPOLATION_DELAY = 0.1f;

    private struct Snapshot
    {
        public float timestamp;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
    }

    private Snapshot[] snapshotBuffer = new Snapshot[BUFFER_SIZE];
    private int bufferIndex = 0;
    private int snapshotCount = 0;
    private float estimatedServerTime = 0f;
    private float lastSnapshotTime = 0f;

    private const int HISTORY_SIZE = 64;
    private struct HistoryEntry
    {
        public float serverTime;
        public Vector3 position;
        public Quaternion rotation;
    }
    private HistoryEntry[] serverHistory = new HistoryEntry[HISTORY_SIZE];
    private int historyIndex = 0;
    private int historyCount = 0;

    public void MarkDirty()
    {
        isDirty = true;
    }

    public void UpdateState(Vector3 pos, Quaternion rot)
    {
        OnStateUpdated?.Invoke(pos, rot);
    }

    public void RecordHistory(float serverTime)
    {
        serverHistory[historyIndex] = new HistoryEntry
        {
            serverTime = serverTime,
            position = transform.position,
            rotation = transform.rotation,
        };
        historyIndex = (historyIndex + 1) % HISTORY_SIZE;
        historyCount = Mathf.Min(historyCount + 1, HISTORY_SIZE);
    }

    public Vector3 GetPositionAtTime(float time)
    {
        if (historyCount == 0)
            return transform.position;

        HistoryEntry? before = null;
        HistoryEntry? after = null;

        for (int i = 0; i < historyCount; i++)
        {
            int idx = (historyIndex - 1 - i + HISTORY_SIZE) % HISTORY_SIZE;
            var entry = serverHistory[idx];

            if (entry.serverTime <= time)
            {
                if (!before.HasValue || entry.serverTime > before.Value.serverTime)
                    before = entry;
            }
            if (entry.serverTime >= time)
            {
                if (!after.HasValue || entry.serverTime < after.Value.serverTime)
                    after = entry;
            }
        }

        if (!before.HasValue && !after.HasValue)
            return transform.position;

        if (!before.HasValue)
            return after.Value.position;

        if (!after.HasValue)
            return before.Value.position;

        float duration = after.Value.serverTime - before.Value.serverTime;
        if (duration <= 0)
            return after.Value.position;

        float t = (time - before.Value.serverTime) / duration;
        t = Mathf.Clamp01(t);

        return Vector3.Lerp(before.Value.position, after.Value.position, t);
    }

    public void AddSnapshot(float serverTimestamp, Vector3 pos, Quaternion rot, Vector3 vel)
    {
        snapshotBuffer[bufferIndex] = new Snapshot
        {
            timestamp = serverTimestamp,
            position = pos,
            rotation = rot,
            velocity = vel,
        };
        bufferIndex = (bufferIndex + 1) % BUFFER_SIZE;
        snapshotCount = Mathf.Min(snapshotCount + 1, BUFFER_SIZE);

        if (serverTimestamp > estimatedServerTime)
        {
            estimatedServerTime = serverTimestamp;
            lastSnapshotTime = Time.time;
        }
    }

    public float GetCurrentServerTime()
    {
        return estimatedServerTime + (Time.time - lastSnapshotTime);
    }

    public (Vector3 pos, Quaternion rot, bool valid) InterpolateState()
    {
        if (snapshotCount < 2)
            return (transform.position, transform.rotation, false);

        float renderTime = GetCurrentServerTime() - INTERPOLATION_DELAY;

        Snapshot? before = null;
        Snapshot? after = null;

        for (int i = 0; i < snapshotCount; i++)
        {
            int idx = (bufferIndex - 1 - i + BUFFER_SIZE) % BUFFER_SIZE;
            var snap = snapshotBuffer[idx];

            if (snap.timestamp <= renderTime)
            {
                if (!before.HasValue || snap.timestamp > before.Value.timestamp)
                    before = snap;
            }
            if (snap.timestamp >= renderTime)
            {
                if (!after.HasValue || snap.timestamp < after.Value.timestamp)
                    after = snap;
            }
        }

        if (!before.HasValue && !after.HasValue)
            return (transform.position, transform.rotation, false);

        if (!before.HasValue)
            return (after.Value.position, after.Value.rotation, true);

        if (!after.HasValue)
            return (before.Value.position, before.Value.rotation, true);

        float duration = after.Value.timestamp - before.Value.timestamp;
        if (duration <= 0)
            return (after.Value.position, after.Value.rotation, true);

        float t = (renderTime - before.Value.timestamp) / duration;
        t = Mathf.Clamp01(t);

        return (
            Vector3.Lerp(before.Value.position, after.Value.position, t),
            Quaternion.Slerp(before.Value.rotation, after.Value.rotation, t),
            true
        );
    }

    public void ClearSnapshots()
    {
        snapshotCount = 0;
        bufferIndex = 0;
    }

    public void ClearHistory()
    {
        historyCount = 0;
        historyIndex = 0;
    }
}

