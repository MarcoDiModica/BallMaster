using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    /// <summary>
    /// Visual Element for displaying a Kanban column with its cards.
    /// </summary>
    public class ColumnElement : VisualElement
    {
        public event Action<ColumnElement, KanbanCard> OnCardAdded;
        public event Action OnDataChanged;

        public KanbanColumn Column { get; private set; }
        public KanbanBoard Board { get; private set; }
        public int ColumnIndex { get; private set; }

        private VisualElement _header;
        private Label _titleLabel;
        private Label _countLabel;
        private Button _editButton;
        private ScrollView _cardsScroll;
        private VisualElement _cardsContainer;
        private Button _addButton;

        private Func<List<string>> _getActiveTagFilters;
        private Func<List<string>> _getActiveAssigneeFilters;
        private Action<ColumnElement> _onColumnEdit;
        private Action<KanbanCard> _onCardEdit;

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
            // Header
            _header = new VisualElement();
            _header.AddToClassList("column-header");
            _header.style.borderTopColor = Column.headerColor;
            _header.style.borderTopWidth = 3;
            Add(_header);

            // Drag handle
            var dragHandle = new VisualElement();
            dragHandle.AddToClassList("column-drag-handle");
            dragHandle.RegisterCallback<MouseDownEvent>(OnColumnDragStart);
            _header.Add(dragHandle);

            var dragIcon = new Label("⋮⋮");
            dragIcon.style.color = new Color(0.5f, 0.5f, 0.5f);
            dragIcon.style.marginRight = 8;
            dragHandle.Add(dragIcon);

            _titleLabel = new Label(Column.title);
            _titleLabel.AddToClassList("column-title");
            _titleLabel.style.flexGrow = 1;
            _header.Add(_titleLabel);

            _countLabel = new Label("0");
            _countLabel.AddToClassList("column-count");
            _header.Add(_countLabel);

            _editButton = new Button(() => _onColumnEdit?.Invoke(this));
            _editButton.text = "✏️";
            _editButton.AddToClassList("edit-button");
            _header.Add(_editButton);

            // Cards scroll container
            _cardsScroll = new ScrollView(ScrollViewMode.Vertical);
            _cardsScroll.AddToClassList("column-cards-scroll");
            _cardsScroll.style.flexGrow = 1;
            Add(_cardsScroll);

            _cardsContainer = new VisualElement();
            _cardsContainer.AddToClassList("column-cards");
            _cardsScroll.Add(_cardsContainer);

            // Add button
            _addButton = new Button(OnAddCardClicked);
            _addButton.text = "+ Add Card";
            _addButton.AddToClassList("column-add-button");
            Add(_addButton);
        }

        private void OnColumnDragStart(MouseDownEvent evt)
        {
            if (evt.button != 0)
                return;
            evt.StopPropagation();

            DragDropManager.StartColumnDrag(this, ColumnIndex, evt.mousePosition, Column.title);
        }

        private void OnAddCardClicked()
        {
            OnCardAdded?.Invoke(this, null);
        }

        public void RefreshCards()
        {
            _cardsContainer.Clear();

            var activeTagFilters = _getActiveTagFilters?.Invoke() ?? new List<string>();
            var activeAssigneeFilters = _getActiveAssigneeFilters?.Invoke() ?? new List<string>();

            int visibleCount = 0;

            foreach (var card in Column.cards)
            {
                // Apply filters
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

                _cardsContainer.Add(cardElement);
                visibleCount++;
            }

            _countLabel.text = visibleCount.ToString();
        }
    }
}
