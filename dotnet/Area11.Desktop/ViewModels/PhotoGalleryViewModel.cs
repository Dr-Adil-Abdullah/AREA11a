using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Microsoft.Win32;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The photo gallery of one record (F-41, Spec 17.1 &amp; D-21) - a medicine, a customer or a
/// supplier, two or three pictures each.
///
/// The window has one job: put the right picture in front of the person and keep the list honest.
/// Everything that touches the disk or the database is in <see cref="PhotoService"/>; this class
/// opens the file picker, starts the Windows Camera app, and holds the preview picture.
///
/// The preview is loaded into memory **now** (`OnLoad`, file handle closed at once), so the picture
/// is visible while the file could still be replaced, and no file is left locked on the shop PC.
/// </summary>
public sealed class PhotoGalleryViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private PhotoRow? _selected;
    private BitmapImage? _preview;
    private bool _includeHidden;
    private string _statusText = "Add a photo with the button - nothing is written until you press it.";
    private bool _isStatusError;
    private bool _canEdit;
    private string _countText = "no photos";
    private string _hiddenText = string.Empty;
    private bool _lastLoadSucceeded;
    // Window-local only: Refresh/access regrant never resolves an uncertain Add.
    private bool _addNeedsVerification;
    private readonly string _recordTitle;
    private string _title = string.Empty;

    public PhotoGalleryViewModel(AppServices services, User user, string entity, int entityId, string title)
    {
        _services = services;
        _user = user;

        Entity = PhotoEntity.Clean(entity);
        EntityId = entityId;
        _recordTitle = title;
        Title = title;
        Subtitle = PhotoEntity.LabelFor(Entity) + " · " + PhotoItem.CountLabel(0);

        AddCommand = new RelayCommand(Add);
        TakePhotoCommand = new RelayCommand(TakePhoto);
        HideCommand = new RelayCommand(Hide);
        SetCoverCommand = new RelayCommand(SetCover);
        RestoreCommand = new RelayCommand(Restore);
        OpenCommand = new RelayCommand(OpenFile);
        RefreshCommand = new RelayCommand(Load);
        ShowHiddenCommand = new RelayCommand(ToggleHidden);

        _canEdit = false; // first successful current gallery read enables actions

        Load();
    }

    /// <summary>The window title carries the shop's app name (v0.69.0, de-workshop).</summary>
    public string WindowTitle => AppDisplayName + " — Photos";

    private string AppDisplayName
    {
        get
        {
            var name = _services.Settings.GetString("brand.appName").Trim();
            return name.Length == 0 ? "Area11" : name;
        }
    }

    public string Entity { get; }

    public int EntityId { get; }

    public string Title { get => _title; private set => SetProperty(ref _title, value); }

    public RelayCommand AddCommand { get; }

    public RelayCommand TakePhotoCommand { get; }

    public RelayCommand HideCommand { get; }

    public RelayCommand SetCoverCommand { get; }

    public bool IsProduct => Entity == PhotoEntity.Product;

    public bool CanSetCover => IsProduct && CanEdit && _selected is { IsActive: true };

    public RelayCommand RestoreCommand { get; }

    public RelayCommand OpenCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ShowHiddenCommand { get; }

    /// <summary>
    /// The photos as rows. A row owns its decoded, frozen thumbnail, so the list never holds a file
    /// open - a backup or a restore must not fail because a picture is being looked at.
    /// </summary>
    public ObservableCollection<PhotoRow> Photos { get; } = new();

    /// <summary>"Medicine · 3 photos" - the line under the window title.</summary>
    public string Subtitle
    {
        get => _subtitle;
        private set => SetProperty(ref _subtitle, value);
    }

    private string _subtitle = string.Empty;

    public PhotoRow? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value))
            {
                return;
            }

            LoadPreview(value);
            OnPropertyChanged(nameof(CanHide));
            OnPropertyChanged(nameof(CanRestore));
            OnPropertyChanged(nameof(CanSetCover));
            OnPropertyChanged(nameof(SelectedCaption));
            OnPropertyChanged(nameof(SelectedWhen));
            OnPropertyChanged(nameof(SelectedMissing));
        }
    }

    public bool CanHide => CanEdit && _selected is { IsActive: true };

    public bool CanRestore => CanEdit && _selected is { IsActive: false };

    /// <summary>False for a reader who may look at the photos but not change them.</summary>
    public bool CanEdit
    {
        get => _canEdit;
        private set
        {
            if (SetProperty(ref _canEdit, value))
            {
                OnPropertyChanged(nameof(IsReadOnly));
                OnPropertyChanged(nameof(CanHide));
                OnPropertyChanged(nameof(CanRestore));
                OnPropertyChanged(nameof(CanSetCover));
            }
        }
    }

    public bool IsReadOnly => !CanEdit;

    /// <summary>The picture in the big frame.</summary>
    public BitmapImage? Preview
    {
        get => _preview;
        private set => SetProperty(ref _preview, value);
    }

    public bool HasPreview => Preview is not null;

    /// <summary>True when the row is there but the file is not - said plainly, never hidden.</summary>
    public bool SelectedMissing => _selected is { Item.Exists: false };

    public bool HasNoPhotos => CanEdit && Photos.Count == 0;

    public string SelectedCaption => _selected?.Caption ?? "No photo chosen";

    public string SelectedWhen => _selected?.StateLabel ?? string.Empty;

    /// <summary>True while put-away photos are shown beside the live ones.</summary>
    public bool IncludeHidden
    {
        get => _includeHidden;
        private set
        {
            if (SetProperty(ref _includeHidden, value))
            {
                OnPropertyChanged(nameof(HiddenToggleLabel));
            }
        }
    }

    public string HiddenToggleLabel => IncludeHidden ? "Hide the put-away photos" : "Show put-away photos";

    public string CountText
    {
        get => _countText;
        private set => SetProperty(ref _countText, value);
    }

    public string HiddenText
    {
        get => _hiddenText;
        private set => SetProperty(ref _hiddenText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        private set => SetProperty(ref _isStatusError, value);
    }

    /// <summary>The camera line - why the picture appears in Pictures and not in this window (D-58).</summary>
    public string CameraHint => PhotoService.CameraHint;

    // ------------------------------------------------------------------ loading

    public void Load()
    {
        _lastLoadSucceeded = false;
        var keep = _selected?.Item.Id;
        var result = _services.Photos.List(_user, Entity, EntityId, IncludeHidden);

        if (!result.Ok || result.Value is null)
        {
            ClearGallery(result.Error ?? "The photos could not be loaded.");
            return;
        }

        CanEdit = true;
        Title = _recordTitle;
        var all = result.Value;
        var active = all.Count(photo => photo.IsActive);
        var hidden = all.Count - active;

        Photos.Clear();
        var coverId = Entity == PhotoEntity.Product ? all.FirstOrDefault(photo => photo.IsActive)?.Id : null;
        foreach (var photo in all)
        {
            Photos.Add(new PhotoRow(photo, photo.Id == coverId));
        }

        CountText = PhotoItem.CountLabel(active);
        HiddenText = hidden == 0 ? string.Empty : hidden + " put away";
        Subtitle = PhotoEntity.LabelFor(Entity) + " · " + CountText;

        Selected = keep is null
            ? Photos.FirstOrDefault(photo => photo.IsActive) ?? Photos.FirstOrDefault()
            : Photos.FirstOrDefault(photo => photo.Item.Id == keep) ?? Photos.FirstOrDefault();

        if (Selected is null)
        {
            Preview = null;
            OnPropertyChanged(nameof(HasPreview));
            OnPropertyChanged(nameof(SelectedMissing));
        }

        _lastLoadSucceeded = CanEdit; // a failed current preview read may have cleared access
        RaiseListState();
    }

    public bool RefreshAccess()
    {
        var access = _services.Photos.CheckAccess(_user, Entity, EntityId);
        if (!access.Ok)
        {
            ClearGallery(access.Error ?? "Photo access is not available.");
            return false;
        }
        if (!CanEdit) Load();
        return CanEdit;
    }

    private void ClearGallery(string message)
    {
        CanEdit = false;
        _lastLoadSucceeded = false;
        Selected = null;
        Preview = null;
        Photos.Clear();
        Title = "Photos";
        CountText = "Photos not loaded";
        HiddenText = string.Empty;
        Subtitle = "Photo access needs checking";
        OnPropertyChanged(nameof(HasPreview));
        RaiseListState();
        SetStatus(message, true);
    }

    private void RaiseListState()
    {
        OnPropertyChanged(nameof(HasNoPhotos));
        OnPropertyChanged(nameof(CanHide));
        OnPropertyChanged(nameof(CanRestore));
        OnPropertyChanged(nameof(CanSetCover));
    }

    private void LoadPreview(PhotoRow? row)
    {
        Preview = null;

        if (row is null || !row.Item.Exists)
        {
            OnPropertyChanged(nameof(HasPreview));
            return;
        }

        try
        {
            if (row.Item.Entity != Entity || row.Item.EntityId != EntityId)
            {
                ClearGallery("Choose a photo from this record.");
                return;
            }
            var path = _services.Photos.PathOf(_user, row.Item.Id);
            if (!path.Ok || string.IsNullOrWhiteSpace(path.Value))
            {
                ClearGallery(path.Error ?? "The picture could not be read.");
                return;
            }
            Preview = Decode(path.Value, decodeWidth: null);
        }
        catch (Exception exception)
        {
            SetStatus("The picture could not be shown (" + exception.Message + ").", true);
        }

        OnPropertyChanged(nameof(HasPreview));
    }

    /// <summary>
    /// Reads a picture into memory and closes the file at once (`OnLoad`). A frozen image is safe to
    /// hand to a background thread and cannot lock the shop's photo folder.
    /// </summary>
    internal static BitmapImage? Decode(string path, int? decodeWidth)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        image.UriSource = new Uri(path, UriKind.Absolute);

        if (decodeWidth is > 0)
        {
            // Decoding small keeps a six-photo list light; the big frame loads the full picture.
            image.DecodePixelWidth = decodeWidth.Value;
        }

        image.EndInit();
        image.Freeze();

        return image;
    }

    // ------------------------------------------------------------------ actions

    private void Add()
    {
        if (!CanBeginAdd() || !RefreshAccess()) return;
        var dialog = new OpenFileDialog
        {
            Title = "Choose a photo",
            Filter = "Pictures (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        // Start where a phone or the camera app leaves its pictures - that is what the owner picks.
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        if (Directory.Exists(pictures))
        {
            dialog.InitialDirectory = pictures;
        }

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        AddFile(dialog.FileName);
    }

    private bool CanBeginAdd()
    {
        if (!_addNeedsVerification) return true;
        SetStatus("Do not add again in this window. Verify the record, gallery and photo folder first; then reopen the gallery. Refresh alone does not confirm the previous outcome.", true);
        return false;
    }

    /// <summary>Adds one file - also the door the camera button uses once the picture exists.</summary>
    public void AddFile(string path)
    {
        if (!CanBeginAdd() || !RefreshAccess()) return;
        _addNeedsVerification = true; // Before service invocation: thrown/unknown is not permission to retry.
        var acknowledged = false;
        try
        {
            var added = _services.Photos.Add(_user, Entity, EntityId, path);
            if (!added.Ok || added.Value is null)
            {
                if (!added.Ok && added.NoCommitConfirmed) _addNeedsVerification = false;
                SetStatus(added.Error ?? "The photo outcome needs verification before another Add.", true);
                return;
            }

            acknowledged = true;
            Load();
            if (!_lastLoadSucceeded)
            {
                SetStatus("SAVED. The photo was stored, but the gallery could not refresh. Do not add again; verify before reopening.", true);
                return;
            }
            Selected = Photos.FirstOrDefault(photo => photo.Item.Id == added.Value.Id) ?? Selected;
            if (!CanEdit || Selected?.Item.Id != added.Value.Id || !HasPreview)
            {
                SetStatus("SAVED. The photo was added, but its preview could not be verified. Do not add again; verify before reopening.", true);
                return;
            }
            SetStatus("Photo added. The original file is still where it was - this is a copy.", false);
            _addNeedsVerification = false; // Acknowledged Add AND completed gallery/preview cleanup.
        }
        catch (Exception exception)
        {
            _services.Log.Error("Photo Add or its gallery refresh failed.", exception);
            SetStatus(acknowledged
                ? "SAVED. Gallery refresh failed. Do not add again; verify before reopening."
                : "The photo outcome is unconfirmed. Do not add again; verify the record, gallery and photo folder before reopening.", true);
        }
    }

    /// <summary>
    /// Opens the Windows Camera app. The picture it saves lands in Pictures\Camera Roll; the owner
    /// then presses "Add a photo" and picks it. Explained in the window, not hidden (D-58).
    /// </summary>
    private void TakePhoto()
    {
        if (!RefreshAccess()) return;
        try
        {
            Process.Start(new ProcessStartInfo("microsoft.windows.camera:") { UseShellExecute = true });
            SetStatus("The Windows Camera app is open. Take the picture, then press \"Add a photo\" and choose it from Pictures.", false);
        }
        catch (Exception exception)
        {
            SetStatus("The Camera app could not be opened (" + exception.Message
                      + "). Take the picture on a phone and copy it over, then press \"Add a photo\".", true);
        }
    }

    private void SetCover()
    {
        if (!RefreshAccess()) return;
        if (_selected is null || !CanSetCover)
        {
            SetStatus("Choose an active product photo first.", true);
            return;
        }
        var result = _services.Photos.SetProductCover(_user, _selected.Item.Id);
        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The cover photo could not be changed.", true);
            return;
        }
        Load();
        if (!_lastLoadSucceeded)
        {
            SetStatus("SAVED. The photo change was stored, but the gallery could not refresh. Do not repeat the action; refresh to check.", true);
            return;
        }
        SetStatus("Cover selected. It will be shown in product lists after refresh.", false);
    }

    private void Hide()
    {
        if (!RefreshAccess()) return;
        if (_selected is null)
        {
            SetStatus("Choose a photo first.", true);
            return;
        }

        var name = _selected.Caption;
        var hidden = _services.Photos.Hide(_user, _selected.Item.Id);

        if (!hidden.Ok)
        {
            SetStatus(hidden.Error ?? "The photo could not be put away.", true);
            return;
        }

        Load();
        if (!_lastLoadSucceeded)
        {
            SetStatus("SAVED. The photo change was stored, but the gallery could not refresh. Do not repeat the action; refresh to check.", true);
            return;
        }
        SetStatus(name + " is put away. The file is still in the photos folder and the change is in the black box.", false);
    }

    private void Restore()
    {
        if (!RefreshAccess()) return;
        if (_selected is null)
        {
            SetStatus("Choose a photo first.", true);
            return;
        }

        var restored = _services.Photos.Restore(_user, _selected.Item.Id);

        if (!restored.Ok)
        {
            SetStatus(restored.Error ?? "The photo could not be brought back.", true);
            return;
        }

        Load();
        if (!_lastLoadSucceeded)
        {
            SetStatus("SAVED. The photo change was stored, but the gallery could not refresh. Do not repeat the action; refresh to check.", true);
            return;
        }
        SetStatus("The photo is back in the list.", false);
    }

    private void OpenFile()
    {
        if (!RefreshAccess()) return;
        if (_selected is null)
        {
            SetStatus("Choose a photo first.", true);
            return;
        }

        var path = _services.Photos.PathOf(_user, _selected.Item.Id);

        if (!path.Ok || path.Value is null)
        {
            SetStatus(path.Error ?? "The picture is not there any more.", true);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path.Value) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            SetStatus("Windows could not open the picture (" + exception.Message + ").", true);
        }
    }

    private void ToggleHidden()
    {
        IncludeHidden = !IncludeHidden;
        Load();
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}

/// <summary>
/// One tile in the photo strip. It wraps the stored row and holds the small, frozen picture, so the
/// window can draw six photos without keeping six files open on the shop PC.
/// </summary>
public sealed class PhotoRow
{
    public PhotoRow(PhotoItem item, bool isCover = false)
    {
        Item = item;
        IsCover = isCover;

        try
        {
            Thumbnail = PhotoGalleryViewModel.Decode(item.FullPath, decodeWidth: 128);
        }
        catch (Exception)
        {
            // A file with the wrong bytes (a renamed document, a half-copied photo) must not stop the
            // whole list: the row still shows its name and the big frame says the picture is missing.
            Thumbnail = null;
        }
    }

    public PhotoItem Item { get; }

    public bool IsCover { get; }

    /// <summary>True while the photo is on the shelf; false for one the owner put away.</summary>
    public bool IsActive => Item.IsActive;

    public BitmapImage? Thumbnail { get; }

    public string Caption => (IsCover ? "★ Cover · " : string.Empty) + Item.Caption;

    public string StateLabel => Item.Exists ? Item.StateLabel : Item.StateLabel + " - file missing";
}
