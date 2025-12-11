using System;
using System.Collections.Generic;
using UnityEngine;

namespace TaskCanvas
{
    [Serializable]
    public class KanbanCard
    {
        public string id;
        public string title;
        public string description;
        public Color color = new Color(0.5f, 0.7f, 1f);
        public int priority;
        public List<string> tags = new List<string>();
        public List<string> assigneeIds = new List<string>();
        public bool isCompleted;
        public long createdAt;
        public long updatedAt;

        public KanbanCard()
        {
            id = Guid.NewGuid().ToString();
            createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            updatedAt = createdAt;
        }

        public KanbanCard(string title)
            : this()
        {
            this.title = title;
        }

        public void MarkUpdated()
        {
            updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
