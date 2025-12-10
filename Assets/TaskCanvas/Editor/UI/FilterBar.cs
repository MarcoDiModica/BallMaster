using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    /// <summary>
    /// Filter bar component for filtering cards by tags and assignees.
    /// </summary>
    public class FilterBar : VisualElement
    {
        public event Action OnFiltersChanged;

        private KanbanBoard _board;
        private List<string> _activeTagFilters = new List<string>();
        private List<string> _activeAssigneeFilters = new List<string>();

        private VisualElement _tagsContainer;
        private VisualElement _assigneesContainer;
        private Button _clearButton;

        public List<string> ActiveTagFilters => new List<string>(_activeTagFilters);
        public List<string> ActiveAssigneeFilters => new List<string>(_activeAssigneeFilters);

        public FilterBar(KanbanBoard board)
        {
            _board = board;
            AddToClassList("filter-bar");
            BuildUI();
        }

        private void BuildUI()
        {
            // Tags section
            var tagsLabel = new Label("Tags:");
            tagsLabel.AddToClassList("filter-label");
            Add(tagsLabel);

            _tagsContainer = new VisualElement();
            _tagsContainer.style.flexDirection = FlexDirection.Row;
            _tagsContainer.style.flexWrap = Wrap.Wrap;
            Add(_tagsContainer);

            // Separator
            var separator = new VisualElement();
            separator.style.width = 20;
            Add(separator);

            // Assignees section
            var assigneesLabel = new Label("Assignees:");
            assigneesLabel.AddToClassList("filter-label");
            Add(assigneesLabel);

            _assigneesContainer = new VisualElement();
            _assigneesContainer.style.flexDirection = FlexDirection.Row;
            _assigneesContainer.style.flexWrap = Wrap.Wrap;
            Add(_assigneesContainer);

            // Clear button
            _clearButton = new Button(ClearFilters);
            _clearButton.text = "✕ Clear";
            _clearButton.AddToClassList("filter-chip");
            _clearButton.AddToClassList("filter-chip-clear");
            _clearButton.style.display = DisplayStyle.None;
            Add(_clearButton);

            RefreshFilters();
        }

        public void RefreshFilters()
        {
            RefreshTags();
            RefreshAssignees();
            UpdateClearButtonVisibility();
        }

        private void RefreshTags()
        {
            _tagsContainer.Clear();

            if (_board == null || _board.allTags == null)
                return;

            foreach (var tag in _board.allTags)
            {
                var chip = new Button(() => ToggleTagFilter(tag));
                chip.text = $"#{tag}";
                chip.AddToClassList("filter-chip");

                if (_activeTagFilters.Contains(tag))
                    chip.AddToClassList("active");

                _tagsContainer.Add(chip);
            }
        }

        private void RefreshAssignees()
        {
            _assigneesContainer.Clear();

            if (_board == null || _board.assignees == null)
                return;

            foreach (var assignee in _board.assignees)
            {
                var chip = new Button(() => ToggleAssigneeFilter(assignee.id));
                chip.text = $"👤 {assignee.name}";
                chip.AddToClassList("filter-chip");
                chip.AddToClassList("assignee-chip");
                chip.style.borderLeftColor = assignee.color;

                if (_activeAssigneeFilters.Contains(assignee.id))
                    chip.AddToClassList("active");

                _assigneesContainer.Add(chip);
            }
        }

        private void ToggleTagFilter(string tag)
        {
            if (_activeTagFilters.Contains(tag))
                _activeTagFilters.Remove(tag);
            else
                _activeTagFilters.Add(tag);

            RefreshTags();
            UpdateClearButtonVisibility();
            OnFiltersChanged?.Invoke();
        }

        private void ToggleAssigneeFilter(string assigneeId)
        {
            if (_activeAssigneeFilters.Contains(assigneeId))
                _activeAssigneeFilters.Remove(assigneeId);
            else
                _activeAssigneeFilters.Add(assigneeId);

            RefreshAssignees();
            UpdateClearButtonVisibility();
            OnFiltersChanged?.Invoke();
        }

        private void ClearFilters()
        {
            _activeTagFilters.Clear();
            _activeAssigneeFilters.Clear();
            RefreshFilters();
            OnFiltersChanged?.Invoke();
        }

        private void UpdateClearButtonVisibility()
        {
            bool hasFilters = _activeTagFilters.Count > 0 || _activeAssigneeFilters.Count > 0;
            _clearButton.style.display = hasFilters ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetBoard(KanbanBoard board)
        {
            _board = board;
            _activeTagFilters.Clear();
            _activeAssigneeFilters.Clear();
            RefreshFilters();
        }
    }
}
