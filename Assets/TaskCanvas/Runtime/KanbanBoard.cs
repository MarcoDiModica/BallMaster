using System.Collections.Generic;
using UnityEngine;

namespace TaskCanvas
{
    /// <summary>
    /// ScriptableObject representing a Kanban board.
    /// Create via: Right-click in Project > Create > TaskCanvas > Board
    /// </summary>
    [CreateAssetMenu(fileName = "New Kanban Board", menuName = "TaskCanvas/Board", order = 1)]
    public class KanbanBoard : ScriptableObject
    {
        public string boardName = "My Board";
        public List<KanbanColumn> columns = new List<KanbanColumn>();
        public List<Assignee> assignees = new List<Assignee>();
        public List<string> allTags = new List<string>();

        private void Reset()
        {
            boardName = "My Board";
            columns = new List<KanbanColumn>
            {
                new KanbanColumn("📋 To Do", new Color(0.4f, 0.6f, 0.9f)),
                new KanbanColumn("🔄 In Progress", new Color(0.9f, 0.7f, 0.3f)),
                new KanbanColumn("✅ Done", new Color(0.4f, 0.8f, 0.5f)),
            };
            assignees = new List<Assignee>();
            allTags = new List<string> { "bug", "feature", "urgent" };
        }

        public Assignee GetAssigneeById(string id)
        {
            return assignees.Find(a => a.id == id);
        }

        public void AddTag(string tag)
        {
            if (!string.IsNullOrEmpty(tag) && !allTags.Contains(tag))
            {
                allTags.Add(tag);
            }
        }

        public void AddAssignee(Assignee assignee)
        {
            if (assignee != null && !assignees.Exists(a => a.id == assignee.id))
            {
                assignees.Add(assignee);
            }
        }

        public KanbanCard FindCardById(string cardId)
        {
            foreach (var column in columns)
            {
                var card = column.cards.Find(c => c.id == cardId);
                if (card != null)
                    return card;
            }
            return null;
        }

        public bool MoveCard(string cardId, string targetColumnId, int targetIndex = -1)
        {
            KanbanCard card = null;
            KanbanColumn sourceColumn = null;

            // Find the card and its source column
            foreach (var column in columns)
            {
                card = column.cards.Find(c => c.id == cardId);
                if (card != null)
                {
                    sourceColumn = column;
                    break;
                }
            }

            if (card == null || sourceColumn == null)
                return false;

            // Find target column
            var targetColumn = columns.Find(c => c.id == targetColumnId);
            if (targetColumn == null)
                return false;

            // Remove from source
            sourceColumn.cards.Remove(card);

            // Add to target
            if (targetIndex < 0 || targetIndex >= targetColumn.cards.Count)
            {
                targetColumn.cards.Add(card);
            }
            else
            {
                targetColumn.cards.Insert(targetIndex, card);
            }

            card.MarkUpdated();
            return true;
        }
    }
}
