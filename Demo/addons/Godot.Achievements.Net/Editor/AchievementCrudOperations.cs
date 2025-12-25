#if TOOLS
using System;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Handles CRUD operations for achievements with Undo/Redo support.
/// Uses composition pattern with Func/Action delegates for dependencies.
/// </summary>
public partial class AchievementCrudOperations : RefCounted
{
    private readonly Func<AchievementDatabase?> _getDatabaseFunc;
    private readonly Action _saveDatabase;
    private readonly Action<bool> _refreshList;
    private readonly Action<string> _selectAchievementById;
    private EditorUndoRedoManager? _undoRedoManager;

    public AchievementCrudOperations(
        Func<AchievementDatabase?> getDatabaseFunc,
        Action saveDatabase,
        Action<bool> refreshList,
        Action<string> selectAchievementById)
    {
        _getDatabaseFunc = getDatabaseFunc;
        _saveDatabase = saveDatabase;
        _refreshList = refreshList;
        _selectAchievementById = selectAchievementById;
    }

    public void SetUndoRedoManager(EditorUndoRedoManager? manager)
    {
        _undoRedoManager = manager;
    }

    #region Add Achievement

    public void CreateNewAchievement()
    {
        var database = _getDatabaseFunc();
        if (database == null)
        {
            AchievementLogger.Warning(AchievementLogger.Areas.Editor, "Cannot add achievement - no database loaded");
            return;
        }

        var uniqueId = GenerateUniqueId(database);
        var newAchievement = new Achievement
        {
            Id = uniqueId,
            DisplayName = "New Achievement",
            Description = "Achievement description",
            Icon = null,
            SteamId = string.Empty,
            GameCenterId = string.Empty,
            GooglePlayId = string.Empty,
            IsIncremental = false,
            MaxProgress = 1,
            CustomPlatformIds = new Godot.Collections.Dictionary<string, string>(),
            ExtraProperties = new Godot.Collections.Dictionary<string, Variant>()
        };

        if (_undoRedoManager != null)
        {
            _undoRedoManager.CreateAction("Add Achievement");
            _undoRedoManager.AddDoMethod(this, nameof(DoAddAchievement), newAchievement);
            _undoRedoManager.AddUndoMethod(this, nameof(UndoAddAchievement), newAchievement);
            _undoRedoManager.CommitAction();
        }
        else
        {
            DoAddAchievement(newAchievement);
        }
    }

    public void DoAddAchievement(Achievement achievement)
    {
        var database = _getDatabaseFunc();
        if (database == null) return;

        database.AddAchievement(achievement);
        _saveDatabase();
        _refreshList(false);
        _selectAchievementById(achievement.Id);

        AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Created new achievement: {achievement.Id}");
    }

    public void UndoAddAchievement(Achievement achievement)
    {
        var database = _getDatabaseFunc();
        if (database == null) return;

        database.RemoveAchievement(achievement.Id);
        _saveDatabase();
        _refreshList(false);

        AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Undid add achievement: {achievement.Id}");
    }

    #endregion

    #region Remove Achievement

    public void RemoveAchievement(Achievement achievement)
    {
        var database = _getDatabaseFunc();
        if (database == null) return;

        var originalIndex = database.Achievements.IndexOf(achievement);

        if (_undoRedoManager != null)
        {
            _undoRedoManager.CreateAction("Remove Achievement");
            _undoRedoManager.AddDoMethod(this, nameof(DoRemoveAchievement), achievement);
            _undoRedoManager.AddUndoMethod(this, nameof(UndoRemoveAchievement), achievement, originalIndex);
            _undoRedoManager.CommitAction();
        }
        else
        {
            DoRemoveAchievement(achievement);
        }
    }

    public void DoRemoveAchievement(Achievement achievement)
    {
        var database = _getDatabaseFunc();
        if (database == null) return;

        database.RemoveAchievement(achievement.Id);
        _saveDatabase();
        _refreshList(false);

        AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Removed achievement: {achievement.Id}");
    }

    public void UndoRemoveAchievement(Achievement achievement, int originalIndex)
    {
        var database = _getDatabaseFunc();
        if (database == null) return;

        if (originalIndex >= 0 && originalIndex <= database.Achievements.Count)
        {
            database.Achievements.Insert(originalIndex, achievement);
        }
        else
        {
            database.Achievements.Add(achievement);
        }

        _saveDatabase();
        _refreshList(false);
        _selectAchievementById(achievement.Id);

        AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Undid remove achievement: {achievement.Id}");
    }

    #endregion

    #region Duplicate Achievement

    public void DuplicateAchievement(Achievement source)
    {
        var database = _getDatabaseFunc();
        if (database == null) return;

        var duplicate = CloneAchievement(source);

        // Ensure unique ID
        var baseId = duplicate.Id;
        var counter = 1;
        while (database.GetById(duplicate.Id) != null)
        {
            duplicate.Id = counter == 1 ? baseId : $"{baseId.Replace("_copy", "")}_{counter}_copy";
            counter++;
        }

        if (_undoRedoManager != null)
        {
            _undoRedoManager.CreateAction("Duplicate Achievement");
            _undoRedoManager.AddDoMethod(this, nameof(DoDuplicateAchievement), duplicate, source.Id);
            _undoRedoManager.AddUndoMethod(this, nameof(UndoDuplicateAchievement), duplicate);
            _undoRedoManager.CommitAction();
        }
        else
        {
            DoDuplicateAchievement(duplicate, source.Id);
        }
    }

    public void DoDuplicateAchievement(Achievement duplicate, string sourceId)
    {
        var database = _getDatabaseFunc();
        if (database == null) return;

        database.AddAchievement(duplicate);
        _saveDatabase();
        _refreshList(false);
        _selectAchievementById(duplicate.Id);

        AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Duplicated achievement: {sourceId} -> {duplicate.Id}");
    }

    public void UndoDuplicateAchievement(Achievement duplicate)
    {
        var database = _getDatabaseFunc();
        if (database == null) return;

        database.RemoveAchievement(duplicate.Id);
        _saveDatabase();
        _refreshList(false);

        AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Undid duplicate achievement: {duplicate.Id}");
    }

    private static Achievement CloneAchievement(Achievement original)
    {
        return new Achievement
        {
            Id = $"{original.Id}_copy",
            DisplayName = $"{original.DisplayName} (Copy)",
            Description = original.Description,
            Icon = original.Icon,
            SteamId = original.SteamId,
            GameCenterId = original.GameCenterId,
            GooglePlayId = original.GooglePlayId,
            IsIncremental = original.IsIncremental,
            MaxProgress = original.MaxProgress,
            CustomPlatformIds = new Godot.Collections.Dictionary<string, string>(original.CustomPlatformIds),
            ExtraProperties = new Godot.Collections.Dictionary<string, Variant>(original.ExtraProperties)
        };
    }

    #endregion

    #region Move Achievement

    public void MoveAchievementUp(Achievement achievement)
    {
        var database = _getDatabaseFunc();
        if (database == null) return;

        var currentIndex = database.Achievements.IndexOf(achievement);
        if (currentIndex <= 0) return;

        if (_undoRedoManager != null)
        {
            _undoRedoManager.CreateAction("Move Achievement Up");
            _undoRedoManager.AddDoMethod(this, nameof(DoMoveAchievement), achievement, currentIndex - 1);
            _undoRedoManager.AddUndoMethod(this, nameof(DoMoveAchievement), achievement, currentIndex);
            _undoRedoManager.CommitAction();
        }
        else
        {
            DoMoveAchievement(achievement, currentIndex - 1);
        }
    }

    public void MoveAchievementDown(Achievement achievement)
    {
        var database = _getDatabaseFunc();
        if (database == null) return;

        var currentIndex = database.Achievements.IndexOf(achievement);
        if (currentIndex < 0 || currentIndex >= database.Achievements.Count - 1) return;

        if (_undoRedoManager != null)
        {
            _undoRedoManager.CreateAction("Move Achievement Down");
            _undoRedoManager.AddDoMethod(this, nameof(DoMoveAchievement), achievement, currentIndex + 1);
            _undoRedoManager.AddUndoMethod(this, nameof(DoMoveAchievement), achievement, currentIndex);
            _undoRedoManager.CommitAction();
        }
        else
        {
            DoMoveAchievement(achievement, currentIndex + 1);
        }
    }

    public void DoMoveAchievement(Achievement achievement, int toIndex)
    {
        var database = _getDatabaseFunc();
        if (database == null) return;

        var currentIndex = database.Achievements.IndexOf(achievement);
        if (currentIndex < 0) return;

        database.Achievements.RemoveAt(currentIndex);
        database.Achievements.Insert(toIndex, achievement);

        _saveDatabase();
        _refreshList(true);
        AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Moved achievement: {achievement.DisplayName}");
    }

    #endregion

    #region Helpers

    private static string GenerateUniqueId(AchievementDatabase database)
    {
        var counter = 1;
        string id;
        do
        {
            id = $"achievement_{counter:D2}";
            counter++;
            if (counter > 9999)
            {
                id = $"achievement_{Guid.NewGuid():N}";
                break;
            }
        } while (database.GetById(id) != null);
        return id;
    }

    #endregion
}
#endif
