using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class CardElement : VisualElement
    {
        public event Action<CardElement> OnEditClicked;
        public event Action<CardElement> OnCompletionToggled;
        public event Action<CardElement> OnDeleteClicked;

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
        private VisualElement _buttonsContainer;
        private VisualElement _editButton;
        private VisualElement _deleteButton;
        private bool _potentialDrag;
        private bool _didDrag;
        private Vector2 _dragStartPos;
        private bool _isHovered;

        private const float HOVER_DURATION = 0.4f;
        private const float CHECK_DURATION = 0.3f;
        private const UIAnimator.EaseType SMOOTH_EASE = UIAnimator.EaseType.EaseOut;

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
            _mainRow = new VisualElement();
            _mainRow.style.flexDirection = FlexDirection.Row;
            _mainRow.style.alignItems = Align.FlexStart;
            Add(_mainRow);

            _checkContainer = new VisualElement();
            _checkContainer.AddToClassList("check-container");
            _checkContainer.style.width = Card.isCompleted ? 20 : 0;
            _checkContainer.style.marginRight = Card.isCompleted ? 4 : 0;
            _checkContainer.RegisterCallback<MouseEnterEvent>(evt => OnCheckHoverEnter());
            _checkContainer.RegisterCallback<MouseLeaveEvent>(evt => OnCheckHoverLeave());
            _checkContainer.RegisterCallback<MouseDownEvent>(OnCheckClick);
            _mainRow.Add(_checkContainer);

            _checkLabel = new Label(Card.isCompleted ? "✓" : "○");
            _checkLabel.AddToClassList("check-label");
            _checkLabel.style.opacity = Card.isCompleted ? 1 : 0;
            _checkContainer.Add(_checkLabel);

            _contentWrapper = new VisualElement();
            _contentWrapper.AddToClassList("card-content");
            _contentWrapper.style.flexGrow = 1;
            _mainRow.Add(_contentWrapper);

            _titleLabel = new Label(Card.title);
            _titleLabel.AddToClassList("card-title");
            if (Card.isCompleted)
                _titleLabel.AddToClassList("completed-text");
            _contentWrapper.Add(_titleLabel);

            if (!string.IsNullOrEmpty(Card.description))
            {
                _descriptionLabel = new Label(TruncateText(Card.description, 80));
                _descriptionLabel.AddToClassList("card-description");
                if (Card.isCompleted)
                    _descriptionLabel.AddToClassList("completed-text");
                _contentWrapper.Add(_descriptionLabel);
            }

            _footer = new VisualElement();
            _footer.AddToClassList("card-footer");
            _contentWrapper.Add(_footer);

            RefreshFooter();

            _buttonsContainer = new VisualElement();
            _buttonsContainer.AddToClassList("card-buttons-container");
            _buttonsContainer.style.position = Position.Absolute;
            _buttonsContainer.style.top = 6;
            _buttonsContainer.style.right = 6;
            _buttonsContainer.style.flexDirection = FlexDirection.Row;
            _buttonsContainer.style.alignItems = Align.Center;
            _buttonsContainer.style.opacity = 0;
            Add(_buttonsContainer);

            _deleteButton = new VisualElement();
            _deleteButton.AddToClassList("card-action-button");
            _deleteButton.style.width = 20;
            _deleteButton.style.height = 20;
            _deleteButton.style.justifyContent = Justify.Center;
            _deleteButton.style.alignItems = Align.Center;
            var deleteLabel = new Label("×");
            deleteLabel.style.fontSize = 16;
            deleteLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            deleteLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            deleteLabel.style.marginTop = -2;
            _deleteButton.Add(deleteLabel);
            _deleteButton.RegisterCallback<MouseDownEvent>(evt =>
            {
                evt.StopPropagation();
                if (
                    EditorUtility.DisplayDialog(
                        "Delete Card",
                        $"Delete '{Card.title}'?",
                        "Delete",
                        "Cancel"
                    )
                )
                {
                    OnDeleteClicked?.Invoke(this);
                }
            });
            _deleteButton.RegisterCallback<MouseEnterEvent>(evt =>
                deleteLabel.style.color = new Color(1f, 0.4f, 0.4f)
            );
            _deleteButton.RegisterCallback<MouseLeaveEvent>(evt =>
                deleteLabel.style.color = new Color(0.5f, 0.5f, 0.5f)
            );
            _buttonsContainer.Add(_deleteButton);

            _editButton = new VisualElement();
            _editButton.AddToClassList("card-action-button");
            _editButton.style.width = 20;
            _editButton.style.height = 20;
            _editButton.style.justifyContent = Justify.Center;
            _editButton.style.alignItems = Align.Center;
            var editLabel = new Label("✎");
            editLabel.style.fontSize = 12;
            editLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            editLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _editButton.Add(editLabel);
            _editButton.RegisterCallback<MouseDownEvent>(evt =>
            {
                evt.StopPropagation();
                OnEditClicked?.Invoke(this);
            });
            _editButton.RegisterCallback<MouseEnterEvent>(evt =>
                editLabel.style.color = new Color(1f, 1f, 1f)
            );
            _editButton.RegisterCallback<MouseLeaveEvent>(evt =>
                editLabel.style.color = new Color(0.5f, 0.5f, 0.5f)
            );
            _buttonsContainer.Add(_editButton);
        }

        private void OnCheckHoverEnter()
        {
            UIAnimator.AnimateScale(_checkLabel, 1.2f, 0.15f, UIAnimator.EaseType.EaseOut);
        }

        private void OnCheckHoverLeave()
        {
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

            AddToClassList("card-hover");

            UIAnimator.AnimateWidth(_checkContainer, 20, HOVER_DURATION, SMOOTH_EASE);
            UIAnimator.AnimateMarginRight(_checkContainer, 4, HOVER_DURATION, SMOOTH_EASE);
            UIAnimator.AnimateOpacity(_buttonsContainer, 1f, 0.2f, UIAnimator.EaseType.EaseOut);

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

            RemoveFromClassList("card-hover");

            UIAnimator.AnimateOpacity(_buttonsContainer, 0f, 0.2f, UIAnimator.EaseType.EaseOut);

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
            if (_buttonsContainer.Contains(evt.target as VisualElement))
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

            if (delta.magnitude > 5 && !DragDropManager.IsDragging)
            {
                _didDrag = true;
                DragDropManager.StartCardDrag(this, Card.id, ColumnId, _dragStartPos, Card, Board);
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
