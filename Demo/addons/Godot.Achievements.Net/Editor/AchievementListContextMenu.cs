#if TOOLS
using System;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Manages the right-click context menu for the achievement list.
/// Emits signals for operations that require external handling.
/// </summary>
public partial class AchievementListContextMenu : RefCounted
{
    private readonly Func<AchievementDatabase?> _getDatabaseFunc;
    private readonly Func<string> _getDatabasePathFunc;
    private readonly Func<string> _getSearchTextFunc;
    private readonly ItemList _itemList;
    private PopupMenu? _contextMenu;
    private int _contextMenuTargetIndex = -1;

    [Signal]
    public delegate void MoveUpRequestedEventHandler(Achievement achievement);

    [Signal]
    public delegate void MoveDownRequestedEventHandler(Achievement achievement);

    public AchievementListContextMenu(
        ItemList itemList,
        Func<AchievementDatabase?> getDatabaseFunc,
        Func<string> getDatabasePathFunc,
        Func<string> getSearchTextFunc)
    {
        _itemList = itemList;
        _getDatabaseFunc = getDatabaseFunc;
        _getDatabasePathFunc = getDatabasePathFunc;
        _getSearchTextFunc = getSearchTextFunc;
    }

    public PopupMenu CreateContextMenu(Node parent)
    {
        _contextMenu = new PopupMenu();
        _contextMenu.AddItem("Move Up", 0);
        _contextMenu.AddItem("Move Down", 1);
        _contextMenu.AddSeparator();
        _contextMenu.AddItem("Show in File", 2);
        _contextMenu.IdPressed += OnContextMenuItemPressed;
        parent.AddChild(_contextMenu);
        return _contextMenu;
    }

    public void Cleanup()
    {
        if (_contextMenu != null)
        {
            _contextMenu.IdPressed -= OnContextMenuItemPressed;
            _contextMenu.QueueFree();
            _contextMenu = null;
        }
    }

    public void HandleItemListClicked(long index, Vector2 atPosition, long mouseButtonIndex)
    {
        // Right-click to show context menu
        if (mouseButtonIndex != (long)MouseButton.Right)
            return;

        _contextMenuTargetIndex = (int)index;
        var database = _getDatabaseFunc();

        if (_contextMenu == null || database == null || _contextMenuTargetIndex < 0)
            return;

        var achievement = _itemList.GetItemMetadata(_contextMenuTargetIndex).As<Achievement>();
        var databaseIndex = database.Achievements.IndexOf(achievement);

        // Check if filtering is active
        var isFiltered = !string.IsNullOrEmpty(_getSearchTextFunc());

        // Note: Item indices are 0=Move Up, 1=Move Down
        if (isFiltered)
        {
            // Disable move operations when filtered
            _contextMenu.SetItemDisabled(0, true); // Move Up
            _contextMenu.SetItemDisabled(1, true); // Move Down
        }
        else
        {
            // Enable/disable based on actual position in database
            var totalCount = database.Achievements.Count;
            _contextMenu.SetItemDisabled(0, databaseIndex <= 0); // Move Up
            _contextMenu.SetItemDisabled(1, databaseIndex >= totalCount - 1); // Move Down
        }

        _contextMenu.Position = (Vector2I)_itemList.GetGlobalMousePosition();
        _contextMenu.Popup();
    }

    private void OnContextMenuItemPressed(long id)
    {
        if (_contextMenuTargetIndex < 0 || _contextMenuTargetIndex >= _itemList.ItemCount)
            return;

        var achievement = _itemList.GetItemMetadata(_contextMenuTargetIndex).As<Achievement>();
        if (achievement == null)
            return;

        switch (id)
        {
            case 0: // Move Up
                EmitSignal(SignalName.MoveUpRequested, achievement);
                break;
            case 1: // Move Down
                EmitSignal(SignalName.MoveDownRequested, achievement);
                break;
            case 2: // Show in File
                ShowInFile(achievement);
                break;
        }
    }

    private void ShowInFile(Achievement achievement)
    {
        var editorInterface = EditorInterface.Singleton;
        if (editorInterface == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Editor, "EditorInterface not available");
            return;
        }

        var databasePath = ResolveUidToPath(_getDatabasePathFunc());

        // Check if the achievement has its own resource path (saved as external file)
        // Sub-resources have paths like "res://file.tres::Resource_id" - these are NOT external
        var achievementPath = achievement.ResourcePath;
        var isSubResource = !string.IsNullOrEmpty(achievementPath) && achievementPath.Contains("::");
        var isExternalResource = !string.IsNullOrEmpty(achievementPath)
            && !isSubResource
            && ResourceLoader.Exists(achievementPath)
            && achievementPath != databasePath;

        if (isExternalResource)
        {
            // Achievement is saved as its own file - navigate to it
            editorInterface.SelectFile(achievementPath);
            AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Showing achievement file: {achievementPath}");
        }
        else
        {
            // Achievement is internal to the database - show the database file
            if (!string.IsNullOrEmpty(databasePath) && ResourceLoader.Exists(databasePath))
            {
                editorInterface.SelectFile(databasePath);
                AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Achievement is internal - showing database file: {databasePath}");
            }
            else
            {
                AchievementLogger.Warning(AchievementLogger.Areas.Editor, "No database path available to show");
            }
        }
    }

    private static string ResolveUidToPath(string path)
    {
        if (path.StartsWith("uid://"))
        {
            var uid = ResourceUid.TextToId(path);
            if (ResourceUid.HasId(uid))
            {
                return ResourceUid.GetIdPath(uid);
            }
        }
        return path;
    }
}
#endif
