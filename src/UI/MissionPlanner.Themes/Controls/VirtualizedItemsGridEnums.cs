namespace MissionPlanner.Themes.Controls;

/// <summary>How many items a <see cref="VirtualizedItemsGrid"/> lets the user select.</summary>
public enum VirtualizedItemsGridSelectionMode
{
    /// <summary>Selection is disabled.</summary>
    None,
    /// <summary>One item can be selected.</summary>
    Single,
    /// <summary>Several items can be selected.</summary>
    Multiple
}

/// <summary>Which items the select-all action of a <see cref="VirtualizedItemsGrid"/> selects.</summary>
public enum VirtualizedItemsGridSelectAllScope
{
    /// <summary>Only the items on the current page.</summary>
    CurrentPage,
    /// <summary>All items that match the search.</summary>
    FilteredItems
}
