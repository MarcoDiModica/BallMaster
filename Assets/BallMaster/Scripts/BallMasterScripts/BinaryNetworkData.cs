using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

public enum MessageType_B : byte
{
    Join,
    Chat,
    PlayerInput,
    GameState,
    StartGame
}

public class PlayerInputData_B
{
    public float horizontal;
    public float vertical;
}

public class ObjectState_B
{
    public string objectId;
    public Vector3 position;
    public Quaternion rotation;
}

public class GameStateData_B
{
    public List<ObjectState_B> objects = new List<ObjectState_B>();
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

    public static byte[] SerializeInput(PlayerInputData_B input)
    {
        return Serialize(MessageType.PlayerInput, (writer) =>
        {
            writer.Write(input.horizontal);
            writer.Write(input.vertical);
        });
    }

    public static byte[] SerializeGameState(GameStateData_B gameState)
    {
        return Serialize(MessageType.GameState, (writer) =>
        {
            writer.Write(gameState.objects.Count);

            foreach (var obj in gameState.objects)
            {
                writer.Write(obj.objectId);
                WriteVector3(writer, obj.position);
                WriteQuaternion(writer, obj.rotation);

                /*writer.Write(obj.objectId);
                writer.Write(obj.position.x);
                writer.Write(obj.position.y);
                writer.Write(obj.position.z);
                writer.Write(obj.rotation.x);
                writer.Write(obj.rotation.y);
                writer.Write(obj.rotation.z);
                writer.Write(obj.rotation.w);*/
            }
        });
    }
    
  
    public static MessageType PeekHeader(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return (MessageType)(-1);
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

    public static PlayerInputData_B DeserializeInput(byte[] data)
    {
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            reader.ReadByte();
            return new PlayerInputData_B
            {
                horizontal = reader.ReadSingle(),
                vertical = reader.ReadSingle()
            };
        }
    }

    public static GameStateData_B DeserializeGameState(byte[] data)
    {
        GameStateData_B state = new GameStateData_B();
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            reader.ReadByte();
            int objectCount = reader.ReadInt32();

            for (int i = 0; i < objectCount; i++)
            {
                state.objects.Add(new ObjectState_B
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

}



