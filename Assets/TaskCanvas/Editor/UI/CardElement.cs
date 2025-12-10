using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    /// <summary>
    /// Visual Element for displaying a Kanban card.
    /// </summary>
    public class CardElement : VisualElement
    {
        public event Action<CardElement> OnCardClicked;
        public event Action<CardElement> OnDragStarted;

        public KanbanCard Card { get; private set; }
        public KanbanBoard Board { get; private set; }

        private Label _titleLabel;
        private Label _descriptionLabel;
        private VisualElement _footer;

        public CardElement(KanbanCard card, KanbanBoard board)
        {
            Card = card;
            Board = board;

            AddToClassList("kanban-card");
            UpdatePriorityClass();

            BuildUI();
            RegisterCallbacks();
        }

        private void BuildUI()
        {
            // Title
            _titleLabel = new Label(Card.title);
            _titleLabel.AddToClassList("card-title");
            Add(_titleLabel);

            // Description (if exists)
            if (!string.IsNullOrEmpty(Card.description))
            {
                _descriptionLabel = new Label(Card.description);
                _descriptionLabel.AddToClassList("card-description");
                Add(_descriptionLabel);
            }

            // Footer with tags and assignees
            _footer = new VisualElement();
            _footer.AddToClassList("card-footer");
            Add(_footer);

            RefreshFooter();
        }

        private void RefreshFooter()
        {
            _footer.Clear();

            // Tags
            foreach (var tag in Card.tags)
            {
                var tagLabel = new Label($"#{tag}");
                tagLabel.AddToClassList("card-tag");
                _footer.Add(tagLabel);
            }

            // Assignees
            foreach (var assigneeId in Card.assigneeIds)
            {
                var assignee = Board.GetAssigneeById(assigneeId);
                if (assignee != null)
                {
                    var assigneeElement = new VisualElement();
                    assigneeElement.AddToClassList("card-assignee");
                    assigneeElement.style.backgroundColor = assignee.color;

                    // Show first letter
                    var initial = new Label(
                        assignee.name.Length > 0 ? assignee.name[0].ToString().ToUpper() : "?"
                    );
                    initial.style.color = Color.white;
                    initial.style.unityTextAlign = TextAnchor.MiddleCenter;
                    initial.style.fontSize = 10;
                    assigneeElement.Add(initial);

                    assigneeElement.tooltip = assignee.name;
                    _footer.Add(assigneeElement);
                }
            }
        }

        private void RegisterCallbacks()
        {
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<ClickEvent>(OnClick);
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button == 0)
            {
                OnDragStarted?.Invoke(this);
            }
        }

        private void OnClick(ClickEvent evt)
        {
            if (evt.clickCount == 2)
            {
                OnCardClicked?.Invoke(this);
            }
        }

        private void UpdatePriorityClass()
        {
            RemoveFromClassList("priority-low");
            RemoveFromClassList("priority-medium");
            RemoveFromClassList("priority-high");

            switch (Card.priority)
            {
                case 0:
                    AddToClassList("priority-low");
                    break;
                case 1:
                    AddToClassList("priority-medium");
                    break;
                case 2:
                    AddToClassList("priority-high");
                    break;
            }
        }

        public void Refresh()
        {
            _titleLabel.text = Card.title;

            if (_descriptionLabel != null)
            {
                _descriptionLabel.text = Card.description;
                _descriptionLabel.style.display = string.IsNullOrEmpty(Card.description)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
            else if (!string.IsNullOrEmpty(Card.description))
            {
                _descriptionLabel = new Label(Card.description);
                _descriptionLabel.AddToClassList("card-description");
                Insert(1, _descriptionLabel);
            }

            UpdatePriorityClass();
            RefreshFooter();
        }

        public void SetDragging(bool isDragging)
        {
            if (isDragging)
                AddToClassList("dragging");
            else
                RemoveFromClassList("dragging");
        }
    }
}
