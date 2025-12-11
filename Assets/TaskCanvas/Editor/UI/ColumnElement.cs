using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class ColumnElement : VisualElement
    {
        public event Action<ColumnElement, KanbanCard> OnCardAdded;
        public event Action OnDataChanged;

        public KanbanColumn Column { get; private set; }
        public KanbanBoard Board { get; private set; }
        public int ColumnIndex { get; private set; }

        private VisualElement _header;
        private Label _titleLabel;
        private TextField _titleField;
        private Label _countLabel;
        private VisualElement _editButton;
        private ScrollView _cardsScroll;
        private VisualElement _cardsContainer;
        private VisualElement _addButton;
        private bool _isEditing;
        private bool _potentialDrag;
        private Vector2 _dragStartPos;

        private Func<List<string>> _getActiveTagFilters;
        private Func<List<string>> _getActiveAssigneeFilters;
        private Action<ColumnElement> _onColumnEdit;
        private Action<KanbanCard> _onCardEdit;

        private int _clickCount = 0;
        private double _lastClickTime = 0;

        public ColumnElement(
            KanbanColumn column,
            KanbanBoard board,
            int columnIndex,
            Func<List<string>> getActiveTagFilters = null,
            Func<List<string>> getActiveAssigneeFilters = null,
            Action<ColumnElement> onColumnEdit = null,
            Action<KanbanCard> onCardEdit = null
        )
        {
            Column = column;
            Board = board;
            ColumnIndex = columnIndex;
            _getActiveTagFilters = getActiveTagFilters;
            _getActiveAssigneeFilters = getActiveAssigneeFilters;
            _onColumnEdit = onColumnEdit;
            _onCardEdit = onCardEdit;

            userData = column.id;
            AddToClassList("kanban-column");
            BuildUI();
            RefreshCards();
        }

        private void BuildUI()
        {
            _header = new VisualElement();
            _header.AddToClassList("column-header");
            _header.RegisterCallback<MouseDownEvent>(OnHeaderMouseDown);
            _header.RegisterCallback<MouseMoveEvent>(OnHeaderMouseMove);
            _header.RegisterCallback<MouseUpEvent>(OnHeaderMouseUp);
            Add(_header);

            _titleLabel = new Label(Column.title);
            _titleLabel.AddToClassList("column-title");
            _titleLabel.style.flexGrow = 1;
            _header.Add(_titleLabel);

            _titleField = new TextField();
            _titleField.AddToClassList("column-title-field");
            _titleField.style.flexGrow = 1;
            _titleField.style.display = DisplayStyle.None;
            _titleField.RegisterCallback<FocusOutEvent>(evt => CommitTitleEdit());
            _titleField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    CommitTitleEdit();
                    evt.StopPropagation();
                    evt.PreventDefault();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    CancelTitleEdit();
                    evt.StopPropagation();
                }
            });
            _header.Add(_titleField);

            _countLabel = new Label("0");
            _countLabel.AddToClassList("column-count");
            _header.Add(_countLabel);

            _editButton = new VisualElement();
            _editButton.AddToClassList("column-edit-button");
            var editLabel = new Label("✎");
            editLabel.style.fontSize = 14;
            editLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            editLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _editButton.Add(editLabel);
            _editButton.RegisterCallback<MouseDownEvent>(evt =>
            {
                evt.StopPropagation();
                _onColumnEdit?.Invoke(this);
            });
            _editButton.RegisterCallback<MouseEnterEvent>(evt =>
                editLabel.style.color = new Color(0.9f, 0.9f, 0.9f)
            );
            _editButton.RegisterCallback<MouseLeaveEvent>(evt =>
                editLabel.style.color = new Color(0.5f, 0.5f, 0.5f)
            );
            _header.Add(_editButton);

            _cardsScroll = new ScrollView(ScrollViewMode.Vertical);
            _cardsScroll.AddToClassList("column-cards-scroll");
            _cardsScroll.style.flexGrow = 1;
            Add(_cardsScroll);

            _cardsContainer = new VisualElement();
            _cardsContainer.AddToClassList("column-cards");
            _cardsScroll.Add(_cardsContainer);

            _addButton = new VisualElement();
            _addButton.AddToClassList("column-add-button");
            var addLabel = new Label("+ Add Card");
            addLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            addLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            _addButton.Add(addLabel);
            _addButton.RegisterCallback<MouseDownEvent>(evt =>
            {
                evt.StopPropagation();
                OnCardAdded?.Invoke(this, null);
            });
            _addButton.RegisterCallback<MouseEnterEvent>(evt =>
                addLabel.style.color = new Color(0.9f, 0.9f, 0.9f)
            );
            _addButton.RegisterCallback<MouseLeaveEvent>(evt =>
                addLabel.style.color = new Color(0.5f, 0.5f, 0.5f)
            );
            Add(_addButton);
        }

        private void OnHeaderMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0)
                return;
            if (_editButton.Contains(evt.target as VisualElement))
                return;
            if (_isEditing)
                return;

            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - _lastClickTime < 0.3)
            {
                _clickCount++;
            }
            else
            {
                _clickCount = 1;
            }
            _lastClickTime = currentTime;

            if (_clickCount >= 2)
            {
                StartTitleEdit();
                _clickCount = 0;
                evt.StopPropagation();
                return;
            }

            _potentialDrag = true;
            _dragStartPos = evt.mousePosition;
            evt.StopPropagation();
        }

        private void OnHeaderMouseMove(MouseMoveEvent evt)
        {
            if (!_potentialDrag)
                return;

            var delta = evt.mousePosition - _dragStartPos;
            if (delta.magnitude > 5 && !DragDropManager.IsDragging)
            {
                _potentialDrag = false;
                DragDropManager.StartColumnDrag(this, ColumnIndex, _dragStartPos, Column.title);
            }
        }

        private void OnHeaderMouseUp(MouseUpEvent evt)
        {
            _potentialDrag = false;
        }

        private void StartTitleEdit()
        {
            if (_isEditing)
                return;
            _isEditing = true;

            _titleLabel.style.display = DisplayStyle.None;
            _titleField.style.display = DisplayStyle.Flex;
            _titleField.value = Column.title;

            schedule.Execute(() =>
            {
                _titleField.Focus();
                _titleField.SelectAll();
            });
        }

        private void CommitTitleEdit()
        {
            if (!_isEditing)
                return;
            _isEditing = false;

            var newTitle = _titleField.value.Trim();
            if (!string.IsNullOrEmpty(newTitle) && newTitle != Column.title)
            {
                Column.title = newTitle;
                _titleLabel.text = newTitle;
                OnDataChanged?.Invoke();
            }

            _titleField.style.display = DisplayStyle.None;
            _titleLabel.style.display = DisplayStyle.Flex;
        }

        private void CancelTitleEdit()
        {
            if (!_isEditing)
                return;
            _isEditing = false;

            _titleField.style.display = DisplayStyle.None;
            _titleLabel.style.display = DisplayStyle.Flex;
        }

        public void RefreshCards()
        {
            _cardsContainer.Clear();

            var activeTagFilters = _getActiveTagFilters?.Invoke() ?? new List<string>();
            var activeAssigneeFilters = _getActiveAssigneeFilters?.Invoke() ?? new List<string>();

            int visibleCount = 0;

            foreach (var card in Column.cards)
            {
                bool passesTagFilter =
                    activeTagFilters.Count == 0
                    || card.tags.Exists(t => activeTagFilters.Contains(t));
                bool passesAssigneeFilter =
                    activeAssigneeFilters.Count == 0
                    || card.assigneeIds.Exists(a => activeAssigneeFilters.Contains(a));

                if (!passesTagFilter || !passesAssigneeFilter)
                    continue;

                var cardElement = new CardElement(card, Board, Column.id);
                cardElement.OnEditClicked += (ce) => _onCardEdit?.Invoke(ce.Card);
                cardElement.OnCompletionToggled += (ce) => OnDataChanged?.Invoke();
                cardElement.OnDeleteClicked += (ce) =>
                {
                    Column.cards.Remove(ce.Card);
                    OnDataChanged?.Invoke();
                    RefreshCards();
                };

                _cardsContainer.Add(cardElement);
                visibleCount++;
            }

            _countLabel.text = visibleCount.ToString();
        }
    }
}
