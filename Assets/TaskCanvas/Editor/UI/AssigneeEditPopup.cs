using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    /// <summary>
    /// Popup for managing assignees on a board.
    /// </summary>
    public class AssigneeEditPopup : VisualElement
    {
        private KanbanBoard _board;
        private Action _onSave;
        private TextField _nameField;
        private ColorField _colorField;
        private ScrollView _assigneesList;

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

            // Existing assignees list with scroll
            _assigneesList = new ScrollView(ScrollViewMode.Vertical);
            _assigneesList.style.maxHeight = 200;
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
                row.style.paddingLeft = 4;
                row.style.paddingRight = 4;

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
                deleteBtn.style.paddingTop = 2;
                deleteBtn.style.paddingRight = 6;
                deleteBtn.style.paddingBottom = 2;
                deleteBtn.style.paddingLeft = 6;
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
