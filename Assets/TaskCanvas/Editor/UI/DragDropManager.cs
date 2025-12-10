using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    /// <summary>
    /// Manages drag and drop with smooth animations.
    /// Ghost follows mouse with smoothing and tilt.
    /// </summary>
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

        // Smooth drag variables
        private static Vector2 _targetGhostPos;
        private static Vector2 _currentGhostPos;
        private static float _currentTilt;
        private static float _targetTilt;
        private static Vector2 _lastMousePos;
        private static double _lastDragTime = -1;
        private const float SMOOTH_SPEED = 12f; // Higher = faster, less lag

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

            // Calculate delta time
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

            // Smooth position interpolation with deltaTime
            float t = 1f - Mathf.Exp(-SMOOTH_SPEED * deltaTime);
            _currentGhostPos = Vector2.Lerp(_currentGhostPos, _targetGhostPos, t);
            _ghostElement.style.left = _currentGhostPos.x;
            _ghostElement.style.top = _currentGhostPos.y;

            // Smooth tilt interpolation
            _currentTilt = Mathf.Lerp(_currentTilt, _targetTilt, t);
            _ghostElement.style.rotate = new Rotate(_currentTilt);

            // Decay tilt back to 0
            _targetTilt *= 0.92f;
        }

        public static void StartCardDrag(
            VisualElement cardElement,
            string cardId,
            string columnId,
            Vector2 mousePos,
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

            // Calculate drag offset from grab point
            var cardRect = cardElement.worldBound;
            _dragOffset = new Vector2(mousePos.x - cardRect.x, mousePos.y - cardRect.y);

            // Initialize smooth drag position
            _targetGhostPos = new Vector2(cardRect.x, cardRect.y);
            _currentGhostPos = _targetGhostPos;
            _currentTilt = 0;
            _targetTilt = 0;
            _lastMousePos = mousePos;

            CreateCardGhost(cardElement, mousePos, card, board);
            CreateCardPlaceholder();

            var parent = cardElement.parent;
            var idx = parent.IndexOf(cardElement);
            parent.Insert(idx, _placeholder);

            cardElement.style.display = DisplayStyle.None;

            _currentPlaceholderColumnId = columnId;
            _currentPlaceholderCardIndex = idx;
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

            _targetGhostPos = new Vector2(colRect.x, colRect.y);
            _currentGhostPos = _targetGhostPos;
            _currentTilt = 0;
            _targetTilt = 0;
            _lastMousePos = mousePos;

            CreateColumnGhost(columnElement, mousePos, columnTitle);
            CreateColumnPlaceholder();

            if (_columnsContainer != null)
            {
                _columnsContainer.Insert(columnIndex, _placeholder);
            }

            columnElement.style.display = DisplayStyle.None;
            _currentPlaceholderColumnIndex = columnIndex;
        }

        private static void CreateCardGhost(
            VisualElement source,
            Vector2 mousePos,
            KanbanCard card,
            KanbanBoard board
        )
        {
            _ghostElement = new VisualElement();
            _ghostElement.AddToClassList("kanban-card");
            _ghostElement.AddToClassList("drag-ghost");
            _ghostElement.style.position = Position.Absolute;
            _ghostElement.style.width = _ghostWidth;
            _ghostElement.pickingMode = PickingMode.Ignore;
            _ghostElement.style.opacity = 0;

            // Start animation - scale up and fade in
            _ghostElement.style.scale = new Scale(new Vector2(0.95f, 0.95f));

            // Priority border
            switch (card.priority)
            {
                case 0:
                    _ghostElement.AddToClassList("priority-low");
                    break;
                case 1:
                    _ghostElement.AddToClassList("priority-medium");
                    break;
                case 2:
                    _ghostElement.AddToClassList("priority-high");
                    break;
            }

            // Main row for check + content
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.alignItems = Align.Center;
            _ghostElement.Add(mainRow);

            // Completed check if applicable
            if (card.isCompleted)
            {
                var checkContainer = new VisualElement();
                checkContainer.AddToClassList("check-container");
                checkContainer.style.width = 20;
                checkContainer.style.marginRight = 4;
                mainRow.Add(checkContainer);

                var checkLabel = new Label("✓");
                checkLabel.AddToClassList("check-label");
                checkLabel.style.opacity = 1;
                checkContainer.Add(checkLabel);
            }

            // Content wrapper
            var contentWrapper = new VisualElement();
            contentWrapper.style.flexGrow = 1;
            mainRow.Add(contentWrapper);

            // Title
            var title = new Label(card.title);
            title.AddToClassList("card-title");
            if (card.isCompleted)
                title.AddToClassList("completed-text");
            contentWrapper.Add(title);

            // Description
            if (!string.IsNullOrEmpty(card.description))
            {
                var desc = new Label(TruncateText(card.description, 60));
                desc.AddToClassList("card-description");
                contentWrapper.Add(desc);
            }

            // Footer with tags and assignees
            var footer = new VisualElement();
            footer.AddToClassList("card-footer");
            contentWrapper.Add(footer);

            // Tags
            int tagCount = 0;
            foreach (var tag in card.tags)
            {
                if (tagCount >= 2)
                    break;
                var tagLabel = new Label($"#{tag}");
                tagLabel.AddToClassList("card-tag");
                footer.Add(tagLabel);
                tagCount++;
            }

            // Assignees
            int assigneeCount = 0;
            foreach (var assigneeId in card.assigneeIds)
            {
                if (assigneeCount >= 3)
                    break;
                var assignee = board.GetAssigneeById(assigneeId);
                if (assignee != null)
                {
                    var assigneeEl = new VisualElement();
                    assigneeEl.AddToClassList("card-assignee");
                    assigneeEl.style.backgroundColor = assignee.color;

                    var initial = new Label(
                        assignee.name.Length > 0 ? assignee.name[0].ToString().ToUpper() : "?"
                    );
                    initial.style.color = Color.white;
                    initial.style.unityTextAlign = TextAnchor.MiddleCenter;
                    initial.style.fontSize = 10;
                    assigneeEl.Add(initial);

                    footer.Add(assigneeEl);
                    assigneeCount++;
                }
            }

            // Set initial position
            _ghostElement.style.left = _currentGhostPos.x;
            _ghostElement.style.top = _currentGhostPos.y;
            _root.Add(_ghostElement);

            // Animate ghost appearance
            UIAnimator.AnimateOpacity(_ghostElement, 0.95f, 0.2f, UIAnimator.EaseType.EaseOut);
            UIAnimator.AnimateScale(_ghostElement, 1.02f, 0.15f, UIAnimator.EaseType.EaseOut);
        }

        private static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            if (text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        private static void CreateColumnGhost(
            VisualElement source,
            Vector2 mousePos,
            string columnTitle
        )
        {
            _ghostElement = new VisualElement();
            _ghostElement.AddToClassList("kanban-column");
            _ghostElement.AddToClassList("drag-ghost");
            _ghostElement.style.position = Position.Absolute;
            _ghostElement.style.width = _ghostWidth;
            _ghostElement.style.height = 100;
            _ghostElement.pickingMode = PickingMode.Ignore;
            _ghostElement.style.opacity = 0.9f;

            var header = new VisualElement();
            header.AddToClassList("column-header");
            _ghostElement.Add(header);

            var title = new Label(columnTitle);
            title.AddToClassList("column-title");
            header.Add(title);

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

            // Calculate target position from current mouse pos and original grab offset
            _targetGhostPos = new Vector2(
                evt.mousePosition.x - _dragOffset.x,
                evt.mousePosition.y - _dragOffset.y
            );

            // Calculate tilt based on horizontal mouse velocity
            float deltaX = evt.mousePosition.x - _lastMousePos.x;
            _targetTilt = Mathf.Clamp(deltaX * 0.5f, -8f, 8f);
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

            foreach (var child in _columnsContainer.Children())
            {
                if (!child.ClassListContains("kanban-column"))
                    continue;
                if (child.style.display == DisplayStyle.None)
                    continue;

                var colRect = child.worldBound;
                if (mousePos.x >= colRect.x && mousePos.x <= colRect.xMax)
                {
                    var columnId = child.userData as string;
                    var cardsContainer = child.Q<VisualElement>(className: "column-cards");

                    if (cardsContainer == null)
                        continue;

                    int insertIdx = 0;
                    int visibleCardCount = 0;
                    bool foundPosition = false;
                    float lastCardBottom = 0;

                    foreach (var cardChild in cardsContainer.Children())
                    {
                        if (cardChild == _placeholder)
                            continue;
                        if (cardChild.style.display == DisplayStyle.None)
                            continue;

                        if (cardChild.ClassListContains("kanban-card"))
                        {
                            visibleCardCount++;

                            var cardRect = cardChild.worldBound;
                            lastCardBottom = cardRect.yMax;

                            if (!foundPosition)
                            {
                                float threshold = cardRect.y + cardRect.height * 0.4f;
                                if (mousePos.y < threshold)
                                {
                                    foundPosition = true;
                                }
                                else
                                {
                                    insertIdx++;
                                }
                            }
                        }
                    }

                    // If mouse is below all cards or no position found, insert at end
                    if (!foundPosition || mousePos.y > lastCardBottom)
                    {
                        insertIdx = visibleCardCount;
                    }

                    if (
                        columnId != _currentPlaceholderColumnId
                        || insertIdx != _currentPlaceholderCardIndex
                    )
                    {
                        _placeholder.RemoveFromHierarchy();
                        int actualIdx = Mathf.Clamp(insertIdx, 0, cardsContainer.childCount);
                        cardsContainer.Insert(actualIdx, _placeholder);
                        _currentPlaceholderColumnId = columnId;
                        _currentPlaceholderCardIndex = insertIdx;
                    }
                    break;
                }
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

                actualIdx = Mathf.Clamp(actualIdx, 0, _columnsContainer.childCount - 1);
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
            _columnsContainer = null; // Clear cache so it's refreshed next drag
            _currentPlaceholderColumnIndex = -1;
            _currentPlaceholderCardIndex = -1;
            _currentPlaceholderColumnId = null;
        }
    }
}
