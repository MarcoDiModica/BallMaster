using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    /// <summary>
    /// Visual Element for displaying a Kanban card with animated hover-reveal completion.
    /// </summary>
    public class CardElement : VisualElement
    {
        public event Action<CardElement> OnEditClicked;
        public event Action<CardElement> OnCompletionToggled;

        public KanbanCard Card { get; private set; }
        public KanbanBoard Board { get; private set; }
        public string ColumnId { get; private set; }

        private VisualElement _mainRow;
        private VisualElement _checkContainer;
        private Label _checkLabel;
        private VisualElement _contentWrapper;
        private Label _titleLabel;
        private Label _descriptionLabel;
        private VisualElement _footer;
        private VisualElement _editButton;
        private bool _potentialDrag;
        private bool _didDrag;
        private Vector2 _dragStartPos;
        private bool _isHovered;
        private bool _isCheckHovered;

        // Animation durations
        private const float HOVER_DURATION = 0.8f;
        private const float CHECK_DURATION = 0.4f;
        private const UIAnimator.EaseType SMOOTH_EASE = UIAnimator.EaseType.EaseOutCubic;

        public CardElement(KanbanCard card, KanbanBoard board, string columnId)
        {
            Card = card;
            Board = board;
            ColumnId = columnId;

            AddToClassList("kanban-card");
            if (card.isCompleted)
                AddToClassList("completed");
            UpdatePriorityClass();

            style.position = Position.Relative;

            BuildUI();
            RegisterCallbacks();
        }

        private void BuildUI()
        {
            // Main horizontal container with vertical centering
            _mainRow = new VisualElement();
            _mainRow.style.flexDirection = FlexDirection.Row;
            _mainRow.style.alignItems = Align.Center;
            Add(_mainRow);

            // Check container (starts at width 0, animates on hover) - smaller offset
            _checkContainer = new VisualElement();
            _checkContainer.AddToClassList("check-container");
            _checkContainer.style.width = Card.isCompleted ? 20 : 0;
            _checkContainer.style.marginRight = Card.isCompleted ? 4 : 0;
            _checkContainer.RegisterCallback<MouseEnterEvent>(evt => OnCheckHoverEnter());
            _checkContainer.RegisterCallback<MouseLeaveEvent>(evt => OnCheckHoverLeave());
            _checkContainer.RegisterCallback<MouseDownEvent>(OnCheckClick);
            _mainRow.Add(_checkContainer);

            // Check label - bigger
            _checkLabel = new Label(Card.isCompleted ? "✓" : "○");
            _checkLabel.AddToClassList("check-label");
            _checkLabel.style.opacity = Card.isCompleted ? 1 : 0;
            _checkContainer.Add(_checkLabel);

            // Content wrapper
            _contentWrapper = new VisualElement();
            _contentWrapper.AddToClassList("card-content");
            _contentWrapper.style.flexGrow = 1;
            _contentWrapper.style.justifyContent = Justify.Center;
            _mainRow.Add(_contentWrapper);

            // Header with title
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            _contentWrapper.Add(headerRow);

            // Title
            _titleLabel = new Label(Card.title);
            _titleLabel.AddToClassList("card-title");
            _titleLabel.style.flexGrow = 1;
            _titleLabel.style.marginBottom = 0;
            if (Card.isCompleted)
                _titleLabel.AddToClassList("completed-text");
            headerRow.Add(_titleLabel);

            // Edit button - use VisualElement with label to avoid Button cursor
            _editButton = new VisualElement();
            _editButton.AddToClassList("card-edit-button");
            var editLabel = new Label("•••");
            editLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _editButton.Add(editLabel);
            _editButton.RegisterCallback<MouseDownEvent>(evt =>
            {
                evt.StopPropagation();
                OnEditClicked?.Invoke(this);
            });
            Add(_editButton);

            // Description
            if (!string.IsNullOrEmpty(Card.description))
            {
                _descriptionLabel = new Label(TruncateText(Card.description, 80));
                _descriptionLabel.AddToClassList("card-description");
                if (Card.isCompleted)
                    _descriptionLabel.AddToClassList("completed-text");
                _contentWrapper.Add(_descriptionLabel);
            }

            // Footer with tags and assignees
            _footer = new VisualElement();
            _footer.AddToClassList("card-footer");
            _contentWrapper.Add(_footer);

            RefreshFooter();
        }

        private void OnCheckHoverEnter()
        {
            _isCheckHovered = true;
            UIAnimator.AnimateScale(_checkLabel, 1.2f, 0.15f, UIAnimator.EaseType.EaseOut);
        }

        private void OnCheckHoverLeave()
        {
            _isCheckHovered = false;
            UIAnimator.AnimateScale(_checkLabel, 1f, 0.15f, UIAnimator.EaseType.EaseOut);
        }

        private void OnCheckClick(MouseDownEvent evt)
        {
            evt.StopPropagation();
            Card.isCompleted = !Card.isCompleted;
            Card.MarkUpdated();

            if (Card.isCompleted)
            {
                _checkLabel.text = "✓";
                AddToClassList("completed");
                _titleLabel.AddToClassList("completed-text");
                _descriptionLabel?.AddToClassList("completed-text");

                _checkLabel.style.scale = new Scale(new Vector2(0.3f, 0.3f));
                UIAnimator.AnimateScale(
                    _checkLabel,
                    1f,
                    CHECK_DURATION,
                    UIAnimator.EaseType.EaseOut
                );
                UIAnimator.AnimateOpacity(_checkLabel, 1f, 0.15f, UIAnimator.EaseType.EaseOut);
            }
            else
            {
                _checkLabel.text = "○";
                RemoveFromClassList("completed");
                _titleLabel.RemoveFromClassList("completed-text");
                _descriptionLabel?.RemoveFromClassList("completed-text");
                UIAnimator.AnimateOpacity(_checkLabel, 0.4f, 0.15f, UIAnimator.EaseType.EaseOut);
            }

            OnCompletionToggled?.Invoke(this);
        }

        private void OnHoverEnter()
        {
            if (DragDropManager.IsDragging)
                return;
            if (_isHovered)
                return;
            _isHovered = true;

            // Smaller offset - just 20px width + 4px margin = 24px total
            UIAnimator.AnimateWidth(_checkContainer, 20, HOVER_DURATION, SMOOTH_EASE);
            UIAnimator.AnimateMarginRight(_checkContainer, 4, HOVER_DURATION, SMOOTH_EASE);

            if (!Card.isCompleted)
            {
                _checkLabel.text = "○";
            }
            UIAnimator.AnimateOpacity(
                _checkLabel,
                Card.isCompleted ? 1f : 0.4f,
                HOVER_DURATION,
                SMOOTH_EASE
            );
        }

        private void OnHoverLeave()
        {
            if (!_isHovered)
                return;
            _isHovered = false;

            if (Card.isCompleted)
                return;

            UIAnimator.AnimateWidth(_checkContainer, 0, HOVER_DURATION, SMOOTH_EASE);
            UIAnimator.AnimateMarginRight(_checkContainer, 0, HOVER_DURATION, SMOOTH_EASE);
            UIAnimator.AnimateOpacity(_checkLabel, 0, HOVER_DURATION, SMOOTH_EASE);
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            if (text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        private void RefreshFooter()
        {
            _footer.Clear();

            int tagCount = 0;
            foreach (var tag in Card.tags)
            {
                if (tagCount >= 3)
                {
                    var moreLabel = new Label($"+{Card.tags.Count - 3}");
                    moreLabel.AddToClassList("card-tag");
                    moreLabel.AddToClassList("card-tag-more");
                    _footer.Add(moreLabel);
                    break;
                }

                var tagLabel = new Label($"#{tag}");
                tagLabel.AddToClassList("card-tag");
                _footer.Add(tagLabel);
                tagCount++;
            }

            int assigneeCount = 0;
            foreach (var assigneeId in Card.assigneeIds)
            {
                if (assigneeCount >= 3)
                {
                    var moreAssignee = new VisualElement();
                    moreAssignee.AddToClassList("card-assignee");
                    moreAssignee.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
                    var moreLabel = new Label($"+{Card.assigneeIds.Count - 3}");
                    moreLabel.style.color = Color.white;
                    moreLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    moreLabel.style.fontSize = 8;
                    moreAssignee.Add(moreLabel);
                    _footer.Add(moreAssignee);
                    break;
                }

                var assignee = Board.GetAssigneeById(assigneeId);
                if (assignee != null)
                {
                    var assigneeElement = new VisualElement();
                    assigneeElement.AddToClassList("card-assignee");
                    assigneeElement.style.backgroundColor = assignee.color;

                    var initial = new Label(
                        assignee.name.Length > 0 ? assignee.name[0].ToString().ToUpper() : "?"
                    );
                    initial.style.color = Color.white;
                    initial.style.unityTextAlign = TextAnchor.MiddleCenter;
                    initial.style.fontSize = 10;
                    assigneeElement.Add(initial);

                    assigneeElement.tooltip = assignee.name;
                    _footer.Add(assigneeElement);
                    assigneeCount++;
                }
            }
        }

        private void RegisterCallbacks()
        {
            RegisterCallback<MouseEnterEvent>(evt => OnHoverEnter());
            RegisterCallback<MouseLeaveEvent>(evt => OnHoverLeave());
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (_editButton.Contains(evt.target as VisualElement))
                return;
            if (_checkContainer.Contains(evt.target as VisualElement))
                return;

            if (evt.button == 0)
            {
                _potentialDrag = true;
                _didDrag = false;
                _dragStartPos = evt.mousePosition;
                evt.StopPropagation();
            }
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_potentialDrag)
                return;

            var delta = evt.mousePosition - _dragStartPos;

            if (delta.magnitude > 8 && !DragDropManager.IsDragging)
            {
                _didDrag = true;
                DragDropManager.StartCardDrag(
                    this,
                    Card.id,
                    ColumnId,
                    evt.mousePosition,
                    Card,
                    Board
                );
                _potentialDrag = false;
            }
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (_potentialDrag && !_didDrag && evt.button == 0)
            {
                OnEditClicked?.Invoke(this);
            }
            _potentialDrag = false;
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
            UpdatePriorityClass();
            RefreshFooter();
        }
    }
}
