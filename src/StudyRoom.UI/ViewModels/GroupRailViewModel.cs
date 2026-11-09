using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StudyRoom.Application.Interfaces;
using StudyRoom.Application.Messages;
using StudyRoom.Core.Models;

namespace StudyRoom.UI.ViewModels;

public partial class GroupRailViewModel : ObservableObject
{
    private readonly IGroupManagementService _groupService;
    private readonly IMessenger _messenger;

    public event Action? RequestOpenJoinDialog;

    [ObservableProperty]
    private ObservableCollection<GroupNavigationItem> _groups = [];

    [ObservableProperty]
    private GroupNavigationItem _personalHome;

    [ObservableProperty]
    private GroupNavigationItem? _selectedGroup;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    public GroupRailViewModel(IGroupManagementService groupService, IMessenger? messenger = null)
    {
        _groupService = groupService;
        _messenger = messenger ?? WeakReferenceMessenger.Default;

        _personalHome = new GroupNavigationItem
        {
            Id = Guid.Empty,
            Name = "Không gian cá nhân",
            Subject = "Tự học độc lập",
            IsPersonal = true,
            IsSelected = true,
            AccentColor = "#6366F1"
        };
        _selectedGroup = _personalHome;

        _messenger.Register<GroupMembershipChangedMessage>(this, async (r, m) =>
        {
            await ((GroupRailViewModel)r).LoadGroupsAsync();
        });
    }

    [RelayCommand]
    public async Task LoadGroupsAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _groupService.GetUserGroupsAsync();
            Groups.Clear();

            // Pinned first, then by name/recent
            var sorted = list.OrderByDescending(g => g.IsPinned).ThenBy(g => g.Name);
            foreach (var g in sorted)
            {
                g.IsSelected = SelectedGroup != null && SelectedGroup.Id == g.Id;
                Groups.Add(g);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void SelectGroup(GroupNavigationItem? item)
    {
        if (item is null) return;

        PersonalHome.IsSelected = false;
        foreach (var g in Groups)
        {
            g.IsSelected = g.Id == item.Id;
        }

        item.IsSelected = true;
        item.UnreadCount = 0; // Clear unread on focus
        SelectedGroup = item;

        _messenger.Send(new GroupSwitchedMessage(item.Id, item.Name, item.IsPersonal, item.JoinCode));
    }

    [RelayCommand]
    public void SelectPersonalHome()
    {
        foreach (var g in Groups)
        {
            g.IsSelected = false;
        }

        PersonalHome.IsSelected = true;
        SelectedGroup = PersonalHome;

        _messenger.Send(new GroupSwitchedMessage(Guid.Empty, "Không gian cá nhân", true, string.Empty));
    }

    [RelayCommand]
    public void OpenJoinDialog()
    {
        if (RequestOpenJoinDialog != null)
        {
            RequestOpenJoinDialog.Invoke();
        }
        else
        {
            OpenDefaultJoinDialog();
        }
    }

    private void OpenDefaultJoinDialog()
    {
        if (System.Windows.Application.Current != null)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialogVm = new JoinGroupDialogViewModel(_groupService, _messenger);
                    var dialog = new Views.Components.JoinGroupDialog(dialogVm);
                    dialog.Owner = System.Windows.Application.Current.MainWindow;
                    dialog.ShowDialog();
                });
            }
            catch
            {
                // Headless test runner fallback
            }
        }
    }

    [RelayCommand]
    public void CopyInviteCode(GroupNavigationItem? item)
    {
        var target = item ?? SelectedGroup;
        if (target != null && !string.IsNullOrWhiteSpace(target.JoinCode))
        {
            try
            {
                System.Windows.Clipboard.SetText(target.JoinCode);
                StatusMessage = $"Đã sao chép mã mời '{target.JoinCode}'!";
            }
            catch
            {
                // Headless test runner fallback
            }
        }
    }

    [RelayCommand]
    public async Task PinGroupAsync(GroupNavigationItem? item)
    {
        var target = item ?? SelectedGroup;
        if (target != null && !target.IsPersonal)
        {
            bool newPin = !target.IsPinned;
            await _groupService.PinGroupAsync(target.Id, newPin);
            target.IsPinned = newPin;
            await LoadGroupsAsync();
        }
    }

    [RelayCommand]
    public async Task MuteGroupAsync(GroupNavigationItem? item)
    {
        var target = item ?? SelectedGroup;
        if (target != null && !target.IsPersonal)
        {
            bool newMute = !target.IsMuted;
            await _groupService.MuteGroupAsync(target.Id, newMute);
            target.IsMuted = newMute;
        }
    }

    [RelayCommand]
    public async Task LeaveGroupAsync(GroupNavigationItem? item)
    {
        var target = item ?? SelectedGroup;
        if (target != null && !target.IsPersonal)
        {
            await _groupService.LeaveGroupAsync(target.Id);
            Groups.Remove(target);

            if (SelectedGroup?.Id == target.Id)
            {
                SelectPersonalHome();
            }

            _messenger.Send(new GroupMembershipChangedMessage(target.Id, "LEFT"));
        }
    }
}
