using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    /// <summary>
    /// Main TaskCanvas window for managing Kanban boards.
    /// Open via: Window > TaskCanvas
    /// </summary>
    public class TaskCanvasWindow : EditorWindow
    {
        private KanbanBoard _currentBoard;
        private VisualElement _root;
        private VisualElement _toolbar;
        private FilterBar _filterBar;
        private ScrollView _mainScrollView;
        private VisualElement _columnsContainer;
        private VisualElement _emptyState;
        private Button _themeToggle;
        private DropdownField _boardDropdown;
        private List<string> _boardPaths = new List<string>();

        private List<ColumnElement> _columnElements = new List<ColumnElement>();

        [MenuItem("Window/TaskCanvas")]
        public static void ShowWindow()
        {
            var window = GetWindow<TaskCanvasWindow>();
            window.titleContent = new GUIContent("📋 TaskCanvas");
            window.minSize = new Vector2(600, 400);
        }

        private void OnEnable()
        {
            BuildUI();
            RefreshBoardList();
            LoadLastBoard();
        }

        private void OnFocus()
        {
            RefreshBoardList();
        }

        private void BuildUI()
        {
            _root = rootVisualElement;
            _root.Clear();
            _root.AddToClassList("task-canvas-root");

            // Apply theme
            ThemeManager.ApplyTheme(_root);

            // Initialize drag-drop manager
            DragDropManager.Initialize(_root, OnCardDropped, OnColumnReorder, RefreshColumns);

            // Build toolbar
            BuildToolbar();

            // Build filter bar
            _filterBar = new FilterBar(_currentBoard);
            _filterBar.OnFiltersChanged += RefreshColumns;
            _root.Add(_filterBar);

            // Main scroll view for columns
            _mainScrollView = new ScrollView(ScrollViewMode.Horizontal);
            _mainScrollView.AddToClassList("main-scroll-view");
            _mainScrollView.style.flexGrow = 1;
            _root.Add(_mainScrollView);

            // Columns container inside scroll
            _columnsContainer = new VisualElement();
            _columnsContainer.AddToClassList("columns-container");
            _mainScrollView.Add(_columnsContainer);

            // Empty state
            _emptyState = new VisualElement();
            _emptyState.AddToClassList("empty-state");
            var emptyText = new Label(
                "No board selected.\nSelect or create a Kanban Board to get started."
            );
            emptyText.AddToClassList("empty-state-text");
            _emptyState.Add(emptyText);

            var createButton = new Button(CreateNewBoard);
            createButton.text = "+ Create New Board";
            createButton.AddToClassList("toolbar-button");
            _emptyState.Add(createButton);

            _root.Add(_emptyState);

            RefreshView();
        }

        private void BuildToolbar()
        {
            _toolbar = new VisualElement();
            _toolbar.AddToClassList("toolbar");
            _root.Add(_toolbar);

            // Title
            var title = new Label("📋 TaskCanvas");
            title.AddToClassList("toolbar-title");
            _toolbar.Add(title);

            // Board dropdown
            _boardDropdown = new DropdownField();
            _boardDropdown.AddToClassList("board-dropdown");
            _boardDropdown.RegisterValueChangedCallback(evt =>
                OnBoardDropdownChanged(evt.newValue)
            );
            _toolbar.Add(_boardDropdown);

            // New Board button
            var newBoardButton = new Button(CreateNewBoard);
            newBoardButton.text = "+ Board";
            newBoardButton.AddToClassList("toolbar-button");
            _toolbar.Add(newBoardButton);

            // New Assignee button
            var addAssigneeButton = new Button(ShowAddAssigneePopup);
            addAssigneeButton.text = "+ Assignee";
            addAssigneeButton.AddToClassList("toolbar-button");
            _toolbar.Add(addAssigneeButton);

            // New Tag button
            var addTagButton = new Button(ShowAddTagPopup);
            addTagButton.text = "+ Tag";
            addTagButton.AddToClassList("toolbar-button");
            _toolbar.Add(addTagButton);

            // Spacer
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            _toolbar.Add(spacer);

            // Theme toggle
            _themeToggle = new Button(ToggleTheme);
            _themeToggle.text = ThemeManager.GetThemeIcon();
            _themeToggle.AddToClassList("theme-toggle");
            _toolbar.Add(_themeToggle);
        }

        private void RefreshBoardList()
        {
            _boardPaths.Clear();
            var guids = AssetDatabase.FindAssets("t:KanbanBoard");
            var names = new List<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                _boardPaths.Add(path);
                var board = AssetDatabase.LoadAssetAtPath<KanbanBoard>(path);
                names.Add(
                    board != null
                        ? board.boardName
                        : System.IO.Path.GetFileNameWithoutExtension(path)
                );
            }

            _boardDropdown.choices = names.Count > 0 ? names : new List<string> { "(No boards)" };

            if (_currentBoard != null)
            {
                var currentPath = AssetDatabase.GetAssetPath(_currentBoard);
                var idx = _boardPaths.IndexOf(currentPath);
                if (idx >= 0 && idx < names.Count)
                {
                    _boardDropdown.SetValueWithoutNotify(names[idx]);
                }
            }
        }

        private void OnBoardDropdownChanged(string boardName)
        {
            if (boardName == "(No boards)" || string.IsNullOrEmpty(boardName))
                return;

            var idx = _boardDropdown.choices.IndexOf(boardName);
            if (idx >= 0 && idx < _boardPaths.Count)
            {
                var board = AssetDatabase.LoadAssetAtPath<KanbanBoard>(_boardPaths[idx]);
                SetBoard(board);
            }
        }

        private void ToggleTheme()
        {
            ThemeManager.ToggleTheme();
            _themeToggle.text = ThemeManager.GetThemeIcon();
            ThemeManager.ApplyTheme(_root);
        }

        private void SetBoard(KanbanBoard board)
        {
            _currentBoard = board;

            if (board != null)
            {
                EditorPrefs.SetString("TaskCanvas_LastBoard", AssetDatabase.GetAssetPath(board));
                var idx = _boardPaths.IndexOf(AssetDatabase.GetAssetPath(board));
                if (idx >= 0 && idx < _boardDropdown.choices.Count)
                {
                    _boardDropdown.SetValueWithoutNotify(_boardDropdown.choices[idx]);
                }
            }

            _filterBar.SetBoard(board);
            RefreshView();
        }

        private void LoadLastBoard()
        {
            var lastBoardPath = EditorPrefs.GetString("TaskCanvas_LastBoard", "");
            if (!string.IsNullOrEmpty(lastBoardPath))
            {
                var board = AssetDatabase.LoadAssetAtPath<KanbanBoard>(lastBoardPath);
                if (board != null)
                {
                    SetBoard(board);
                }
            }
        }

        private void RefreshView()
        {
            bool hasBoard = _currentBoard != null;

            _mainScrollView.style.display = hasBoard ? DisplayStyle.Flex : DisplayStyle.None;
            _filterBar.style.display = hasBoard ? DisplayStyle.Flex : DisplayStyle.None;
            _emptyState.style.display = hasBoard ? DisplayStyle.None : DisplayStyle.Flex;

            if (hasBoard)
            {
                RefreshColumns();
            }
        }

        private void RefreshColumns()
        {
            _columnsContainer.Clear();
            _columnElements.Clear();

            if (_currentBoard == null)
                return;

            for (int i = 0; i < _currentBoard.columns.Count; i++)
            {
                var column = _currentBoard.columns[i];

                var columnElement = new ColumnElement(
                    column,
                    _currentBoard,
                    i,
                    () => _filterBar.ActiveTagFilters,
                    () => _filterBar.ActiveAssigneeFilters,
                    OnColumnEdit,
                    OnCardEdit
                );

                columnElement.OnCardAdded += OnCardAdded;
                columnElement.OnDataChanged += OnDataChanged;

                _columnsContainer.Add(columnElement);
                _columnElements.Add(columnElement);
            }

            // Add "Add Column" button
            var addColumnBtn = new Button(ShowAddColumnPopup);
            addColumnBtn.text = "+ Add Column";
            addColumnBtn.AddToClassList("add-column-button");
            _columnsContainer.Add(addColumnBtn);
        }

        private void OnCardAdded(ColumnElement column, KanbanCard card)
        {
            ShowCreateCardPopup(column.Column);
        }

        private void OnCardEdit(KanbanCard card)
        {
            ShowEditCardPopup(card);
        }

        private void OnColumnEdit(ColumnElement columnElement)
        {
            ShowEditColumnPopup(columnElement.Column);
        }

        private void OnDataChanged()
        {
            EditorUtility.SetDirty(_currentBoard);
            AssetDatabase.SaveAssets();
        }

        private void OnColumnReorder(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex || fromIndex < 0 || toIndex < 0)
                return;
            if (fromIndex >= _currentBoard.columns.Count || toIndex >= _currentBoard.columns.Count)
                return;

            var column = _currentBoard.columns[fromIndex];
            _currentBoard.columns.RemoveAt(fromIndex);
            _currentBoard.columns.Insert(toIndex, column);

            EditorUtility.SetDirty(_currentBoard);
            AssetDatabase.SaveAssets();
            RefreshColumns();
        }

        private void OnCardDropped(string cardId, string targetColumnId, int targetIndex)
        {
            if (_currentBoard == null)
                return;

            if (_currentBoard.MoveCard(cardId, targetColumnId, targetIndex))
            {
                EditorUtility.SetDirty(_currentBoard);
                AssetDatabase.SaveAssets();
                RefreshColumns();
            }
        }

        private void CreateNewBoard()
        {
            var popup = new BoardCreatePopup(
                (boardName) =>
                {
                    if (string.IsNullOrWhiteSpace(boardName))
                        return;

                    var path = EditorUtility.SaveFilePanelInProject(
                        "Save Kanban Board",
                        boardName,
                        "asset",
                        "Choose where to save the board"
                    );

                    if (!string.IsNullOrEmpty(path))
                    {
                        var board = ScriptableObject.CreateInstance<KanbanBoard>();
                        board.boardName = boardName;
                        AssetDatabase.CreateAsset(board, path);
                        AssetDatabase.SaveAssets();
                        RefreshBoardList();
                        SetBoard(board);
                    }
                }
            );

            _root.Add(popup);
        }

        private void ShowCreateCardPopup(KanbanColumn targetColumn)
        {
            var popup = new CardEditPopup(
                null,
                _currentBoard,
                targetColumn,
                (card) =>
                {
                    if (card != null)
                    {
                        targetColumn.cards.Add(card);
                        EditorUtility.SetDirty(_currentBoard);
                        AssetDatabase.SaveAssets();
                        RefreshColumns();
                        _filterBar.RefreshFilters();
                    }
                }
            );

            _root.Add(popup);
        }

        private void ShowEditCardPopup(KanbanCard card)
        {
            var popup = new CardEditPopup(
                card,
                _currentBoard,
                null,
                (updatedCard) =>
                {
                    EditorUtility.SetDirty(_currentBoard);
                    AssetDatabase.SaveAssets();
                    RefreshColumns();
                    _filterBar.RefreshFilters();
                }
            );

            _root.Add(popup);
        }

        private void ShowAddColumnPopup()
        {
            var popup = new ColumnEditPopup(
                null,
                _currentBoard,
                () =>
                {
                    EditorUtility.SetDirty(_currentBoard);
                    AssetDatabase.SaveAssets();
                    RefreshColumns();
                }
            );

            _root.Add(popup);
        }

        private void ShowEditColumnPopup(KanbanColumn column)
        {
            var popup = new ColumnEditPopup(
                column,
                _currentBoard,
                () =>
                {
                    EditorUtility.SetDirty(_currentBoard);
                    AssetDatabase.SaveAssets();
                    RefreshColumns();
                }
            );

            _root.Add(popup);
        }

        private void ShowAddAssigneePopup()
        {
            if (_currentBoard == null)
            {
                EditorUtility.DisplayDialog("No Board", "Please select a board first.", "OK");
                return;
            }

            var popup = new AssigneeEditPopup(
                _currentBoard,
                () =>
                {
                    EditorUtility.SetDirty(_currentBoard);
                    AssetDatabase.SaveAssets();
                    _filterBar.RefreshFilters();
                }
            );

            _root.Add(popup);
        }

        private void ShowAddTagPopup()
        {
            if (_currentBoard == null)
            {
                EditorUtility.DisplayDialog("No Board", "Please select a board first.", "OK");
                return;
            }

            var popup = new TagEditPopup(
                _currentBoard,
                () =>
                {
                    EditorUtility.SetDirty(_currentBoard);
                    AssetDatabase.SaveAssets();
                    _filterBar.RefreshFilters();
                }
            );

            _root.Add(popup);
        }
    }

    /// <summary>
    /// Popup for creating a new board with name input.
    /// </summary>
    public class BoardCreatePopup : VisualElement
    {
        private TextField _nameField;
        private Action<string> _onCreate;

        public BoardCreatePopup(Action<string> onCreate)
        {
            _onCreate = onCreate;
            AddToClassList("modal-overlay");
            BuildUI();
        }

        private void BuildUI()
        {
            var content = new VisualElement();
            content.AddToClassList("modal-content");
            content.style.minWidth = 300;
            Add(content);

            var title = new Label("Create New Board");
            title.AddToClassList("modal-title");
            content.Add(title);

            var nameGroup = new VisualElement();
            nameGroup.AddToClassList("modal-field");
            content.Add(nameGroup);

            var nameLabel = new Label("Board Name");
            nameLabel.AddToClassList("modal-field-label");
            nameGroup.Add(nameLabel);

            _nameField = new TextField();
            _nameField.value = "My Board";
            _nameField.AddToClassList("modal-text-field");
            _nameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    Create();
            });
            nameGroup.Add(_nameField);

            var buttons = new VisualElement();
            buttons.AddToClassList("modal-buttons");
            buttons.style.marginTop = 16;
            content.Add(buttons);

            var cancelBtn = new Button(Close);
            cancelBtn.text = "Cancel";
            cancelBtn.AddToClassList("modal-button");
            cancelBtn.AddToClassList("modal-button-secondary");
            buttons.Add(cancelBtn);

            var createBtn = new Button(Create);
            createBtn.text = "Create";
            createBtn.AddToClassList("modal-button");
            createBtn.AddToClassList("modal-button-primary");
            buttons.Add(createBtn);

            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == this)
                    Close();
            });

            RegisterCallback<AttachToPanelEvent>(_ => _nameField.Focus());
        }

        private void Create()
        {
            _onCreate?.Invoke(_nameField.value);
            Close();
        }

        private void Close()
        {
            RemoveFromHierarchy();
        }
    }
}
