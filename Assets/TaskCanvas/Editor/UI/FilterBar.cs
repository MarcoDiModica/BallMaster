using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    /// <summary>
    /// Filter bar component for filtering cards by tags and assignees.
    /// Uses inline wrapping instead of scrollbars.
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
        private bool _isExpanded = false;
        private Button _expandButton;

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
            style.flexWrap = Wrap.Wrap;
            style.alignItems = Align.Center;

            // Tags section
            var tagsLabel = new Label("Tags:");
            tagsLabel.AddToClassList("filter-label");
            Add(tagsLabel);

            _tagsContainer = new VisualElement();
            _tagsContainer.style.flexDirection = FlexDirection.Row;
            _tagsContainer.style.flexWrap = Wrap.Wrap;
            _tagsContainer.style.flexShrink = 1;
            Add(_tagsContainer);

            // Separator
            var separator = new VisualElement();
            separator.style.width = 16;
            Add(separator);

            // Assignees section
            var assigneesLabel = new Label("Assignees:");
            assigneesLabel.AddToClassList("filter-label");
            Add(assigneesLabel);

            _assigneesContainer = new VisualElement();
            _assigneesContainer.style.flexDirection = FlexDirection.Row;
            _assigneesContainer.style.flexWrap = Wrap.Wrap;
            _assigneesContainer.style.flexShrink = 1;
            Add(_assigneesContainer);

            // Clear button
            _clearButton = new Button(ClearFilters);
            _clearButton.text = "✕ Clear";
            _clearButton.AddToClassList("filter-chip");
            _clearButton.AddToClassList("filter-chip-clear");
            _clearButton.style.display = DisplayStyle.None;
            _clearButton.style.marginLeft = 8;
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

            // Show max 8 tags inline, rest in a "+N more" chip
            int maxVisible = 8;
            int count = 0;

            foreach (var tag in _board.allTags)
            {
                if (count >= maxVisible && _board.allTags.Count > maxVisible + 1)
                {
                    var moreChip = new Button(ToggleExpand);
                    moreChip.text = $"+{_board.allTags.Count - maxVisible} more";
                    moreChip.AddToClassList("filter-chip");
                    moreChip.AddToClassList("filter-chip-more");
                    _tagsContainer.Add(moreChip);
                    break;
                }

                var chip = new Button(() => ToggleTagFilter(tag));
                chip.text = $"#{tag}";
                chip.AddToClassList("filter-chip");

                if (_activeTagFilters.Contains(tag))
                    chip.AddToClassList("active");

                _tagsContainer.Add(chip);
                count++;
            }
        }

        private void RefreshAssignees()
        {
            _assigneesContainer.Clear();

            if (_board == null || _board.assignees == null)
                return;

            // Show max 6 assignees inline
            int maxVisible = 6;
            int count = 0;

            foreach (var assignee in _board.assignees)
            {
                if (count >= maxVisible && _board.assignees.Count > maxVisible + 1)
                {
                    var moreChip = new Button(ToggleExpand);
                    moreChip.text = $"+{_board.assignees.Count - maxVisible} more";
                    moreChip.AddToClassList("filter-chip");
                    moreChip.AddToClassList("filter-chip-more");
                    _assigneesContainer.Add(moreChip);
                    break;
                }

                var chip = new Button(() => ToggleAssigneeFilter(assignee.id));
                chip.text = $"👤 {assignee.name}";
                chip.AddToClassList("filter-chip");
                chip.AddToClassList("assignee-chip");
                chip.style.borderLeftColor = assignee.color;

                if (_activeAssigneeFilters.Contains(assignee.id))
                    chip.AddToClassList("active");

                _assigneesContainer.Add(chip);
                count++;
            }
        }

        private void ToggleExpand()
        {
            _isExpanded = !_isExpanded;
            // For now just refresh - in expanded mode show all
            RefreshFilters();
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
