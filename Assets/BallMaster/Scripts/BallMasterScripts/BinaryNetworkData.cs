using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

public enum MessageType : byte
{
    Join,
    Chat,
    PlayerTransform,
    GameState,
    StartGame,
    AssignPlayerId,
    SyncExistingPlayers,
    BallState,
    SyncExistingBalls,
    BallLaunched
}

public class PlayerTransformData
{
    public string playerId;
    public Vector3 position;
    public Quaternion rotation;
}

public class ExistingPlayerData
{
    public string playerId;
    public Vector3 position;
    public Quaternion rotation;
}

public class ExistingPlayersData
{
    public List<ExistingPlayerData> players = new List<ExistingPlayerData>();
}

public class ExistingBallData
{
    public string ballId;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public byte state;
    public string ownerPlayerId;
    public int bounceCount;
}

public class ExistingBallsData
{
    public List<ExistingBallData> balls = new List<ExistingBallData>();
}

public class BallLaunchData
{
    public string ballId;
    public Vector3 direction;
    public string launcherId;
    public Vector3 launchPosition;
}

public class ObjectState
{
    public string objectId;
    public Vector3 position;
    public Quaternion rotation;
}

public class GameStateData
{
    public List<ObjectState> objects = new List<ObjectState>();
}

public class BallStateData
{
    public string ballId;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public byte state;
    public string ownerPlayerId;
    public int bounceCount;
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
        return Serialize(type, (writer) =>
        {
            writer.Write(message);
        });
    }

    public static byte[] SerializePlayerId(string playerId)
    {
        return Serialize(MessageType.AssignPlayerId, (writer) =>
        {
            writer.Write(playerId);
        });
    }

    public static byte[] SerializePlayerTransform(PlayerTransformData transform)
    {
        return Serialize(MessageType.PlayerTransform, (writer) =>
        {
            writer.Write(transform.playerId);
            WriteVector3(writer, transform.position);
            WriteQuaternion(writer, transform.rotation);
        });
    }

    public static byte[] SerializeExistingPlayers(ExistingPlayersData playersData)
    {
        return Serialize(MessageType.SyncExistingPlayers, (writer) =>
        {
            writer.Write(playersData.players.Count);

            foreach (var player in playersData.players)
            {
                writer.Write(player.playerId);
                WriteVector3(writer, player.position);
                WriteQuaternion(writer, player.rotation);
            }
        });
    }

    public static byte[] SerializeExistingBalls(ExistingBallsData ballsData)
    {
        return Serialize(MessageType.SyncExistingBalls, (writer) =>
        {
            writer.Write(ballsData.balls.Count);

            foreach (var ball in ballsData.balls)
            {
                writer.Write(ball.ballId);
                WriteVector3(writer, ball.position);
                WriteQuaternion(writer, ball.rotation);
                WriteVector3(writer, ball.velocity);
                writer.Write(ball.state);
                writer.Write(ball.ownerPlayerId ?? "");
                writer.Write(ball.bounceCount);
            }
        });
    }

    public static byte[] SerializeBallLaunch(BallLaunchData launchData)
    {
        return Serialize(MessageType.BallLaunched, (writer) =>
        {
            writer.Write(launchData.ballId);
            WriteVector3(writer, launchData.direction);
            writer.Write(launchData.launcherId);
            WriteVector3(writer, launchData.launchPosition);
        });
    }

    public static byte[] SerializeGameState(GameStateData gameState)
    {
        return Serialize(MessageType.GameState, (writer) =>
        {
            writer.Write(gameState.objects.Count);

            foreach (var obj in gameState.objects)
            {
                writer.Write(obj.objectId);
                WriteVector3(writer, obj.position);
                WriteQuaternion(writer, obj.rotation);
            }
        });
    }

    public static MessageType PeekHeader(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return unchecked((MessageType)(-1));
        }
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
                rotation = ReadQuaternion(reader)
            };
        }
    }

    public static ExistingPlayersData DeserializeExistingPlayers(byte[] data)
    {
        ExistingPlayersData playersData = new ExistingPlayersData();
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            reader.ReadByte();
            int playerCount = reader.ReadInt32();

            for (int i = 0; i < playerCount; i++)
            {
                playersData.players.Add(new ExistingPlayerData
                {
                    playerId = reader.ReadString(),
                    position = ReadVector3(reader),
                    rotation = ReadQuaternion(reader)
                });
            }
        }
        return playersData;
    }

    public static ExistingBallsData DeserializeExistingBalls(byte[] data)
    {
        ExistingBallsData ballsData = new ExistingBallsData();
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            reader.ReadByte();
            int ballCount = reader.ReadInt32();

            for (int i = 0; i < ballCount; i++)
            {
                ballsData.balls.Add(new ExistingBallData
                {
                    ballId = reader.ReadString(),
                    position = ReadVector3(reader),
                    rotation = ReadQuaternion(reader),
                    velocity = ReadVector3(reader),
                    state = reader.ReadByte(),
                    ownerPlayerId = reader.ReadString(),
                    bounceCount = reader.ReadInt32()
                });
            }
        }
        return ballsData;
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
                launchPosition = ReadVector3(reader)
            };
        }
    }

    public static GameStateData DeserializeGameState(byte[] data)
    {
        GameStateData state = new GameStateData();
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            reader.ReadByte();
            int objectCount = reader.ReadInt32();

            for (int i = 0; i < objectCount; i++)
            {
                state.objects.Add(new ObjectState
                {
                    objectId = reader.ReadString(),
                    position = ReadVector3(reader),
                    rotation = ReadQuaternion(reader)
                });
            }
        }
        return state;
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
        return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }

    public static byte[] SerializeBallStates(List<BallStateData> ballStates)
    {
        return Serialize(MessageType.BallState, (writer) =>
        {
            writer.Write(ballStates.Count);

            foreach (var ball in ballStates)
            {
                writer.Write(ball.ballId);
                WriteVector3(writer, ball.position);
                WriteQuaternion(writer, ball.rotation);
                WriteVector3(writer, ball.velocity);
                writer.Write(ball.state);
                writer.Write(ball.ownerPlayerId ?? "");
                writer.Write(ball.bounceCount);
            }
        });
    }

    public static List<BallStateData> DeserializeBallStates(byte[] data)
    {
        List<BallStateData> ballStates = new List<BallStateData>();
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            reader.ReadByte();
            int ballCount = reader.ReadInt32();

            for (int i = 0; i < ballCount; i++)
            {
                ballStates.Add(new BallStateData
                {
                    ballId = reader.ReadString(),
                    position = ReadVector3(reader),
                    rotation = ReadQuaternion(reader),
                    velocity = ReadVector3(reader),
                    state = reader.ReadByte(),
                    ownerPlayerId = reader.ReadString(),
                    bounceCount = reader.ReadInt32()
                });
            }
        }
        return ballStates;
    }
}