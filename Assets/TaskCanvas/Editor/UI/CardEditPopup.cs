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
    /// Popup for creating and editing cards with modular extras panel.
    /// </summary>
    public class CardEditPopup : VisualElement
    {
        private KanbanCard _card;
        private KanbanBoard _board;
        private KanbanColumn _targetColumn;
        private Action<KanbanCard> _onComplete;
        private bool _isCreateMode;

        private TextField _titleField;
        private TextField _descriptionField;
        private VisualElement _prioritySelector;
        private ScrollView _tagsContainer;
        private ScrollView _assigneesContainer;
        private VisualElement _extrasPanel;

        // Module data
        private bool _hasDueDate;
        private long _dueDate;

        public CardEditPopup(
            KanbanCard card,
            KanbanBoard board,
            KanbanColumn targetColumn,
            Action<KanbanCard> onComplete
        )
        {
            _board = board;
            _targetColumn = targetColumn;
            _onComplete = onComplete;
            _isCreateMode = card == null;

            if (_isCreateMode)
            {
                _card = new KanbanCard("");
            }
            else
            {
                _card = card;
            }

            AddToClassList("modal-overlay");
            BuildUI();
        }

        private void BuildUI()
        {
            var content = new VisualElement();
            content.AddToClassList("modal-content");
            content.style.minWidth = 500;
            content.style.maxWidth = 600;
            Add(content);

            // Title
            var title = new Label(_isCreateMode ? "Create Card" : "Edit Card");
            title.AddToClassList("modal-title");
            content.Add(title);

            // Main layout: left side + right side (extras)
            var mainLayout = new VisualElement();
            mainLayout.style.flexDirection = FlexDirection.Row;
            content.Add(mainLayout);

            // Left side - main fields
            var leftPanel = new VisualElement();
            leftPanel.style.flexGrow = 1;
            leftPanel.style.marginRight = 16;
            mainLayout.Add(leftPanel);

            // Card Title field
            var titleGroup = new VisualElement();
            titleGroup.AddToClassList("modal-field");
            leftPanel.Add(titleGroup);

            var titleLabel = new Label("Title *");
            titleLabel.AddToClassList("modal-field-label");
            titleGroup.Add(titleLabel);

            _titleField = new TextField();
            _titleField.value = _card.title ?? "";
            _titleField.AddToClassList("modal-text-field");
            titleGroup.Add(_titleField);

            // Description field
            var descGroup = new VisualElement();
            descGroup.AddToClassList("modal-field");
            leftPanel.Add(descGroup);

            var descLabel = new Label("Description");
            descLabel.AddToClassList("modal-field-label");
            descGroup.Add(descLabel);

            _descriptionField = new TextField();
            _descriptionField.multiline = true;
            _descriptionField.value = _card.description ?? "";
            _descriptionField.AddToClassList("modal-text-field");
            _descriptionField.style.minHeight = 50;
            descGroup.Add(_descriptionField);

            // Priority selector
            var priorityGroup = new VisualElement();
            priorityGroup.AddToClassList("modal-field");
            leftPanel.Add(priorityGroup);

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

            // Tags section with scroll
            var tagsGroup = new VisualElement();
            tagsGroup.AddToClassList("modal-field");
            leftPanel.Add(tagsGroup);

            var tagsHeader = new VisualElement();
            tagsHeader.style.flexDirection = FlexDirection.Row;
            tagsHeader.style.justifyContent = Justify.SpaceBetween;
            tagsGroup.Add(tagsHeader);

            var tagsLabel = new Label("Tags");
            tagsLabel.AddToClassList("modal-field-label");
            tagsHeader.Add(tagsLabel);

            var addTagBtn = new Button(ShowQuickAddTag);
            addTagBtn.text = "+ New";
            addTagBtn.AddToClassList("modal-button-small");
            tagsHeader.Add(addTagBtn);

            _tagsContainer = new ScrollView(ScrollViewMode.Horizontal);
            _tagsContainer.style.maxHeight = 60;
            _tagsContainer.style.flexDirection = FlexDirection.Row;
            _tagsContainer.style.flexWrap = Wrap.Wrap;
            tagsGroup.Add(_tagsContainer);

            RefreshTags();

            // Assignees section with scroll
            var assigneesGroup = new VisualElement();
            assigneesGroup.AddToClassList("modal-field");
            leftPanel.Add(assigneesGroup);

            var assigneesHeader = new VisualElement();
            assigneesHeader.style.flexDirection = FlexDirection.Row;
            assigneesHeader.style.justifyContent = Justify.SpaceBetween;
            assigneesGroup.Add(assigneesHeader);

            var assigneesLabel = new Label("Assignees");
            assigneesLabel.AddToClassList("modal-field-label");
            assigneesHeader.Add(assigneesLabel);

            var addAssigneeBtn = new Button(ShowQuickAddAssignee);
            addAssigneeBtn.text = "+ New";
            addAssigneeBtn.AddToClassList("modal-button-small");
            assigneesHeader.Add(addAssigneeBtn);

            _assigneesContainer = new ScrollView(ScrollViewMode.Horizontal);
            _assigneesContainer.style.maxHeight = 60;
            _assigneesContainer.style.flexDirection = FlexDirection.Row;
            _assigneesContainer.style.flexWrap = Wrap.Wrap;
            assigneesGroup.Add(_assigneesContainer);

            RefreshAssignees();

            // Right side - extras panel
            _extrasPanel = new VisualElement();
            _extrasPanel.style.width = 140;
            _extrasPanel.style.borderLeftWidth = 1;
            _extrasPanel.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
            _extrasPanel.style.paddingLeft = 12;
            mainLayout.Add(_extrasPanel);

            var extrasTitle = new Label("Add to card");
            extrasTitle.AddToClassList("modal-field-label");
            extrasTitle.style.marginBottom = 8;
            _extrasPanel.Add(extrasTitle);

            // Due Date module
            var dueDateBtn = new Button(ToggleDueDate);
            dueDateBtn.text = "📅 Due Date";
            dueDateBtn.AddToClassList("extras-button");
            _extrasPanel.Add(dueDateBtn);

            // Future modules placeholder
            var linkBtn = new Button(() => { });
            linkBtn.text = "🔗 Link";
            linkBtn.AddToClassList("extras-button");
            linkBtn.SetEnabled(false);
            _extrasPanel.Add(linkBtn);

            var checklistBtn = new Button(() => { });
            checklistBtn.text = "☑️ Checklist";
            checklistBtn.AddToClassList("extras-button");
            checklistBtn.SetEnabled(false);
            _extrasPanel.Add(checklistBtn);

            // Buttons
            var buttons = new VisualElement();
            buttons.AddToClassList("modal-buttons");
            buttons.style.marginTop = 16;
            content.Add(buttons);

            // Delete button (only in edit mode)
            if (!_isCreateMode)
            {
                var deleteBtn = new Button(DeleteCard);
                deleteBtn.text = "Delete";
                deleteBtn.AddToClassList("modal-button");
                deleteBtn.AddToClassList("modal-button-danger");
                buttons.Add(deleteBtn);
            }

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            buttons.Add(spacer);

            var cancelBtn = new Button(Cancel);
            cancelBtn.text = "Cancel";
            cancelBtn.AddToClassList("modal-button");
            cancelBtn.AddToClassList("modal-button-secondary");
            buttons.Add(cancelBtn);

            var saveBtn = new Button(Save);
            saveBtn.text = _isCreateMode ? "Create" : "Save";
            saveBtn.AddToClassList("modal-button");
            saveBtn.AddToClassList("modal-button-primary");
            buttons.Add(saveBtn);

            // Close on background click
            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == this)
                    Cancel();
            });
        }

        private void RefreshTags()
        {
            _tagsContainer.Clear();

            foreach (var tag in _board.allTags)
            {
                var isSelected = _card.tags.Contains(tag);
                var chip = new Button(() => ToggleTag(tag));
                chip.text = $"#{tag}";
                chip.AddToClassList("filter-chip");

                if (isSelected)
                    chip.AddToClassList("active");

                _tagsContainer.Add(chip);
            }
        }

        private void RefreshAssignees()
        {
            _assigneesContainer.Clear();

            foreach (var assignee in _board.assignees)
            {
                var isSelected = _card.assigneeIds.Contains(assignee.id);
                var chip = new Button(() => ToggleAssignee(assignee.id));
                chip.text = $"👤 {assignee.name}";
                chip.AddToClassList("filter-chip");
                chip.AddToClassList("assignee-chip");
                chip.style.borderLeftColor = assignee.color;

                if (isSelected)
                    chip.AddToClassList("active");

                _assigneesContainer.Add(chip);
            }
        }

        private void ToggleTag(string tag)
        {
            if (_card.tags.Contains(tag))
                _card.tags.Remove(tag);
            else
                _card.tags.Add(tag);

            RefreshTags();
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

        private void ShowQuickAddTag()
        {
            var tagName =
                EditorUtility.DisplayDialogComplex(
                    "Quick Add Tag",
                    "Enter tag name:",
                    "Add",
                    "Cancel",
                    ""
                ) == 0
                    ? ""
                    : "";

            // Use a simple input dialog workaround
            var popup = new QuickInputPopup(
                "New Tag",
                "Enter tag name:",
                (name) =>
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        _board.AddTag(name.Trim().TrimStart('#'));
                        _card.tags.Add(name.Trim().TrimStart('#'));
                        RefreshTags();
                    }
                }
            );
            parent.Add(popup);
        }

        private void ShowQuickAddAssignee()
        {
            var popup = new QuickInputPopup(
                "New Assignee",
                "Enter name:",
                (name) =>
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        var assignee = new Assignee(name.Trim());
                        _board.AddAssignee(assignee);
                        _card.assigneeIds.Add(assignee.id);
                        RefreshAssignees();
                    }
                }
            );
            parent.Add(popup);
        }

        private void ToggleDueDate()
        {
            _hasDueDate = !_hasDueDate;
            // TODO: Implement date picker when needed
            EditorUtility.DisplayDialog(
                "Coming Soon",
                "Due date feature will be implemented in the next update.",
                "OK"
            );
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_titleField.value))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a title.", "OK");
                return;
            }

            _card.title = _titleField.value;
            _card.description = _descriptionField.value;
            _card.MarkUpdated();

            _onComplete?.Invoke(_isCreateMode ? _card : null);
            Close();
        }

        private void Cancel()
        {
            if (_isCreateMode)
            {
                _onComplete?.Invoke(null);
            }
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
                _onComplete?.Invoke(null);
                Close();
            }
        }

        private void Close()
        {
            RemoveFromHierarchy();
        }
    }

    /// <summary>
    /// Simple quick input popup for adding tags/assignees inline.
    /// </summary>
    public class QuickInputPopup : VisualElement
    {
        private TextField _inputField;
        private Action<string> _onSubmit;

        public QuickInputPopup(string title, string placeholder, Action<string> onSubmit)
        {
            _onSubmit = onSubmit;

            AddToClassList("quick-input-popup");
            style.position = Position.Absolute;
            style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            style.borderTopLeftRadius = 4;
            style.borderTopRightRadius = 4;
            style.borderBottomLeftRadius = 4;
            style.borderBottomRightRadius = 4;
            style.paddingTop = 8;
            style.paddingRight = 8;
            style.paddingBottom = 8;
            style.paddingLeft = 8;

            var label = new Label(title);
            label.style.marginBottom = 4;
            label.style.fontSize = 12;
            Add(label);

            _inputField = new TextField();
            _inputField.AddToClassList("modal-text-field");
            Add(_inputField);

            var buttonsRow = new VisualElement();
            buttonsRow.style.flexDirection = FlexDirection.Row;
            buttonsRow.style.marginTop = 8;
            Add(buttonsRow);

            var cancelBtn = new Button(Close);
            cancelBtn.text = "Cancel";
            cancelBtn.style.marginRight = 4;
            buttonsRow.Add(cancelBtn);

            var addBtn = new Button(Submit);
            addBtn.text = "Add";
            addBtn.AddToClassList("modal-button-primary");
            buttonsRow.Add(addBtn);

            _inputField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    Submit();
                else if (evt.keyCode == KeyCode.Escape)
                    Close();
            });

            RegisterCallback<AttachToPanelEvent>(_ => _inputField.Focus());
        }

        private void Submit()
        {
            _onSubmit?.Invoke(_inputField.value);
            Close();
        }

        private void Close()
        {
            RemoveFromHierarchy();
        }
    }
}
