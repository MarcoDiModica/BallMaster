using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public static class DragDropManager
    {
        public static bool IsDragging { get; private set; }
        public static string DraggedCardId { get; private set; }
        public static string SourceColumnId { get; private set; }
        public static int DraggedColumnIndex { get; private set; } = -1;

        private static VisualElement _draggedElement;
        private static VisualElement _ghostElement;
        private static VisualElement _placeholder;
        private static VisualElement _root;
        private static VisualElement _columnsContainer;

        private static Action<string, string, int> _onCardDropped;
        private static Action<int, int> _onColumnReorder;

        private static int _currentPlaceholderColumnIndex = -1;
        private static int _currentPlaceholderCardIndex = -1;
        private static string _currentPlaceholderColumnId;
        private static Vector2 _dragOffset;
        private static float _ghostWidth;
        private static float _ghostHeight;
        private static int _originalCardIndex;

        private static Vector2 _targetGhostPos;
        private static Vector2 _currentGhostPos;
        private static float _currentTilt;
        private static float _targetTilt;
        private static Vector2 _lastMousePos;
        private static double _lastDragTime = -1;
        private const float SMOOTH_SPEED = 30f;

        public static void Initialize(
            VisualElement root,
            Action<string, string, int> onCardDropped,
            Action<int, int> onColumnReorder,
            Action onRefresh
        )
        {
            _root = root;
            _onCardDropped = onCardDropped;
            _onColumnReorder = onColumnReorder;

            _root.RegisterCallback<MouseMoveEvent>(OnGlobalMouseMove, TrickleDown.TrickleDown);
            _root.RegisterCallback<MouseUpEvent>(OnGlobalMouseUp, TrickleDown.TrickleDown);

            EditorApplication.update += UpdateSmoothDrag;
        }

        private static void UpdateSmoothDrag()
        {
            if (!IsDragging || _ghostElement == null)
            {
                _lastDragTime = -1;
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime;
            if (_lastDragTime < 0)
            {
                deltaTime = 0.016f;
            }
            else
            {
                deltaTime = (float)(currentTime - _lastDragTime);
            }
            _lastDragTime = currentTime;
            deltaTime = Mathf.Clamp(deltaTime, 0.001f, 0.1f);

            float t = 1f - Mathf.Exp(-SMOOTH_SPEED * deltaTime);
            _currentGhostPos = Vector2.Lerp(_currentGhostPos, _targetGhostPos, t);
            _ghostElement.style.left = _currentGhostPos.x;
            _ghostElement.style.top = _currentGhostPos.y;

            _currentTilt = Mathf.Lerp(_currentTilt, _targetTilt, t);
            _ghostElement.style.rotate = new Rotate(_currentTilt);

            _targetTilt *= 0.9f;
        }

        public static void StartCardDrag(
            VisualElement cardElement,
            string cardId,
            string columnId,
            Vector2 grabPosition,
            KanbanCard card,
            KanbanBoard board
        )
        {
            if (IsDragging)
                return;

            IsDragging = true;
            DraggedCardId = cardId;
            SourceColumnId = columnId;
            DraggedColumnIndex = -1;
            _draggedElement = cardElement;

            _columnsContainer = _root.Q<VisualElement>(className: "columns-container");
            _ghostWidth = cardElement.resolvedStyle.width;
            _ghostHeight = cardElement.resolvedStyle.height;

            var cardRect = cardElement.worldBound;
            _dragOffset = new Vector2(grabPosition.x - cardRect.x, cardRect.height * 0.5f);

            _currentGhostPos = new Vector2(cardRect.x, cardRect.y);
            _targetGhostPos = _currentGhostPos;
            _currentTilt = 0;
            _targetTilt = 0;
            _lastMousePos = grabPosition;

            cardElement.style.display = DisplayStyle.None;

            CreateCardGhost(card, board);
            CreateCardPlaceholder();

            var parent = cardElement.parent;
            _originalCardIndex = parent.IndexOf(cardElement);
            parent.Insert(_originalCardIndex, _placeholder);

            _currentPlaceholderColumnId = columnId;
            _currentPlaceholderCardIndex = _originalCardIndex;
        }

        public static void StartColumnDrag(
            VisualElement columnElement,
            int columnIndex,
            Vector2 mousePos,
            string columnTitle
        )
        {
            if (IsDragging)
                return;

            IsDragging = true;
            DraggedCardId = null;
            SourceColumnId = null;
            DraggedColumnIndex = columnIndex;
            _draggedElement = columnElement;

            _columnsContainer = _root.Q<VisualElement>(className: "columns-container");
            _ghostWidth = columnElement.resolvedStyle.width;
            _ghostHeight = columnElement.resolvedStyle.height;

            var colRect = columnElement.worldBound;
            _dragOffset = new Vector2(mousePos.x - colRect.x, mousePos.y - colRect.y);

            _currentGhostPos = new Vector2(colRect.x, colRect.y);
            _targetGhostPos = _currentGhostPos;
            _currentTilt = 0;
            _targetTilt = 0;
            _lastMousePos = mousePos;

            CreateColumnGhost(columnTitle);
            CreateColumnPlaceholder();

            if (_columnsContainer != null)
            {
                _columnsContainer.Insert(columnIndex, _placeholder);
            }

            columnElement.style.display = DisplayStyle.None;
            _currentPlaceholderColumnIndex = columnIndex;
        }

        private static void CreateCardGhost(KanbanCard card, KanbanBoard board)
        {
            _ghostElement = new VisualElement();
            _ghostElement.AddToClassList("drag-ghost-card");
            _ghostElement.style.position = Position.Absolute;
            _ghostElement.style.width = _ghostWidth;
            _ghostElement.pickingMode = PickingMode.Ignore;
            _ghostElement.style.opacity = 0.9f;
            _ghostElement.style.backgroundColor = new Color(0.18f, 0.18f, 0.19f);
            _ghostElement.style.borderTopLeftRadius = 6;
            _ghostElement.style.borderTopRightRadius = 6;
            _ghostElement.style.borderBottomLeftRadius = 6;
            _ghostElement.style.borderBottomRightRadius = 6;
            _ghostElement.style.paddingTop = 10;
            _ghostElement.style.paddingBottom = 10;
            _ghostElement.style.paddingLeft = 12;
            _ghostElement.style.paddingRight = 12;
            _ghostElement.style.borderLeftWidth = 4;

            switch (card.priority)
            {
                case 0:
                    _ghostElement.style.borderLeftColor = new Color(0.42f, 0.80f, 0.47f);
                    break;
                case 1:
                    _ghostElement.style.borderLeftColor = new Color(1f, 0.85f, 0.24f);
                    break;
                case 2:
                    _ghostElement.style.borderLeftColor = new Color(1f, 0.42f, 0.42f);
                    break;
                default:
                    _ghostElement.style.borderLeftColor = new Color(0.29f, 0.62f, 1f);
                    break;
            }

            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.alignItems = Align.Center;
            _ghostElement.Add(mainRow);

            if (card.isCompleted)
            {
                var checkLabel = new Label("✓");
                checkLabel.style.fontSize = 14;
                checkLabel.style.color = new Color(0.42f, 0.80f, 0.47f);
                checkLabel.style.marginRight = 6;
                mainRow.Add(checkLabel);
            }

            var contentWrapper = new VisualElement();
            contentWrapper.style.flexGrow = 1;
            mainRow.Add(contentWrapper);

            var title = new Label(card.title);
            title.style.fontSize = 13;
            title.style.color = new Color(0.88f, 0.88f, 0.88f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            if (card.isCompleted)
                title.style.opacity = 0.5f;
            contentWrapper.Add(title);

            if (!string.IsNullOrEmpty(card.description))
            {
                var desc = new Label(TruncateText(card.description, 50));
                desc.style.fontSize = 11;
                desc.style.color = new Color(0.53f, 0.53f, 0.53f);
                desc.style.marginTop = 2;
                contentWrapper.Add(desc);
            }

            _ghostElement.style.left = _currentGhostPos.x;
            _ghostElement.style.top = _currentGhostPos.y;
            _root.Add(_ghostElement);
        }

        private static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            if (text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        private static void CreateColumnGhost(string columnTitle)
        {
            _ghostElement = new VisualElement();
            _ghostElement.AddToClassList("drag-ghost-column");
            _ghostElement.style.position = Position.Absolute;
            _ghostElement.style.width = _ghostWidth;
            _ghostElement.style.height = 80;
            _ghostElement.pickingMode = PickingMode.Ignore;
            _ghostElement.style.opacity = 0.85f;
            _ghostElement.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            _ghostElement.style.borderTopLeftRadius = 8;
            _ghostElement.style.borderTopRightRadius = 8;
            _ghostElement.style.borderBottomLeftRadius = 8;
            _ghostElement.style.borderBottomRightRadius = 8;
            _ghostElement.style.paddingTop = 10;
            _ghostElement.style.paddingLeft = 12;

            var title = new Label(columnTitle);
            title.style.fontSize = 14;
            title.style.color = new Color(0.88f, 0.88f, 0.88f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _ghostElement.Add(title);

            _ghostElement.style.left = _currentGhostPos.x;
            _ghostElement.style.top = _currentGhostPos.y;
            _root.Add(_ghostElement);
        }

        private static void CreateCardPlaceholder()
        {
            _placeholder = new VisualElement();
            _placeholder.AddToClassList("drag-placeholder");
            _placeholder.style.height = _ghostHeight;
            _placeholder.style.marginBottom = 4;
        }

        private static void CreateColumnPlaceholder()
        {
            _placeholder = new VisualElement();
            _placeholder.AddToClassList("drag-placeholder-column");
            _placeholder.style.width = _ghostWidth;
            _placeholder.style.minWidth = 280;
            _placeholder.style.height = _ghostHeight;
            _placeholder.style.marginRight = 12;
        }

        private static void OnGlobalMouseMove(MouseMoveEvent evt)
        {
            if (!IsDragging || _placeholder == null)
                return;

            _targetGhostPos = new Vector2(
                evt.mousePosition.x - _dragOffset.x,
                evt.mousePosition.y - _dragOffset.y
            );

            float deltaX = evt.mousePosition.x - _lastMousePos.x;
            _targetTilt = Mathf.Clamp(deltaX * 0.3f, -5f, 5f);
            _lastMousePos = evt.mousePosition;

            if (DraggedCardId != null)
            {
                UpdateCardPlaceholder(evt.mousePosition);
            }
            else if (DraggedColumnIndex >= 0)
            {
                UpdateColumnPlaceholder(evt.mousePosition);
            }
        }

        private static void UpdateCardPlaceholder(Vector2 mousePos)
        {
            if (_columnsContainer == null)
                return;

            VisualElement targetColumn = null;
            string targetColumnId = null;

            foreach (var child in _columnsContainer.Children())
            {
                if (!child.ClassListContains("kanban-column"))
                    continue;
                if (child.style.display == DisplayStyle.None)
                    continue;

                var colRect = child.worldBound;
                if (mousePos.x >= colRect.x && mousePos.x <= colRect.xMax)
                {
                    targetColumn = child;
                    targetColumnId = child.userData as string;
                    break;
                }
            }

            if (targetColumn == null || targetColumnId == null)
                return;

            var cardsContainer = targetColumn.Q<VisualElement>(className: "column-cards");
            if (cardsContainer == null)
                return;

            int insertIdx = 0;
            bool foundPosition = false;

            var children = new List<VisualElement>();
            foreach (var c in cardsContainer.Children())
            {
                if (
                    c != _placeholder
                    && c.ClassListContains("kanban-card")
                    && c.style.display != DisplayStyle.None
                )
                {
                    children.Add(c);
                }
            }

            for (int i = 0; i < children.Count; i++)
            {
                var cardChild = children[i];
                var cardRect = cardChild.worldBound;
                float cardMidY = cardRect.y + cardRect.height * 0.5f;

                if (mousePos.y < cardMidY)
                {
                    insertIdx = i;
                    foundPosition = true;
                    break;
                }
            }

            if (!foundPosition)
            {
                insertIdx = children.Count;
            }

            if (
                targetColumnId != _currentPlaceholderColumnId
                || insertIdx != _currentPlaceholderCardIndex
            )
            {
                _placeholder.RemoveFromHierarchy();

                int actualInsertIndex = 0;
                int cardsSeen = 0;
                foreach (var c in cardsContainer.Children())
                {
                    if (c == _placeholder)
                        continue;
                    if (cardsSeen == insertIdx)
                        break;
                    if (c.ClassListContains("kanban-card") && c.style.display != DisplayStyle.None)
                    {
                        cardsSeen++;
                    }
                    actualInsertIndex++;
                }

                if (cardsSeen < insertIdx)
                {
                    actualInsertIndex = cardsContainer.childCount;
                }

                cardsContainer.Insert(actualInsertIndex, _placeholder);
                _currentPlaceholderColumnId = targetColumnId;
                _currentPlaceholderCardIndex = insertIdx;
            }
        }

        private static void UpdateColumnPlaceholder(Vector2 mousePos)
        {
            if (_columnsContainer == null)
                return;

            int insertIdx = 0;
            int columnCount = 0;

            foreach (var child in _columnsContainer.Children())
            {
                if (child == _placeholder)
                    continue;
                if (!child.ClassListContains("kanban-column"))
                    continue;
                if (child.style.display == DisplayStyle.None)
                    continue;
                columnCount++;
            }

            int visibleIdx = 0;
            foreach (var child in _columnsContainer.Children())
            {
                if (child == _placeholder)
                    continue;
                if (!child.ClassListContains("kanban-column"))
                    continue;
                if (child.style.display == DisplayStyle.None)
                    continue;

                var colRect = child.worldBound;
                if (mousePos.x < colRect.x + colRect.width * 0.5f)
                {
                    insertIdx = visibleIdx;
                    break;
                }
                visibleIdx++;
                insertIdx = visibleIdx;
            }

            insertIdx = Mathf.Clamp(insertIdx, 0, columnCount);

            if (insertIdx != _currentPlaceholderColumnIndex)
            {
                _placeholder.RemoveFromHierarchy();

                int actualIdx = 0;
                int counted = 0;
                foreach (var child in _columnsContainer.Children())
                {
                    if (child == _placeholder)
                        continue;
                    if (
                        child.ClassListContains("kanban-column")
                        && child.style.display != DisplayStyle.None
                    )
                    {
                        if (counted == insertIdx)
                            break;
                        counted++;
                    }
                    actualIdx++;
                }

                actualIdx = Mathf.Clamp(actualIdx, 0, _columnsContainer.childCount);
                _columnsContainer.Insert(actualIdx, _placeholder);
                _currentPlaceholderColumnIndex = insertIdx;
            }
        }

        private static void OnGlobalMouseUp(MouseUpEvent evt)
        {
            if (!IsDragging)
                return;

            if (DraggedCardId != null && _currentPlaceholderColumnId != null)
            {
                _onCardDropped?.Invoke(
                    DraggedCardId,
                    _currentPlaceholderColumnId,
                    _currentPlaceholderCardIndex
                );
            }
            else if (DraggedColumnIndex >= 0 && _currentPlaceholderColumnIndex >= 0)
            {
                _onColumnReorder?.Invoke(DraggedColumnIndex, _currentPlaceholderColumnIndex);
            }

            EndDrag();
        }

        public static void EndDrag()
        {
            if (_draggedElement != null)
            {
                _draggedElement.style.display = DisplayStyle.Flex;
                _draggedElement.style.opacity = 1f;
            }

            if (_ghostElement != null)
            {
                _ghostElement.RemoveFromHierarchy();
                _ghostElement = null;
            }

            if (_placeholder != null)
            {
                _placeholder.RemoveFromHierarchy();
                _placeholder = null;
            }

            IsDragging = false;
            DraggedCardId = null;
            SourceColumnId = null;
            DraggedColumnIndex = -1;
            _draggedElement = null;
            _columnsContainer = null;
            _currentPlaceholderColumnIndex = -1;
            _currentPlaceholderCardIndex = -1;
            _currentPlaceholderColumnId = null;
            _originalCardIndex = -1;
        }
    }
}
