using System;
using System.Collections.Generic;
using UnityEngine;

namespace TaskCanvas
{
    /// <summary>
    /// Represents a column in the Kanban board (e.g., To Do, In Progress, Done).
    /// </summary>
    [Serializable]
    public class KanbanColumn
    {
        public string id;
        public string title;
        public Color headerColor = new Color(0.3f, 0.5f, 0.9f);
        public List<KanbanCard> cards = new List<KanbanCard>();

        public KanbanColumn()
        {
            id = Guid.NewGuid().ToString();
        }

        public KanbanColumn(string title)
            : this()
        {
            this.title = title;
        }

        public KanbanColumn(string title, Color color)
            : this(title)
        {
            this.headerColor = color;
        }
    }
}
