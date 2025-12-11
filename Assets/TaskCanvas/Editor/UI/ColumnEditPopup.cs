using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class ColumnEditPopup : VisualElement
    {
        private KanbanColumn _column;
        private KanbanBoard _board;
        private Action _onSave;
        private bool _isCreateMode;

        private TextField _titleField;
        private ColorField _colorField;

        public ColumnEditPopup(KanbanColumn column, KanbanBoard board, Action onSave)
        {
            _column = column;
            _board = board;
            _onSave = onSave;
            _isCreateMode = column == null;

            if (_isCreateMode)
            {
                _column = new KanbanColumn("New Column");
            }

            AddToClassList("modal-overlay");
            BuildUI();
        }

        private void BuildUI()
        {
            var content = new VisualElement();
            content.AddToClassList("modal-content");
            Add(content);

            var title = new Label(_isCreateMode ? "Create Column" : "Edit Column");
            title.AddToClassList("modal-title");
            content.Add(title);

            var titleGroup = new VisualElement();
            titleGroup.AddToClassList("modal-field");
            content.Add(titleGroup);

            var titleLabel = new Label("Title");
            titleLabel.AddToClassList("modal-field-label");
            titleGroup.Add(titleLabel);

            _titleField = new TextField();
            _titleField.value = _column.title;
            _titleField.AddToClassList("modal-text-field");
            titleGroup.Add(_titleField);

            var colorGroup = new VisualElement();
            colorGroup.AddToClassList("modal-field");
            content.Add(colorGroup);

            var colorLabel = new Label("Header Color");
            colorLabel.AddToClassList("modal-field-label");
            colorGroup.Add(colorLabel);

            _colorField = new ColorField();
            _colorField.value = _column.headerColor;
            colorGroup.Add(_colorField);

            var buttons = new VisualElement();
            buttons.AddToClassList("modal-buttons");
            content.Add(buttons);

            if (!_isCreateMode)
            {
                var deleteBtn = new Button(DeleteColumn);
                deleteBtn.text = "Delete";
                deleteBtn.AddToClassList("modal-button");
                deleteBtn.AddToClassList("modal-button-danger");
                buttons.Add(deleteBtn);
            }

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            buttons.Add(spacer);

            var cancelBtn = new Button(Close);
            cancelBtn.text = "Cancel";
            cancelBtn.AddToClassList("modal-button");
            cancelBtn.AddToClassList("modal-button-secondary");
            buttons.Add(cancelBtn);

            var saveBtn = new Button(Save);
            saveBtn.text = _isCreateMode ? "Create" : "Save";
            saveBtn.AddToClassList("modal-button");
            saveBtn.AddToClassList("modal-button-primary");
            buttons.Add(saveBtn);

            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == this)
                    Close();
            });
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_titleField.value))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a column title.", "OK");
                return;
            }

            _column.title = _titleField.value;
            _column.headerColor = _colorField.value;

            if (_isCreateMode)
            {
                _board.columns.Add(_column);
            }

            _onSave?.Invoke();
            Close();
        }

        private void DeleteColumn()
        {
            var cardCount = _column.cards.Count;
            var message =
                cardCount > 0
                    ? $"Delete column '{_column.title}' and its {cardCount} card(s)?"
                    : $"Delete column '{_column.title}'?";

            if (EditorUtility.DisplayDialog("Delete Column", message, "Delete", "Cancel"))
            {
                _board.columns.Remove(_column);
                _onSave?.Invoke();
                Close();
            }
        }

        private void Close()
        {
            RemoveFromHierarchy();
        }
    }
}
