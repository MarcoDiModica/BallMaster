using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    /// <summary>
    /// Popup for managing tags on a board.
    /// </summary>
    public class TagEditPopup : VisualElement
    {
        private KanbanBoard _board;
        private Action _onSave;
        private TextField _nameField;
        private ColorField _colorField;
        private ScrollView _tagsList;

        public TagEditPopup(KanbanBoard board, Action onSave)
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
            var title = new Label("Manage Tags");
            title.AddToClassList("modal-title");
            content.Add(title);

            // Existing tags list with scroll
            _tagsList = new ScrollView(ScrollViewMode.Vertical);
            _tagsList.style.maxHeight = 200;
            _tagsList.style.marginBottom = 16;
            content.Add(_tagsList);
            RefreshTagsList();

            // New tag section
            var newSection = new Label("Add New Tag");
            newSection.AddToClassList("modal-field-label");
            newSection.style.marginTop = 12;
            content.Add(newSection);

            // Name field
            var nameGroup = new VisualElement();
            nameGroup.AddToClassList("modal-field");
            content.Add(nameGroup);

            var nameLabel = new Label("Tag Name");
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
            _colorField.value = new Color(0.6f, 0.4f, 0.8f);
            colorGroup.Add(_colorField);

            // Add button
            var addBtn = new Button(AddTag);
            addBtn.text = "+ Add Tag";
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

        private void RefreshTagsList()
        {
            _tagsList.Clear();

            if (_board.allTags == null)
                return;

            foreach (var tag in _board.allTags)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4;
                row.style.paddingLeft = 4;
                row.style.paddingRight = 4;

                var tagLabel = new Label($"#{tag}");
                tagLabel.style.flexGrow = 1;
                tagLabel.AddToClassList("card-tag");
                row.Add(tagLabel);

                var deleteBtn = new Button(() => DeleteTag(tag));
                deleteBtn.text = "✕";
                deleteBtn.AddToClassList("modal-button");
                deleteBtn.AddToClassList("modal-button-danger");
                deleteBtn.style.paddingTop = 2;
                deleteBtn.style.paddingRight = 6;
                deleteBtn.style.paddingBottom = 2;
                deleteBtn.style.paddingLeft = 6;
                row.Add(deleteBtn);

                _tagsList.Add(row);
            }
        }

        private void AddTag()
        {
            var tagName = _nameField.value?.Trim().TrimStart('#');
            if (string.IsNullOrWhiteSpace(tagName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a tag name.", "OK");
                return;
            }

            if (_board.allTags.Contains(tagName))
            {
                EditorUtility.DisplayDialog("Error", "This tag already exists.", "OK");
                return;
            }

            _board.AddTag(tagName);
            _nameField.value = "";

            RefreshTagsList();
            _onSave?.Invoke();
        }

        private void DeleteTag(string tag)
        {
            if (
                EditorUtility.DisplayDialog(
                    "Delete Tag",
                    $"Remove tag '#{tag}'? Cards using this tag will keep it.",
                    "Delete",
                    "Cancel"
                )
            )
            {
                _board.allTags.Remove(tag);
                RefreshTagsList();
                _onSave?.Invoke();
            }
        }

        private void Close()
        {
            RemoveFromHierarchy();
        }
    }
}
