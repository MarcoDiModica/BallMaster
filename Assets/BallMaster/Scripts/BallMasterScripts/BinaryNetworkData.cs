using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum MessageType : byte
{
    Join,
    Chat,
    AssignPlayerId,
    PlayerTransform,
    StartGame,
    BallLaunched,
    BallEquip,
    Replication,
    Heartbeat,
    HeartbeatAck,
    BallDrop,
}

public class BallLaunchData
{
    public string ballId;
    public Vector3 direction;
    public string launcherId;
    public Vector3 launchPosition;
}

public class BallEquipData
{
    public string ballId;
    public string playerId;
}

public class PlayerTransformData
{
    public string playerId;
    public Vector3 position;
    public Quaternion rotation;
}

public static class NetworkProtocolBinary
{
    private static byte[] Serialize(MessageType type, Action<BinaryWriter> writeAction)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((byte)type);
            writeAction(writer);
            return stream.ToArray();
        }
    }

    public static byte[] SerializeString(MessageType type, string message)
    {
        return Serialize(
            type,
            (writer) =>
            {
                writer.Write(message);
            }
        );
    }

    public static byte[] SerializePlayerId(string playerId)
    {
        return Serialize(
            MessageType.AssignPlayerId,
            (writer) =>
            {
                writer.Write(playerId);
            }
        );
    }

    public static byte[] SerializePlayerTransform(PlayerTransformData data)
    {
        return Serialize(
            MessageType.PlayerTransform,
            (writer) =>
            {
                writer.Write(data.playerId);
                WriteVector3(writer, data.position);
                WriteQuaternion(writer, data.rotation);
            }
        );
    }

    public static PlayerTransformData DeserializePlayerTransform(byte[] data)
    {
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            reader.ReadByte();
            return new PlayerTransformData
            {
                playerId = reader.ReadString(),
                position = ReadVector3(reader),
                rotation = ReadQuaternion(reader),
            };
        }
    }

    public static byte[] SerializeReplicationPackets(List<ReplicationPacket> packets)
    {
        return Serialize(
            MessageType.Replication,
            (writer) =>
            {
                writer.Write(packets.Count);

                foreach (var packet in packets)
                {
                    writer.Write((byte)packet.command);
                    writer.Write(packet.networkId);
                    writer.Write((byte)packet.objectType);
                    writer.Write(packet.timestamp);
                    WriteVector3(writer, packet.position);
                    WriteQuaternion(writer, packet.rotation);
                    WriteVector3(writer, packet.velocity);
                }
            }
        );
    }

    public static List<ReplicationPacket> DeserializeReplicationPackets(byte[] data)
    {
        List<ReplicationPacket> packets = new List<ReplicationPacket>();
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            // Header
            reader.ReadByte();

            int packetCount = reader.ReadInt32();

            for (int i = 0; i < packetCount; i++)
            {
                packets.Add(
                    new ReplicationPacket
                    {
                        command = (ReplicationCommand)reader.ReadByte(),
                        networkId = reader.ReadString(),
                        objectType = (ReplicatedObjectType)reader.ReadByte(),
                        timestamp = reader.ReadSingle(),
                        position = ReadVector3(reader),
                        rotation = ReadQuaternion(reader),
                        velocity = ReadVector3(reader),
                    }
                );
            }
        }
        return packets;
    }

    public static byte[] SerializeBallLaunch(BallLaunchData launchData)
    {
        return Serialize(
            MessageType.BallLaunched,
            (writer) =>
            {
                writer.Write(launchData.ballId);
                WriteVector3(writer, launchData.direction);
                writer.Write(launchData.launcherId);
                WriteVector3(writer, launchData.launchPosition);
            }
        );
    }

    public static byte[] SerializeBallDrop(BallLaunchData launchData)
    {
        return Serialize(
            MessageType.BallDrop,
            (writer) =>
            {
                writer.Write(launchData.ballId);
                WriteVector3(writer, launchData.direction);
                writer.Write(launchData.launcherId);
                WriteVector3(writer, launchData.launchPosition);
            }
        );
    }

    public static BallLaunchData DeserializeBallLaunch(byte[] data)
    {
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            reader.ReadByte();
            return new BallLaunchData
            {
                ballId = reader.ReadString(),
                direction = ReadVector3(reader),
                launcherId = reader.ReadString(),
                launchPosition = ReadVector3(reader),
            };
        }
    }

    public static byte[] SerializeBallEquip(BallEquipData equipData)
    {
        return Serialize(
            MessageType.BallEquip,
            (writer) =>
            {
                writer.Write(equipData.ballId);
                writer.Write(equipData.playerId);
            }
        );
    }

    public static BallEquipData DeserializeBallEquip(byte[] data)
    {
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            reader.ReadByte();
            return new BallEquipData
            {
                ballId = reader.ReadString(),
                playerId = reader.ReadString(),
            };
        }
    }

    public static MessageType PeekHeader(byte[] data)
    {
        if (data == null || data.Length == 0)
            return unchecked((MessageType)(-1));
        return (MessageType)data[0];
    }

    public static string DeserializeString(byte[] data)
    {
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            reader.ReadByte();
            return reader.ReadString();
        }
    }

    private static void WriteVector3(BinaryWriter writer, Vector3 v)
    {
        writer.Write(v.x);
        writer.Write(v.y);
        writer.Write(v.z);
    }

    private static Vector3 ReadVector3(BinaryReader reader)
    {
        return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }

    private static void WriteQuaternion(BinaryWriter writer, Quaternion q)
    {
        writer.Write(q.x);
        writer.Write(q.y);
        writer.Write(q.z);
        writer.Write(q.w);
    }

    private static Quaternion ReadQuaternion(BinaryReader reader)
    {
        return new Quaternion(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle()
        );
    }
}
