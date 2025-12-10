using System;
using UnityEngine;

namespace TaskCanvas
{
    /// <summary>
    /// Represents a person or team that can be assigned to cards.
    /// </summary>
    [Serializable]
    public class Assignee
    {
        public string id;
        public string name;
        public Color color = new Color(0.4f, 0.6f, 1f);

        public Assignee()
        {
            id = Guid.NewGuid().ToString();
        }

        public Assignee(string name)
            : this()
        {
            this.name = name;
        }
    }
}
