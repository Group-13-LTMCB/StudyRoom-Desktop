using StudyRoom.Application.Interfaces;
using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;
using StudyRoom.Core.Interfaces;

namespace StudyRoom.Application.Services;

public class KanbanService : IKanbanService
{
    private readonly IFractionalIndexingHelper _indexingHelper;
    private readonly ILocalCacheRepository? _cacheRepository;
    private readonly Dictionary<string, List<KanbanCard>> _cardStore = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _isInitialized;

    public KanbanService(
        IFractionalIndexingHelper indexingHelper,
        ILocalCacheRepository? cacheRepository = null)
    {
        _indexingHelper = indexingHelper ?? throw new ArgumentNullException(nameof(indexingHelper));
        _cacheRepository = cacheRepository;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized) return;
        if (_cacheRepository != null)
        {
            await _cacheRepository.InitializeAsync(cancellationToken);
        }
        _isInitialized = true;
    }

    public async Task<IReadOnlyList<KanbanCard>> GetCardsAsync(string groupId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedAsync(cancellationToken);

            if (_cacheRepository != null)
            {
                var cached = await _cacheRepository.GetCachedCardsAsync(groupId, cancellationToken);
                if (cached != null && cached.Count > 0)
                {
                    _cardStore[groupId] = cached.OrderBy(c => c.Column).ThenBy(c => c.Rank).ToList();
                    return _cardStore[groupId].AsReadOnly();
                }
            }

            if (!_cardStore.TryGetValue(groupId, out var list) || list.Count == 0)
            {
                list = CreateDefaultCards(groupId);
                _cardStore[groupId] = list;

                if (_cacheRepository != null)
                {
                    await _cacheRepository.CacheCardsAsync(groupId, list, cancellationToken);
                }
            }

            return list.OrderBy(c => c.Column).ThenBy(c => c.Rank).ToList().AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<KanbanCard> AddCardAsync(
        string groupId,
        string title,
        string? description,
        KanbanColumnType column,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedAsync(cancellationToken);

            if (!_cardStore.TryGetValue(groupId, out var list))
            {
                if (_cacheRepository != null)
                {
                    var cached = await _cacheRepository.GetCachedCardsAsync(groupId, cancellationToken);
                    list = (cached != null && cached.Count > 0) ? cached.ToList() : CreateDefaultCards(groupId);
                }
                else
                {
                    list = CreateDefaultCards(groupId);
                }
                _cardStore[groupId] = list;
            }

            var colCards = list.Where(c => c.Column == column).OrderBy(c => c.Rank).ToList();
            double? prevRank = colCards.Count > 0 ? colCards[^1].Rank : null;
            double newRank = _indexingHelper.CalculateNewRank(prevRank, null);

            Guid groupGuid = Guid.TryParse(groupId, out var parsedGrp) ? parsedGrp : Guid.NewGuid();

            var newCard = new KanbanCard
            {
                Id = Guid.NewGuid(),
                GroupId = groupGuid,
                Title = title,
                Description = description,
                Column = column,
                Rank = newRank,
                OrderIndex = colCards.Count,
                Version = 1,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            list.Add(newCard);

            if (_cacheRepository != null)
            {
                await _cacheRepository.CacheCardsAsync(groupId, list, cancellationToken);
            }

            return newCard;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<KanbanCard> MoveCardAsync(
        Guid cardId,
        KanbanColumnType targetColumn,
        double? prevRank,
        double? nextRank,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedAsync(cancellationToken);

            string? foundGroupId = null;
            KanbanCard? card = null;

            foreach (var (grpId, cards) in _cardStore)
            {
                var candidate = cards.FirstOrDefault(c => c.Id == cardId);
                if (candidate != null)
                {
                    foundGroupId = grpId;
                    card = candidate;
                    break;
                }
            }

            if (card == null)
            {
                throw new KeyNotFoundException($"Thẻ Kanban với ID {cardId} không tìm thấy.");
            }

            double newRank = _indexingHelper.CalculateNewRank(prevRank, nextRank);
            card.Column = targetColumn;
            card.Rank = newRank;
            card.UpdatedAt = DateTimeOffset.UtcNow;

            var groupCards = _cardStore[foundGroupId!];
            var colCards = groupCards.Where(c => c.Column == targetColumn).OrderBy(c => c.Rank).ToList();
            var colRanks = colCards.Select(c => c.Rank);

            if (_indexingHelper.NeedsRebalance(colRanks))
            {
                var balancedRanks = _indexingHelper.Rebalance(colCards.Count);
                for (int i = 0; i < colCards.Count; i++)
                {
                    colCards[i].Rank = balancedRanks[i];
                    colCards[i].UpdatedAt = DateTimeOffset.UtcNow;
                }
            }

            if (_cacheRepository != null)
            {
                await _cacheRepository.CacheCardsAsync(foundGroupId!, groupCards, cancellationToken);
            }

            return card;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteCardAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedAsync(cancellationToken);

            string? foundGroupId = null;
            KanbanCard? card = null;

            foreach (var (grpId, cards) in _cardStore)
            {
                var candidate = cards.FirstOrDefault(c => c.Id == cardId);
                if (candidate != null)
                {
                    foundGroupId = grpId;
                    card = candidate;
                    break;
                }
            }

            if (card != null && foundGroupId != null)
            {
                _cardStore[foundGroupId].Remove(card);

                if (_cacheRepository != null)
                {
                    await _cacheRepository.CacheCardsAsync(foundGroupId, _cardStore[foundGroupId], cancellationToken);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static List<KanbanCard> CreateDefaultCards(string groupId)
    {
        Guid groupGuid = Guid.TryParse(groupId, out var parsedGrp) ? parsedGrp : Guid.NewGuid();

        return
        [
            new KanbanCard
            {
                Id = Guid.NewGuid(),
                GroupId = groupGuid,
                Title = "Bài tập Giải tích III - Đạo hàm riêng",
                Description = "Hoàn thành bài tập 1.1 đến 1.5 chương Giải tích hàm nhiều biến",
                Column = KanbanColumnType.Todo,
                Rank = 1.0,
                OrderIndex = 0,
                Version = 1,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new KanbanCard
            {
                Id = Guid.NewGuid(),
                GroupId = groupGuid,
                Title = "Ôn thi Tiếng Anh - Reading Unit 4",
                Description = "Luyện đọc hiểu dạng Matching Headings và tóm tắt đoạn văn",
                Column = KanbanColumnType.Todo,
                Rank = 2.0,
                OrderIndex = 1,
                Version = 1,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new KanbanCard
            {
                Id = Guid.NewGuid(),
                GroupId = groupGuid,
                Title = "Báo cáo Vật lý đại cương II",
                Description = "Xử lý số liệu thí nghiệm đo điện trở và viết báo cáo kết quả",
                Column = KanbanColumnType.InProgress,
                Rank = 1.0,
                OrderIndex = 0,
                Version = 1,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new KanbanCard
            {
                Id = Guid.NewGuid(),
                GroupId = groupGuid,
                Title = "Cấu trúc dữ liệu - Cây AVL & Red-Black",
                Description = "Cài đặt thuật toán xoay cây và kiểm thử thời gian chạy",
                Column = KanbanColumnType.InProgress,
                Rank = 2.0,
                OrderIndex = 1,
                Version = 1,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new KanbanCard
            {
                Id = Guid.NewGuid(),
                GroupId = groupGuid,
                Title = "Đồ án Cơ sở dữ liệu: Thiết kế ERD",
                Description = "Chuẩn hóa quan hệ dạng 3NF và xuất script SQL tạo bảng",
                Column = KanbanColumnType.Done,
                Rank = 1.0,
                OrderIndex = 0,
                Version = 1,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ];
    }
}
