#if TOOLS
using System;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Handles import/export UI operations for achievements.
/// Manages file dialogs and delegates to AchievementImportExport utility.
/// </summary>
public partial class AchievementImportExportHandler : RefCounted
{
    private readonly Func<AchievementDatabase?> _getDatabaseFunc;
    private readonly Action _saveDatabase;
    private readonly Action<bool> _refreshList;
    private readonly Func<Node> _getParentNodeFunc;

    private EditorFileDialog? _importCSVFileDialog;
    private EditorFileDialog? _exportCSVFileDialog;
    private EditorFileDialog? _importJSONFileDialog;
    private EditorFileDialog? _exportJSONFileDialog;
    /// <summary>
    /// Parameterless constructor required by Godot's serialization system.
    /// This constructor is never called directly - use the parameterized constructor instead.
    /// </summary>
#pragma warning disable CS8618 // Required for Godot serialization - fields initialized via parameterized constructor
    public AchievementImportExportHandler() { }
#pragma warning restore CS8618
    public AchievementImportExportHandler(
        Func<AchievementDatabase?> getDatabaseFunc,
        Action saveDatabase,
        Action<bool> refreshList,
        Func<Node> getParentNodeFunc)
    {
        _getDatabaseFunc = getDatabaseFunc;
        _saveDatabase = saveDatabase;
        _refreshList = refreshList;
        _getParentNodeFunc = getParentNodeFunc;
    }

    public void SetupMenuButtons(MenuButton importButton, MenuButton exportButton)
    {
        var importPopup = importButton.GetPopup();
        importPopup.AddItem("CSV", 0);
        importPopup.AddItem("JSON", 1);
        importPopup.IdPressed += OnImportMenuItemPressed;

        var exportPopup = exportButton.GetPopup();
        exportPopup.AddItem("CSV", 0);
        exportPopup.AddItem("JSON", 1);
        exportPopup.IdPressed += OnExportMenuItemPressed;
    }

    public void CreateFileDialogs(Node parent)
    {
        // Create import CSV file dialog
        _importCSVFileDialog = new EditorFileDialog();
        _importCSVFileDialog.FileMode = EditorFileDialog.FileModeEnum.OpenFile;
        _importCSVFileDialog.AddFilter("*.csv", "CSV Files");
        _importCSVFileDialog.Access = EditorFileDialog.AccessEnum.Filesystem;
        _importCSVFileDialog.Title = "Import Achievements from CSV";
        _importCSVFileDialog.FileSelected += OnImportCSVFileSelected;
        parent.AddChild(_importCSVFileDialog);

        // Create export CSV file dialog
        _exportCSVFileDialog = new EditorFileDialog();
        _exportCSVFileDialog.FileMode = EditorFileDialog.FileModeEnum.SaveFile;
        _exportCSVFileDialog.AddFilter("*.csv", "CSV Files");
        _exportCSVFileDialog.Access = EditorFileDialog.AccessEnum.Filesystem;
        _exportCSVFileDialog.Title = "Export Achievements to CSV";
        _exportCSVFileDialog.FileSelected += OnExportCSVFileSelected;
        parent.AddChild(_exportCSVFileDialog);

        // Create import JSON file dialog
        _importJSONFileDialog = new EditorFileDialog();
        _importJSONFileDialog.FileMode = EditorFileDialog.FileModeEnum.OpenFile;
        _importJSONFileDialog.AddFilter("*.json", "JSON Files");
        _importJSONFileDialog.Access = EditorFileDialog.AccessEnum.Filesystem;
        _importJSONFileDialog.Title = "Import Achievements from JSON";
        _importJSONFileDialog.FileSelected += OnImportJSONFileSelected;
        parent.AddChild(_importJSONFileDialog);

        // Create export JSON file dialog
        _exportJSONFileDialog = new EditorFileDialog();
        _exportJSONFileDialog.FileMode = EditorFileDialog.FileModeEnum.SaveFile;
        _exportJSONFileDialog.AddFilter("*.json", "JSON Files");
        _exportJSONFileDialog.Access = EditorFileDialog.AccessEnum.Filesystem;
        _exportJSONFileDialog.Title = "Export Achievements to JSON";
        _exportJSONFileDialog.FileSelected += OnExportJSONFileSelected;
        parent.AddChild(_exportJSONFileDialog);
    }

    public void Cleanup(MenuButton importButton, MenuButton exportButton)
    {
        var importPopup = importButton.GetPopup();
        importPopup.IdPressed -= OnImportMenuItemPressed;

        var exportPopup = exportButton.GetPopup();
        exportPopup.IdPressed -= OnExportMenuItemPressed;

        if (_importCSVFileDialog != null)
        {
            _importCSVFileDialog.FileSelected -= OnImportCSVFileSelected;
            _importCSVFileDialog.QueueFree();
        }

        if (_exportCSVFileDialog != null)
        {
            _exportCSVFileDialog.FileSelected -= OnExportCSVFileSelected;
            _exportCSVFileDialog.QueueFree();
        }

        if (_importJSONFileDialog != null)
        {
            _importJSONFileDialog.FileSelected -= OnImportJSONFileSelected;
            _importJSONFileDialog.QueueFree();
        }

        if (_exportJSONFileDialog != null)
        {
            _exportJSONFileDialog.FileSelected -= OnExportJSONFileSelected;
            _exportJSONFileDialog.QueueFree();
        }
    }

    private void OnImportMenuItemPressed(long id)
    {
        var database = _getDatabaseFunc();
        if (database == null)
        {
            ShowInfoDialog("Please load a database first before importing achievements.");
            return;
        }

        switch (id)
        {
            case 0: // CSV
                if (_importCSVFileDialog != null)
                {
                    _importCSVFileDialog.CurrentPath = "achievements.csv";
                    _importCSVFileDialog.PopupCentered(new Vector2I(800, 600));
                }
                break;
            case 1: // JSON
                if (_importJSONFileDialog != null)
                {
                    _importJSONFileDialog.CurrentPath = "achievements.json";
                    _importJSONFileDialog.PopupCentered(new Vector2I(800, 600));
                }
                break;
        }
    }

    private void OnExportMenuItemPressed(long id)
    {
        var database = _getDatabaseFunc();
        if (database == null || database.Achievements.Count == 0)
        {
            ShowInfoDialog("No achievements to export. Please load a database with achievements first.");
            return;
        }

        switch (id)
        {
            case 0: // CSV
                if (_exportCSVFileDialog != null)
                {
                    _exportCSVFileDialog.CurrentPath = "achievements.csv";
                    _exportCSVFileDialog.PopupCentered(new Vector2I(800, 600));
                }
                break;
            case 1: // JSON
                if (_exportJSONFileDialog != null)
                {
                    _exportJSONFileDialog.CurrentPath = "achievements.json";
                    _exportJSONFileDialog.PopupCentered(new Vector2I(800, 600));
                }
                break;
        }
    }

    private void OnImportCSVFileSelected(string path)
    {
        var database = _getDatabaseFunc();
        if (database == null) return;

        var result = AchievementImportExport.ImportFromCSV(database, path);

        if (result.Success)
        {
            _saveDatabase();
            _refreshList(true);
            ShowSuccessDialog("Import Successful",
                $"CSV Import Complete!\n\nNew achievements: {result.ImportedCount}\nUpdated achievements: {result.UpdatedCount}\nSkipped (unchanged): {result.SkippedCount}");
        }
        else
        {
            ShowErrorDialog(result.ErrorMessage ?? "Unknown error");
        }
    }

    private void OnExportCSVFileSelected(string path)
    {
        var database = _getDatabaseFunc();
        if (database == null || database.Achievements.Count == 0) return;

        var result = AchievementImportExport.ExportToCSV(database, path);

        if (result.Success)
        {
            ShowSuccessDialog("Export Successful",
                $"Successfully exported {result.ExportedCount} achievements to:\n\n{path}");
        }
        else
        {
            ShowErrorDialog(result.ErrorMessage ?? "Unknown error");
        }
    }

    private void OnImportJSONFileSelected(string path)
    {
        var database = _getDatabaseFunc();
        if (database == null) return;

        var result = AchievementImportExport.ImportFromJSON(database, path);

        if (result.Success)
        {
            _saveDatabase();
            _refreshList(true);
            ShowSuccessDialog("Import Successful",
                $"JSON Import Complete!\n\nNew achievements: {result.ImportedCount}\nUpdated achievements: {result.UpdatedCount}\nSkipped (unchanged): {result.SkippedCount}");
        }
        else
        {
            ShowErrorDialog(result.ErrorMessage ?? "Unknown error");
        }
    }

    private void OnExportJSONFileSelected(string path)
    {
        var database = _getDatabaseFunc();
        if (database == null || database.Achievements.Count == 0) return;

        var result = AchievementImportExport.ExportToJSON(database, path);

        if (result.Success)
        {
            ShowSuccessDialog("Export Successful",
                $"Successfully exported {result.ExportedCount} achievements to:\n\n{path}");
        }
        else
        {
            ShowErrorDialog(result.ErrorMessage ?? "Unknown error");
        }
    }

    private void ShowInfoDialog(string message)
    {
        var parent = _getParentNodeFunc();
        var dialog = new AcceptDialog();
        dialog.DialogText = message;
        parent.AddChild(dialog);
        dialog.PopupCentered();
    }

    private void ShowSuccessDialog(string title, string message)
    {
        var parent = _getParentNodeFunc();
        var dialog = new AcceptDialog();
        dialog.Title = title;
        dialog.DialogText = message;
        parent.AddChild(dialog);
        dialog.PopupCentered();
    }

    private void ShowErrorDialog(string message)
    {
        var parent = _getParentNodeFunc();
        var dialog = new AcceptDialog();
        dialog.Title = "Error";
        dialog.DialogText = message;
        parent.AddChild(dialog);
        dialog.PopupCentered();
    }
}
#endif
