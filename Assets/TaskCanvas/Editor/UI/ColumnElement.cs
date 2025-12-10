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
        public event Action<CardElement> OnCardClicked;
        public event Action<string, string, int> OnCardDropped; // cardId, targetColumnId, targetIndex

        public KanbanColumn Column { get; private set; }
        public KanbanBoard Board { get; private set; }

        private VisualElement _header;
        private Label _titleLabel;
        private Label _countLabel;
        private ScrollView _cardsScroll;
        private VisualElement _cardsContainer;
        private Button _addButton;
        private VisualElement _dropZone;

        private List<CardElement> _cardElements = new List<CardElement>();
        private Func<List<string>> _getActiveTagFilters;
        private Func<List<string>> _getActiveAssigneeFilters;

        public ColumnElement(
            KanbanColumn column,
            KanbanBoard board,
            Func<List<string>> getActiveTagFilters = null,
            Func<List<string>> getActiveAssigneeFilters = null
        )
        {
            Column = column;
            Board = board;
            _getActiveTagFilters = getActiveTagFilters;
            _getActiveAssigneeFilters = getActiveAssigneeFilters;

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
            Add(_header);

            _titleLabel = new Label(Column.title);
            _titleLabel.AddToClassList("column-title");
            _header.Add(_titleLabel);

            _countLabel = new Label("0");
            _countLabel.AddToClassList("column-count");
            _header.Add(_countLabel);

            // Cards scroll container
            _cardsScroll = new ScrollView(ScrollViewMode.Vertical);
            _cardsScroll.AddToClassList("column-cards-scroll");
            Add(_cardsScroll);

            _cardsContainer = new VisualElement();
            _cardsContainer.AddToClassList("column-cards");
            _cardsScroll.Add(_cardsContainer);

            // Drop zone
            _dropZone = new VisualElement();
            _dropZone.AddToClassList("drop-zone");
            _cardsContainer.Add(_dropZone);

            // Add button
            _addButton = new Button(OnAddCardClicked);
            _addButton.text = "+ Add Card";
            _addButton.AddToClassList("column-add-button");
            Add(_addButton);

            // Register drop callbacks
            RegisterCallback<DragEnterEvent>(OnDragEnter);
            RegisterCallback<DragLeaveEvent>(OnDragLeave);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        private void OnAddCardClicked()
        {
            var newCard = new KanbanCard("New Task");
            Column.cards.Add(newCard);
            EditorUtility.SetDirty(Board);
            OnCardAdded?.Invoke(this, newCard);
            RefreshCards();
        }

        public void RefreshCards()
        {
            // Clear existing
            _cardElements.Clear();
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

                var cardElement = new CardElement(card, Board);
                cardElement.OnCardClicked += (ce) => OnCardClicked?.Invoke(ce);
                cardElement.OnDragStarted += OnCardDragStarted;

                _cardsContainer.Add(cardElement);
                _cardElements.Add(cardElement);
                visibleCount++;
            }

            // Re-add drop zone at the end
            _dropZone = new VisualElement();
            _dropZone.AddToClassList("drop-zone");
            _cardsContainer.Add(_dropZone);

            _countLabel.text = visibleCount.ToString();
        }

        private void OnCardDragStarted(CardElement cardElement)
        {
            cardElement.SetDragging(true);
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("CardId", cardElement.Card.id);
            DragAndDrop.SetGenericData("SourceColumnId", Column.id);
            DragAndDrop.StartDrag("Dragging Card");
        }

        private void OnDragEnter(DragEnterEvent evt)
        {
            if (DragAndDrop.GetGenericData("CardId") != null)
            {
                _dropZone.AddToClassList("drag-over");
            }
        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
            _dropZone.RemoveFromClassList("drag-over");
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.GetGenericData("CardId") != null)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            }
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            _dropZone.RemoveFromClassList("drag-over");

            var cardId = DragAndDrop.GetGenericData("CardId") as string;
            if (!string.IsNullOrEmpty(cardId))
            {
                DragAndDrop.AcceptDrag();
                OnCardDropped?.Invoke(cardId, Column.id, -1);
            }
        }

        public void HighlightDropZone(bool highlight)
        {
            if (highlight)
                _dropZone.AddToClassList("drag-over");
            else
                _dropZone.RemoveFromClassList("drag-over");
        }
    }
}
