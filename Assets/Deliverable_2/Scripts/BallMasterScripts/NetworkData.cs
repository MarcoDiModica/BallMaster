using System;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;

public enum MessageType
{
    Join,
    Chat,
    PlayerInput,
    GameState,
    StartGame
}

public class PlayerInputData
{
    public float horizontal;
    public float vertical;
}

public class ObjectState
{
    public string objectId; // ojo con los IDs, habrá que mirarlo bien
    public Vector3 position;
    public Quaternion rotation;
}

public class GameStateData
{
    public List<ObjectState> objects = new List<ObjectState>();
}

public static class NetworkProtocol
{
    private const char HEADER_DELIM = '|';
    private const char FIELD_DELIM = ',';
    private const char OBJECT_DELIM = ';';

    private static readonly CultureInfo culture = CultureInfo.InvariantCulture;

    // Serialización
    
    public static string SerializeJoin(string name)
    {
        return $"{MessageType.Join}{HEADER_DELIM}{name}";
    }

    public static string SerializeChat(string message)
    {
        return $"{MessageType.Chat}{HEADER_DELIM}{message}";
    }

    public static string SerializeStartGame(string sceneName)
    {
        return $"{MessageType.StartGame}{HEADER_DELIM}{sceneName}";
    }

    public static string SerializeInput(PlayerInputData input)
    {
        string h = input.horizontal.ToString(culture);
        string v = input.vertical.ToString(culture);
        return $"{MessageType.PlayerInput}{HEADER_DELIM}{h}{FIELD_DELIM}{v}";
    }

    public static string SerializeGameState(GameStateData state)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(MessageType.GameState);
        sb.Append(HEADER_DELIM);

        foreach (var obj in state.objects)
        {
            // buffer con la info de todos los objetos ordenados por comas en GameState X
            sb.Append(obj.objectId);
            sb.Append(FIELD_DELIM);
            sb.Append(obj.position.x.ToString(culture)); sb.Append(FIELD_DELIM);
            sb.Append(obj.position.y.ToString(culture)); sb.Append(FIELD_DELIM);
            sb.Append(obj.position.z.ToString(culture)); sb.Append(FIELD_DELIM);
            sb.Append(obj.rotation.x.ToString(culture)); sb.Append(FIELD_DELIM);
            sb.Append(obj.rotation.y.ToString(culture)); sb.Append(FIELD_DELIM);
            sb.Append(obj.rotation.z.ToString(culture)); sb.Append(FIELD_DELIM);
            sb.Append(obj.rotation.w.ToString(culture));

            sb.Append(OBJECT_DELIM);    
        }

        if (state.objects.Count > 0)
            sb.Length--; // cosas de formato, quitar la última separación

        return sb.ToString();
    }

    // Deserialización

    public static bool ParseHeader(string rawMessage, out MessageType type, out string payload)
    {
        type = MessageType.Chat; // default
        payload = string.Empty;
        
        int delimIndex = rawMessage.IndexOf(HEADER_DELIM);
        if (delimIndex == -1)
            return false;

        string header = rawMessage.Substring(0, delimIndex);
        if (!Enum.TryParse(header, out type)) return false;

        payload = rawMessage.Substring(delimIndex + 1);
        return true;
    }

    // Solo hacen falta funciones de deserializar PlayerInput y GameState, lo otros son strings
    public static PlayerInputData DeserializeInput(string payload)
    {
        try
        {
            string[] fields = payload.Split(FIELD_DELIM);
            if (fields.Length != 2) return null;

            return new PlayerInputData
            {
                horizontal = float.Parse(fields[0], culture),
                vertical = float.Parse(fields[1], culture)
            };
        }
        catch (Exception e)
        {
            Debug.LogError("Error deserializing PlayerInput: " + e.Message);
            return null;
        }
    }

    public static GameStateData DeserializeGameState(string payload)
    {
        GameStateData state = new GameStateData();
        if (string.IsNullOrEmpty(payload))
            return state;

        try
        {
            string[] objectStrings = payload.Split(OBJECT_DELIM);

            foreach (string objStr in objectStrings)
            {
                string[] fields = objStr.Split(FIELD_DELIM);
                if (fields.Length != 8) continue;

                ObjectState objState = new ObjectState
                {
                    objectId = fields[0],
                    position = new Vector3(
                        float.Parse(fields[1], culture),
                        float.Parse(fields[2], culture),
                        float.Parse(fields[3], culture)
                    ),
                    rotation = new Quaternion(
                        float.Parse(fields[4], culture),
                        float.Parse(fields[5], culture),
                        float.Parse(fields[6], culture),
                        float.Parse(fields[7], culture)
                    )
                };
                state.objects.Add(objState);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error deserializing GameState: " + e.Message);
            return new GameStateData();
        }

        return state;
    }

}


