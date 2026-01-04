using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class ReliableChannel
{
    private struct PendingMessage
    {
        public byte[] data;
        public float sentTime;
        public int retryCount;
        public Action<IPEndPoint> sendAction;
        public IPEndPoint target;
    }

    private Dictionary<ushort, PendingMessage> pendingMessages = new Dictionary<ushort, PendingMessage>();
    private ushort nextSequence = 0;
    private float retryInterval = 0.2f;
    private int maxRetries = 5;
    private Action<byte[], IPEndPoint> rawSend;

    public ReliableChannel(Action<byte[], IPEndPoint> sendFunc)
    {
        rawSend = sendFunc;
    }

    public ushort SendReliable(byte[] data, IPEndPoint target)
    {
        ushort seq = nextSequence++;
        byte[] wrappedData = WrapWithSequence(data, seq);

        PendingMessage msg = new PendingMessage
        {
            data = wrappedData,
            sentTime = Time.time,
            retryCount = 0,
            target = target,
        };

        pendingMessages[seq] = msg;
        rawSend(wrappedData, target);
        return seq;
    }

    public void OnAckReceived(ushort sequenceNumber)
    {
        if (pendingMessages.ContainsKey(sequenceNumber))
        {
            pendingMessages.Remove(sequenceNumber);
        }
    }

    public void Update()
    {
        List<ushort> toRemove = new List<ushort>();
        List<ushort> toRetry = new List<ushort>();

        foreach (var kvp in pendingMessages)
        {
            ushort seq = kvp.Key;
            PendingMessage msg = kvp.Value;

            if (Time.time - msg.sentTime >= retryInterval)
            {
                if (msg.retryCount >= maxRetries)
                {
                    toRemove.Add(seq);
                }
                else
                {
                    toRetry.Add(seq);
                }
            }
        }

        foreach (ushort seq in toRetry)
        {
            PendingMessage msg = pendingMessages[seq];
            msg.retryCount++;
            msg.sentTime = Time.time;
            pendingMessages[seq] = msg;
            rawSend(msg.data, msg.target);
        }

        foreach (ushort seq in toRemove)
        {
            pendingMessages.Remove(seq);
        }
    }

    private byte[] WrapWithSequence(byte[] original, ushort seq)
    {
        byte[] wrapped = new byte[original.Length + 2];
        wrapped[0] = (byte)(seq & 0xFF);
        wrapped[1] = (byte)((seq >> 8) & 0xFF);
        Array.Copy(original, 0, wrapped, 2, original.Length);
        return wrapped;
    }

    public static ushort ExtractSequence(byte[] data)
    {
        if (data.Length < 2) return 0;
        return (ushort)(data[0] | (data[1] << 8));
    }

    public static byte[] UnwrapData(byte[] data)
    {
        if (data.Length < 2) return data;
        byte[] unwrapped = new byte[data.Length - 2];
        Array.Copy(data, 2, unwrapped, 0, unwrapped.Length);
        return unwrapped;
    }

    public int GetPendingCount()
    {
        return pendingMessages.Count;
    }

    public void Clear()
    {
        pendingMessages.Clear();
    }
}
