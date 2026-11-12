using CommunityToolkit.Mvvm.Messaging;
using StudyRoom.Application.Interfaces;
using StudyRoom.Application.Messages;
using StudyRoom.Core.Common;
using StudyRoom.Core.Models;
using StudyRoom.UI.ViewModels;
using Xunit;

namespace StudyRoom.UI.Tests;

public class JoinGroupDialogViewModelTests
{
    private class FakeGroupService : IGroupManagementService
    {
        public Task<IReadOnlyList<GroupNavigationItem>> GetUserGroupsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GroupNavigationItem>>([]);

        public Task<Result<GroupNavigationItem>> JoinGroupByCodeAsync(string joinCode, CancellationToken cancellationToken = default)
        {
            if (joinCode == "123456")
            {
                return Task.FromResult(Result<GroupNavigationItem>.Success(new GroupNavigationItem
                {
                    Id = Guid.NewGuid(),
                    Name = "Nhóm Test 123456",
                    JoinCode = joinCode
                }));
            }
            return Task.FromResult(Result<GroupNavigationItem>.Failure("Mã phòng không tồn tại."));
        }

        public Task<Result<GroupNavigationItem>> CreateGroupAsync(string name, string subject, int maxMembers, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Task.FromResult(Result<GroupNavigationItem>.Failure("Tên nhóm không được rỗng."));

            return Task.FromResult(Result<GroupNavigationItem>.Success(new GroupNavigationItem
            {
                Id = Guid.NewGuid(),
                Name = name,
                Subject = subject,
                JoinCode = "654321",
                IsHost = true
            }));
        }

        public Task<Result> LeaveGroupAsync(Guid groupId, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
        public Task<Result> DisbandGroupAsync(Guid groupId, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());

        public Task<IReadOnlyList<GroupNavigationItem>> GetExploreGroupsAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<GroupNavigationItem> list = [
                new() { Id = Guid.NewGuid(), Name = "Explore AI", Subject = "AI", JoinCode = "888111" }
            ];
            return Task.FromResult(list);
        }

        public Task PinGroupAsync(Guid groupId, bool isPinned, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MuteGroupAsync(Guid groupId, bool isMuted, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task VerifyCodeAsync_InvalidLength_SetsErrorMessage()
    {
        var service = new FakeGroupService();
        var vm = new JoinGroupDialogViewModel(service) { InputJoinCode = "123" };

        await vm.VerifyCodeAsync();

        Assert.NotNull(vm.ErrorMessage);
        Assert.Null(vm.PreviewGroup);
    }

    [Fact]
    public async Task VerifyCodeAsync_ValidCode_SetsPreviewGroup()
    {
        var service = new FakeGroupService();
        var vm = new JoinGroupDialogViewModel(service) { InputJoinCode = "123456" };

        await vm.VerifyCodeAsync();

        Assert.Null(vm.ErrorMessage);
        Assert.NotNull(vm.PreviewGroup);
        Assert.Equal("123456", vm.PreviewGroup!.JoinCode);
    }

    [Fact]
    public async Task ConfirmJoinAsync_Success_BroadcastsAndRequestsClose()
    {
        var service = new FakeGroupService();
        var messenger = new WeakReferenceMessenger();
        var vm = new JoinGroupDialogViewModel(service, messenger) { InputJoinCode = "123456" };

        bool closed = false;
        vm.RequestClose += () => closed = true;

        GroupSwitchedMessage? switchedMsg = null;
        messenger.Register<GroupSwitchedMessage>(this, (_, m) => switchedMsg = m);

        await vm.ConfirmJoinAsync();

        Assert.True(closed);
        Assert.NotNull(switchedMsg);
        Assert.Equal("123456", switchedMsg!.JoinCode);
    }

    [Fact]
    public async Task ConfirmCreateGroupAsync_ValidData_CreatesGroupAndCloses()
    {
        var service = new FakeGroupService();
        var messenger = new WeakReferenceMessenger();
        var vm = new JoinGroupDialogViewModel(service, messenger)
        {
            NewGroupName = "Nhóm C# Nâng Cao",
            NewGroupSubject = "Công nghệ phần mềm",
            NewGroupMaxMembers = 8
        };

        bool closed = false;
        vm.RequestClose += () => closed = true;

        GroupMembershipChangedMessage? changedMsg = null;
        messenger.Register<GroupMembershipChangedMessage>(this, (_, m) => changedMsg = m);

        await vm.ConfirmCreateGroupAsync();

        Assert.True(closed);
        Assert.NotNull(changedMsg);
        Assert.Equal("CREATED", changedMsg!.Action);
    }

    [Fact]
    public void SelectTab_ChangesSelectedTabAndBooleans()
    {
        var service = new FakeGroupService();
        var vm = new JoinGroupDialogViewModel(service);

        vm.SelectTab(1);
        Assert.Equal(1, vm.SelectedTabIndex);
        Assert.False(vm.IsJoinCodeTab);
        Assert.True(vm.IsExploreTab);
        Assert.False(vm.IsCreateTab);

        vm.SelectTab(2);
        Assert.Equal(2, vm.SelectedTabIndex);
        Assert.False(vm.IsJoinCodeTab);
        Assert.False(vm.IsExploreTab);
        Assert.True(vm.IsCreateTab);
    }
}
