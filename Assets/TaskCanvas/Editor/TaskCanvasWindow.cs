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
        private VisualElement _columnsContainer;
        private VisualElement _emptyState;
        private Button _themeToggle;
        private ObjectField _boardField;

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
            LoadLastBoard();
        }

        private void BuildUI()
        {
            _root = rootVisualElement;
            _root.Clear();
            _root.AddToClassList("task-canvas-root");

            // Apply theme
            ThemeManager.ApplyTheme(_root);

            // Build toolbar
            BuildToolbar();

            // Build filter bar
            _filterBar = new FilterBar(_currentBoard);
            _filterBar.OnFiltersChanged += RefreshColumns;
            _root.Add(_filterBar);

            // Build columns container
            _columnsContainer = new VisualElement();
            _columnsContainer.AddToClassList("columns-container");
            _root.Add(_columnsContainer);

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

            // Board selector
            _boardField = new ObjectField();
            _boardField.objectType = typeof(KanbanBoard);
            _boardField.label = "Board";
            _boardField.AddToClassList("board-dropdown");
            _boardField.RegisterValueChangedCallback(evt =>
            {
                SetBoard(evt.newValue as KanbanBoard);
            });
            _toolbar.Add(_boardField);

            // New Card button
            var addCardButton = new Button(AddNewCard);
            addCardButton.text = "+ Card";
            addCardButton.AddToClassList("toolbar-button");
            _toolbar.Add(addCardButton);

            // New Assignee button
            var addAssigneeButton = new Button(ShowAddAssigneePopup);
            addAssigneeButton.text = "+ Assignee";
            addAssigneeButton.AddToClassList("toolbar-button");
            _toolbar.Add(addAssigneeButton);

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

        private void ToggleTheme()
        {
            ThemeManager.ToggleTheme();
            _themeToggle.text = ThemeManager.GetThemeIcon();
            ThemeManager.ApplyTheme(_root);
        }

        private void SetBoard(KanbanBoard board)
        {
            _currentBoard = board;
            _boardField.SetValueWithoutNotify(board);

            if (board != null)
            {
                EditorPrefs.SetString("TaskCanvas_LastBoard", AssetDatabase.GetAssetPath(board));
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

            _columnsContainer.style.display = hasBoard ? DisplayStyle.Flex : DisplayStyle.None;
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

            foreach (var column in _currentBoard.columns)
            {
                var columnElement = new ColumnElement(
                    column,
                    _currentBoard,
                    () => _filterBar.ActiveTagFilters,
                    () => _filterBar.ActiveAssigneeFilters
                );

                columnElement.OnCardAdded += OnCardAdded;
                columnElement.OnCardClicked += OnCardClicked;
                columnElement.OnCardDropped += OnCardDropped;

                _columnsContainer.Add(columnElement);
                _columnElements.Add(columnElement);
            }
        }

        private void OnCardAdded(ColumnElement column, KanbanCard card)
        {
            EditorUtility.SetDirty(_currentBoard);
            AssetDatabase.SaveAssets();
            ShowEditCardPopup(card);
        }

        private void OnCardClicked(CardElement cardElement)
        {
            ShowEditCardPopup(cardElement.Card);
        }

        private void OnCardDropped(string cardId, string targetColumnId, int targetIndex)
        {
            if (_currentBoard.MoveCard(cardId, targetColumnId, targetIndex))
            {
                EditorUtility.SetDirty(_currentBoard);
                AssetDatabase.SaveAssets();
                RefreshColumns();
            }
        }

        private void AddNewCard()
        {
            if (_currentBoard == null || _currentBoard.columns.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Columns",
                    "Please create a board with columns first.",
                    "OK"
                );
                return;
            }

            var firstColumn = _currentBoard.columns[0];
            var newCard = new KanbanCard("New Task");
            firstColumn.cards.Add(newCard);

            EditorUtility.SetDirty(_currentBoard);
            AssetDatabase.SaveAssets();

            RefreshColumns();
            ShowEditCardPopup(newCard);
        }

        private void CreateNewBoard()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Kanban Board",
                "New Kanban Board",
                "asset",
                "Choose a location for the new Kanban board"
            );

            if (!string.IsNullOrEmpty(path))
            {
                var board = ScriptableObject.CreateInstance<KanbanBoard>();
                AssetDatabase.CreateAsset(board, path);
                AssetDatabase.SaveAssets();
                SetBoard(board);
            }
        }

        private void ShowEditCardPopup(KanbanCard card)
        {
            var popup = new CardEditPopup(
                card,
                _currentBoard,
                () =>
                {
                    EditorUtility.SetDirty(_currentBoard);
                    AssetDatabase.SaveAssets();
                    RefreshColumns();
                    _filterBar.RefreshFilters();
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
    }

    /// <summary>
    /// Popup for editing a card's details.
    /// </summary>
    public class CardEditPopup : VisualElement
    {
        private KanbanCard _card;
        private KanbanBoard _board;
        private Action _onSave;

        private TextField _titleField;
        private TextField _descriptionField;
        private TextField _tagsField;
        private VisualElement _prioritySelector;
        private VisualElement _assigneesContainer;

        public CardEditPopup(KanbanCard card, KanbanBoard board, Action onSave)
        {
            _card = card;
            _board = board;
            _onSave = onSave;

            AddToClassList("modal-overlay");
            BuildUI();
        }

        private void BuildUI()
        {
            var content = new VisualElement();
            content.AddToClassList("modal-content");
            Add(content);

            // Title
            var title = new Label("Edit Card");
            title.AddToClassList("modal-title");
            content.Add(title);

            // Card Title field
            var titleGroup = new VisualElement();
            titleGroup.AddToClassList("modal-field");
            content.Add(titleGroup);

            var titleLabel = new Label("Title");
            titleLabel.AddToClassList("modal-field-label");
            titleGroup.Add(titleLabel);

            _titleField = new TextField();
            _titleField.value = _card.title;
            _titleField.AddToClassList("modal-text-field");
            titleGroup.Add(_titleField);

            // Description field
            var descGroup = new VisualElement();
            descGroup.AddToClassList("modal-field");
            content.Add(descGroup);

            var descLabel = new Label("Description");
            descLabel.AddToClassList("modal-field-label");
            descGroup.Add(descLabel);

            _descriptionField = new TextField();
            _descriptionField.multiline = true;
            _descriptionField.value = _card.description ?? "";
            _descriptionField.AddToClassList("modal-text-field");
            _descriptionField.style.minHeight = 60;
            descGroup.Add(_descriptionField);

            // Priority selector
            var priorityGroup = new VisualElement();
            priorityGroup.AddToClassList("modal-field");
            content.Add(priorityGroup);

            var priorityLabel = new Label("Priority");
            priorityLabel.AddToClassList("modal-field-label");
            priorityGroup.Add(priorityLabel);

            _prioritySelector = new VisualElement();
            _prioritySelector.AddToClassList("priority-selector");
            priorityGroup.Add(_prioritySelector);

            string[] priorities = { "🟢 Low", "🟡 Medium", "🔴 High" };
            for (int i = 0; i < priorities.Length; i++)
            {
                int priority = i;
                var btn = new Button(() => SetPriority(priority));
                btn.text = priorities[i];
                btn.AddToClassList("priority-option");

                if (i == 0)
                    btn.AddToClassList("priority-low");
                else if (i == 1)
                    btn.AddToClassList("priority-medium");
                else
                    btn.AddToClassList("priority-high");

                if (_card.priority == i)
                    btn.AddToClassList("selected");

                _prioritySelector.Add(btn);
            }

            // Tags field
            var tagsGroup = new VisualElement();
            tagsGroup.AddToClassList("modal-field");
            content.Add(tagsGroup);

            var tagsLabel = new Label("Tags (comma-separated)");
            tagsLabel.AddToClassList("modal-field-label");
            tagsGroup.Add(tagsLabel);

            _tagsField = new TextField();
            _tagsField.value = string.Join(", ", _card.tags);
            _tagsField.AddToClassList("modal-text-field");
            tagsGroup.Add(_tagsField);

            // Assignees
            var assigneesGroup = new VisualElement();
            assigneesGroup.AddToClassList("modal-field");
            content.Add(assigneesGroup);

            var assigneesLabel = new Label("Assignees");
            assigneesLabel.AddToClassList("modal-field-label");
            assigneesGroup.Add(assigneesLabel);

            _assigneesContainer = new VisualElement();
            _assigneesContainer.style.flexDirection = FlexDirection.Row;
            _assigneesContainer.style.flexWrap = Wrap.Wrap;
            assigneesGroup.Add(_assigneesContainer);

            RefreshAssignees();

            // Buttons
            var buttons = new VisualElement();
            buttons.AddToClassList("modal-buttons");
            content.Add(buttons);

            var deleteBtn = new Button(DeleteCard);
            deleteBtn.text = "Delete";
            deleteBtn.AddToClassList("modal-button");
            deleteBtn.AddToClassList("modal-button-danger");
            buttons.Add(deleteBtn);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            buttons.Add(spacer);

            var cancelBtn = new Button(Close);
            cancelBtn.text = "Cancel";
            cancelBtn.AddToClassList("modal-button");
            cancelBtn.AddToClassList("modal-button-secondary");
            buttons.Add(cancelBtn);

            var saveBtn = new Button(Save);
            saveBtn.text = "Save";
            saveBtn.AddToClassList("modal-button");
            saveBtn.AddToClassList("modal-button-primary");
            buttons.Add(saveBtn);

            // Close on background click
            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == this)
                    Close();
            });
        }

        private void RefreshAssignees()
        {
            _assigneesContainer.Clear();

            foreach (var assignee in _board.assignees)
            {
                var isAssigned = _card.assigneeIds.Contains(assignee.id);
                var chip = new Button(() => ToggleAssignee(assignee.id));
                chip.text = $"👤 {assignee.name}";
                chip.AddToClassList("filter-chip");
                chip.style.borderLeftWidth = 3;
                chip.style.borderLeftColor = assignee.color;

                if (isAssigned)
                    chip.AddToClassList("active");

                _assigneesContainer.Add(chip);
            }
        }

        private void ToggleAssignee(string assigneeId)
        {
            if (_card.assigneeIds.Contains(assigneeId))
                _card.assigneeIds.Remove(assigneeId);
            else
                _card.assigneeIds.Add(assigneeId);

            RefreshAssignees();
        }

        private void SetPriority(int priority)
        {
            _card.priority = priority;

            foreach (var child in _prioritySelector.Children())
            {
                child.RemoveFromClassList("selected");
            }

            var buttons = _prioritySelector.Children().ToList();
            if (priority < buttons.Count)
                buttons[priority].AddToClassList("selected");
        }

        private void Save()
        {
            _card.title = _titleField.value;
            _card.description = _descriptionField.value;

            // Parse tags
            _card.tags.Clear();
            var tags = _tagsField.value.Split(
                new[] { ',', ' ' },
                StringSplitOptions.RemoveEmptyEntries
            );
            foreach (var tag in tags)
            {
                var cleanTag = tag.Trim().TrimStart('#');
                if (!string.IsNullOrEmpty(cleanTag))
                {
                    _card.tags.Add(cleanTag);
                    _board.AddTag(cleanTag);
                }
            }

            _card.MarkUpdated();
            _onSave?.Invoke();
            Close();
        }

        private void DeleteCard()
        {
            if (
                EditorUtility.DisplayDialog(
                    "Delete Card",
                    $"Are you sure you want to delete '{_card.title}'?",
                    "Delete",
                    "Cancel"
                )
            )
            {
                foreach (var column in _board.columns)
                {
                    column.cards.RemoveAll(c => c.id == _card.id);
                }
                _onSave?.Invoke();
                Close();
            }
        }

        private void Close()
        {
            RemoveFromHierarchy();
        }
    }

    /// <summary>
    /// Popup for adding/editing assignees.
    /// </summary>
    public class AssigneeEditPopup : VisualElement
    {
        private KanbanBoard _board;
        private Action _onSave;
        private TextField _nameField;
        private ColorField _colorField;
        private VisualElement _assigneesList;

        public AssigneeEditPopup(KanbanBoard board, Action onSave)
        {
            _board = board;
            _onSave = onSave;

            AddToClassList("modal-overlay");
            BuildUI();
        }

        private void BuildUI()
        {
            var content = new VisualElement();
            content.AddToClassList("modal-content");
            Add(content);

            // Title
            var title = new Label("Manage Assignees");
            title.AddToClassList("modal-title");
            content.Add(title);

            // Existing assignees list
            _assigneesList = new VisualElement();
            _assigneesList.style.marginBottom = 16;
            content.Add(_assigneesList);
            RefreshAssigneesList();

            // New assignee section
            var newSection = new Label("Add New Assignee");
            newSection.AddToClassList("modal-field-label");
            newSection.style.marginTop = 12;
            content.Add(newSection);

            // Name field
            var nameGroup = new VisualElement();
            nameGroup.AddToClassList("modal-field");
            content.Add(nameGroup);

            var nameLabel = new Label("Name");
            nameLabel.AddToClassList("modal-field-label");
            nameGroup.Add(nameLabel);

            _nameField = new TextField();
            _nameField.AddToClassList("modal-text-field");
            nameGroup.Add(_nameField);

            // Color field
            var colorGroup = new VisualElement();
            colorGroup.AddToClassList("modal-field");
            content.Add(colorGroup);

            var colorLabel = new Label("Color");
            colorLabel.AddToClassList("modal-field-label");
            colorGroup.Add(colorLabel);

            _colorField = new ColorField();
            _colorField.value = new Color(0.4f, 0.6f, 1f);
            colorGroup.Add(_colorField);

            // Add button
            var addBtn = new Button(AddAssignee);
            addBtn.text = "+ Add Assignee";
            addBtn.AddToClassList("modal-button");
            addBtn.AddToClassList("modal-button-primary");
            addBtn.style.marginTop = 8;
            content.Add(addBtn);

            // Close button
            var buttons = new VisualElement();
            buttons.AddToClassList("modal-buttons");
            buttons.style.marginTop = 16;
            content.Add(buttons);

            var closeBtn = new Button(Close);
            closeBtn.text = "Done";
            closeBtn.AddToClassList("modal-button");
            closeBtn.AddToClassList("modal-button-secondary");
            buttons.Add(closeBtn);

            // Close on background click
            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == this)
                    Close();
            });
        }

        private void RefreshAssigneesList()
        {
            _assigneesList.Clear();

            foreach (var assignee in _board.assignees)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4;

                var colorBox = new VisualElement();
                colorBox.style.width = 16;
                colorBox.style.height = 16;
                colorBox.style.borderTopLeftRadius = 8;
                colorBox.style.borderTopRightRadius = 8;
                colorBox.style.borderBottomLeftRadius = 8;
                colorBox.style.borderBottomRightRadius = 8;
                colorBox.style.backgroundColor = assignee.color;
                colorBox.style.marginRight = 8;
                row.Add(colorBox);

                var nameLabel = new Label(assignee.name);
                nameLabel.style.flexGrow = 1;
                row.Add(nameLabel);

                var deleteBtn = new Button(() => DeleteAssignee(assignee));
                deleteBtn.text = "✕";
                deleteBtn.AddToClassList("modal-button");
                deleteBtn.AddToClassList("modal-button-danger");
                deleteBtn.style.paddingTop = 4;
                deleteBtn.style.paddingRight = 4;
                deleteBtn.style.paddingBottom = 4;
                deleteBtn.style.paddingLeft = 4;
                row.Add(deleteBtn);

                _assigneesList.Add(row);
            }
        }

        private void AddAssignee()
        {
            if (string.IsNullOrWhiteSpace(_nameField.value))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a name.", "OK");
                return;
            }

            var assignee = new Assignee(_nameField.value.Trim());
            assignee.color = _colorField.value;
            _board.AddAssignee(assignee);

            _nameField.value = "";
            _colorField.value = new Color(0.4f, 0.6f, 1f);

            RefreshAssigneesList();
            _onSave?.Invoke();
        }

        private void DeleteAssignee(Assignee assignee)
        {
            if (
                EditorUtility.DisplayDialog(
                    "Delete Assignee",
                    $"Remove '{assignee.name}'? Cards will keep their assignments.",
                    "Delete",
                    "Cancel"
                )
            )
            {
                _board.assignees.Remove(assignee);
                RefreshAssigneesList();
                _onSave?.Invoke();
            }
        }

        private void Close()
        {
            RemoveFromHierarchy();
        }
    }
}
