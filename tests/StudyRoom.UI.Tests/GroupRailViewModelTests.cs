using CommunityToolkit.Mvvm.Messaging;
using StudyRoom.Application.Interfaces;
using StudyRoom.Application.Messages;
using StudyRoom.Core.Common;
using StudyRoom.Core.Models;
using StudyRoom.UI.ViewModels;
using Xunit;

namespace StudyRoom.UI.Tests;

public class GroupRailViewModelTests
{
    private class FakeGroupService : IGroupManagementService
    {
        public List<GroupNavigationItem> Groups { get; } = [
            new() { Id = Guid.NewGuid(), Name = "Group A", JoinCode = "111222", IsPinned = false },
            new() { Id = Guid.NewGuid(), Name = "Group B", JoinCode = "333444", IsPinned = true }
        ];

        public Task<IReadOnlyList<GroupNavigationItem>> GetUserGroupsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GroupNavigationItem>>(Groups.ToList());

        public Task<Result<GroupNavigationItem>> JoinGroupByCodeAsync(string joinCode, CancellationToken cancellationToken = default)
        {
            var item = new GroupNavigationItem { Id = Guid.NewGuid(), Name = $"Group {joinCode}", JoinCode = joinCode };
            Groups.Add(item);
            return Task.FromResult(Result<GroupNavigationItem>.Success(item));
        }

        public Task<Result<GroupNavigationItem>> CreateGroupAsync(string name, string subject, int maxMembers, CancellationToken cancellationToken = default)
        {
            var item = new GroupNavigationItem { Id = Guid.NewGuid(), Name = name, Subject = subject, JoinCode = "999888", IsHost = true };
            Groups.Add(item);
            return Task.FromResult(Result<GroupNavigationItem>.Success(item));
        }

        public Task<Result> LeaveGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            Groups.RemoveAll(g => g.Id == groupId);
            return Task.FromResult(Result.Success());
        }

        public Task<Result> DisbandGroupAsync(Guid groupId, CancellationToken cancellationToken = default) => LeaveGroupAsync(groupId, cancellationToken);
        public Task<IReadOnlyList<GroupNavigationItem>> GetExploreGroupsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GroupNavigationItem>>([]);
        public Task PinGroupAsync(Guid groupId, bool isPinned, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MuteGroupAsync(Guid groupId, bool isMuted, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void Constructor_SetsPersonalHomeAsSelected()
    {
        var service = new FakeGroupService();
        var vm = new GroupRailViewModel(service);

        Assert.NotNull(vm.PersonalHome);
        Assert.True(vm.PersonalHome.IsPersonal);
        Assert.True(vm.PersonalHome.IsSelected);
        Assert.Equal(vm.PersonalHome, vm.SelectedGroup);
    }

    [Fact]
    public async Task LoadGroupsAsync_OrdersPinnedFirst()
    {
        var service = new FakeGroupService();
        var vm = new GroupRailViewModel(service);

        await vm.LoadGroupsAsync();

        Assert.Equal(2, vm.Groups.Count);
        Assert.True(vm.Groups[0].IsPinned); // Group B is pinned
        Assert.Equal("Group B", vm.Groups[0].Name);
    }

    [Fact]
    public void SelectGroup_SwitchesSelectionAndSendsMessage()
    {
        var service = new FakeGroupService();
        var messenger = new WeakReferenceMessenger();
        var vm = new GroupRailViewModel(service, messenger);

        GroupSwitchedMessage? receivedMessage = null;
        messenger.Register<GroupSwitchedMessage>(this, (_, m) => receivedMessage = m);

        var target = service.Groups[0];
        vm.SelectGroup(target);

        Assert.Equal(target, vm.SelectedGroup);
        Assert.False(vm.PersonalHome.IsSelected);
        Assert.True(target.IsSelected);
        Assert.NotNull(receivedMessage);
        Assert.Equal(target.Id, receivedMessage!.GroupId);
        Assert.False(receivedMessage.IsPersonal);
    }

    [Fact]
    public void SelectPersonalHome_BroadcastsPersonalMessage()
    {
        var service = new FakeGroupService();
        var messenger = new WeakReferenceMessenger();
        var vm = new GroupRailViewModel(service, messenger);

        GroupSwitchedMessage? receivedMessage = null;
        messenger.Register<GroupSwitchedMessage>(this, (_, m) => receivedMessage = m);

        vm.SelectPersonalHome();

        Assert.True(vm.PersonalHome.IsSelected);
        Assert.Equal(vm.PersonalHome, vm.SelectedGroup);
        Assert.NotNull(receivedMessage);
        Assert.True(receivedMessage!.IsPersonal);
        Assert.Equal(Guid.Empty, receivedMessage.GroupId);
    }

    [Fact]
    public async Task LeaveGroupAsync_RemovesGroupAndFallsBackToHome()
    {
        var service = new FakeGroupService();
        var messenger = new WeakReferenceMessenger();
        var vm = new GroupRailViewModel(service, messenger);
        await vm.LoadGroupsAsync();

        var target = vm.Groups[0];
        vm.SelectGroup(target);

        await vm.LeaveGroupAsync(target);

        Assert.DoesNotContain(target, vm.Groups);
        Assert.True(vm.PersonalHome.IsSelected);
    }
}
