using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StudyRoom.Application.Interfaces;
using StudyRoom.Application.Messages;
using StudyRoom.Core.Models;

namespace StudyRoom.UI.ViewModels;

public partial class JoinGroupDialogViewModel : ObservableObject
{
    private readonly IGroupManagementService _groupService;
    private readonly IMessenger _messenger;

    public event Action? RequestClose;

    [ObservableProperty]
    private int _selectedTabIndex = 0; // 0 = Join Code, 1 = Explore, 2 = Create

    public bool IsJoinCodeTab => SelectedTabIndex == 0;
    public bool IsExploreTab => SelectedTabIndex == 1;
    public bool IsCreateTab => SelectedTabIndex == 2;

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsJoinCodeTab));
        OnPropertyChanged(nameof(IsExploreTab));
        OnPropertyChanged(nameof(IsCreateTab));
    }

    [RelayCommand]
    public void SelectTab(object? param)
    {
        if (param is int i) SelectedTabIndex = i;
        else if (param is string s && int.TryParse(s, out var parsed)) SelectedTabIndex = parsed;
    }

    [ObservableProperty]
    private string _inputJoinCode = string.Empty;

    [ObservableProperty]
    private GroupNavigationItem? _previewGroup;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    [ObservableProperty]
    private ObservableCollection<GroupNavigationItem> _exploreGroups = [];

    [ObservableProperty]
    private string _newGroupName = string.Empty;

    [ObservableProperty]
    private string _newGroupSubject = string.Empty;

    [ObservableProperty]
    private int _newGroupMaxMembers = 8;

    public JoinGroupDialogViewModel(IGroupManagementService groupService, IMessenger? messenger = null)
    {
        _groupService = groupService;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    [RelayCommand]
    public async Task LoadExploreGroupsAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _groupService.GetExploreGroupsAsync();
            ExploreGroups.Clear();
            foreach (var g in list)
            {
                ExploreGroups.Add(g);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task VerifyCodeAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;
        PreviewGroup = null;

        if (string.IsNullOrWhiteSpace(InputJoinCode))
        {
            ErrorMessage = "Vui lòng nhập mã phòng học.";
            return;
        }

        string code = InputJoinCode.Trim();
        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            ErrorMessage = "Mã phòng phải gồm chính xác 6 chữ số.";
            return;
        }

        IsBusy = true;
        try
        {
            var explore = await _groupService.GetExploreGroupsAsync();
            var matched = explore.FirstOrDefault(g => g.JoinCode == code);

            PreviewGroup = matched ?? new GroupNavigationItem
            {
                Id = Guid.NewGuid(),
                Name = $"Phòng học #{code}",
                Subject = "Học tập tổng hợp",
                AccentColor = "#6366F1",
                JoinCode = code,
                MemberCount = 3,
                HostName = "Chủ phòng"
            };
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ConfirmJoinAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        string code = PreviewGroup?.JoinCode ?? InputJoinCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            ErrorMessage = "Vui lòng nhập hoặc kiểm tra mã phòng.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _groupService.JoinGroupByCodeAsync(code);
            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
            }
            else if (result.Value is { } joined)
            {
                SuccessMessage = $"Đã tham gia nhóm '{joined.Name}' thành công!";
                _messenger.Send(new GroupMembershipChangedMessage(joined.Id, "JOINED"));
                _messenger.Send(new GroupSwitchedMessage(joined.Id, joined.Name, false, joined.JoinCode));
                RequestClose?.Invoke();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task QuickJoinExploreGroupAsync(GroupNavigationItem? item)
    {
        if (item is null) return;

        IsBusy = true;
        try
        {
            var result = await _groupService.JoinGroupByCodeAsync(item.JoinCode);
            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
            }
            else if (result.Value is { } joined)
            {
                SuccessMessage = $"Đã tham gia nhóm '{joined.Name}'!";
                _messenger.Send(new GroupMembershipChangedMessage(joined.Id, "JOINED"));
                _messenger.Send(new GroupSwitchedMessage(joined.Id, joined.Name, false, joined.JoinCode));
                RequestClose?.Invoke();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ConfirmCreateGroupAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(NewGroupName))
        {
            ErrorMessage = "Vui lòng nhập tên nhóm học tập.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewGroupSubject))
        {
            ErrorMessage = "Vui lòng nhập môn học hoặc chủ đề.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _groupService.CreateGroupAsync(NewGroupName, NewGroupSubject, NewGroupMaxMembers);
            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
            }
            else if (result.Value is { } created)
            {
                SuccessMessage = $"Đã tạo nhóm '{created.Name}' thành công!";
                _messenger.Send(new GroupMembershipChangedMessage(created.Id, "CREATED"));
                _messenger.Send(new GroupSwitchedMessage(created.Id, created.Name, false, created.JoinCode));
                RequestClose?.Invoke();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
