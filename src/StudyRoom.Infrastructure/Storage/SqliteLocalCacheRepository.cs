using Microsoft.Data.Sqlite;
using StudyRoom.Application.Interfaces;
using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;

namespace StudyRoom.Infrastructure.Storage;

public class SqliteLocalCacheRepository : ILocalCacheRepository
{
    private readonly string _connectionString;

    public SqliteLocalCacheRepository(string? connectionStringOrDbPath = null)
    {
        if (string.IsNullOrWhiteSpace(connectionStringOrDbPath))
        {
            string dbDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StudyRoom");
            Directory.CreateDirectory(dbDir);
            string dbPath = Path.Combine(dbDir, "studyroom_local.db");
            _connectionString = $"Data Source={dbPath}";
        }
        else if (connectionStringOrDbPath.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            _connectionString = connectionStringOrDbPath;
        }
        else
        {
            string? dir = Path.GetDirectoryName(connectionStringOrDbPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _connectionString = $"Data Source={connectionStringOrDbPath}";
        }
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string schemaSql = """
            CREATE TABLE IF NOT EXISTS UserSettings (
                Key TEXT PRIMARY KEY NOT NULL,
                Value TEXT NOT NULL,
                LastUpdatedUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS CachedKanbanCards (
                CardId TEXT PRIMARY KEY NOT NULL,
                GroupId TEXT NOT NULL,
                ColumnId TEXT NOT NULL CHECK(ColumnId IN ('Todo', 'InProgress', 'Done')),
                Title TEXT NOT NULL,
                Description TEXT,
                Rank REAL NOT NULL,
                AssigneeId TEXT,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_CachedKanbanCards_GroupId_ColumnId 
            ON CachedKanbanCards (GroupId, ColumnId, Rank ASC);

            CREATE TABLE IF NOT EXISTS CachedFlashcards (
                CardId TEXT PRIMARY KEY NOT NULL,
                GroupId TEXT NOT NULL,
                Question TEXT NOT NULL,
                Answer TEXT NOT NULL,
                LastReviewedUtc TEXT
            );
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = schemaSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveSettingAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO UserSettings (Key, Value, LastUpdatedUtc)
            VALUES (@key, @value, @lastUpdated)
            ON CONFLICT(Key) DO UPDATE SET
                Value = excluded.Value,
                LastUpdatedUtc = excluded.LastUpdatedUtc;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        command.Parameters.AddWithValue("@lastUpdated", DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT Value FROM UserSettings WHERE Key = @key LIMIT 1;";
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@key", key);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result?.ToString();
    }

    public async Task CacheCardsAsync(string groupId, IEnumerable<KanbanCard> cards, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string deleteSql = "DELETE FROM CachedKanbanCards WHERE GroupId = @groupId;";
        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = (SqliteTransaction)transaction;
            deleteCommand.CommandText = deleteSql;
            deleteCommand.Parameters.AddWithValue("@groupId", groupId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = """
            INSERT INTO CachedKanbanCards (CardId, GroupId, ColumnId, Title, Description, Rank, AssigneeId, UpdatedAtUtc)
            VALUES (@cardId, @groupId, @columnId, @title, @description, @rank, @assigneeId, @updatedAtUtc);
            """;

        foreach (var card in cards)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = (SqliteTransaction)transaction;
            insertCommand.CommandText = insertSql;
            insertCommand.Parameters.AddWithValue("@cardId", card.Id.ToString("N"));
            insertCommand.Parameters.AddWithValue("@groupId", groupId);
            insertCommand.Parameters.AddWithValue("@columnId", card.Column.ToString());
            insertCommand.Parameters.AddWithValue("@title", card.Title);
            insertCommand.Parameters.AddWithValue("@description", (object?)card.Description ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("@rank", card.Rank);
            insertCommand.Parameters.AddWithValue("@assigneeId", (object?)card.AssignedUserId?.ToString("N") ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("@updatedAtUtc", card.UpdatedAt.ToString("O"));

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KanbanCard>> GetCachedCardsAsync(string groupId, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT CardId, GroupId, ColumnId, Title, Description, Rank, AssigneeId, UpdatedAtUtc
            FROM CachedKanbanCards
            WHERE GroupId = @groupId
            ORDER BY 
                CASE ColumnId 
                    WHEN 'Todo' THEN 0 
                    WHEN 'InProgress' THEN 1 
                    WHEN 'Done' THEN 2 
                    ELSE 3 
                END, 
                Rank ASC;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@groupId", groupId);

        var list = new List<KanbanCard>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string cardIdStr = reader.GetString(0);
            string groupIdStr = reader.GetString(1);
            string colStr = reader.GetString(2);
            string title = reader.GetString(3);
            string? desc = reader.IsDBNull(4) ? null : reader.GetString(4);
            double rank = reader.GetDouble(5);
            string? assigneeStr = reader.IsDBNull(6) ? null : reader.GetString(6);
            string updatedAtStr = reader.GetString(7);

            var col = Enum.TryParse<KanbanColumnType>(colStr, out var parsedCol) ? parsedCol : KanbanColumnType.Todo;
            var cardId = Guid.TryParse(cardIdStr, out var parsedCardId) ? parsedCardId : Guid.NewGuid();
            var grpId = Guid.TryParse(groupIdStr, out var parsedGrpId) ? parsedGrpId : Guid.Empty;
            Guid? assignee = Guid.TryParse(assigneeStr, out var parsedAssignee) ? parsedAssignee : null;
            DateTimeOffset updatedAt = DateTimeOffset.TryParse(updatedAtStr, out var parsedDate) ? parsedDate : DateTimeOffset.UtcNow;

            list.Add(new KanbanCard
            {
                Id = cardId,
                GroupId = grpId,
                Column = col,
                Title = title,
                Description = desc,
                Rank = rank,
                AssignedUserId = assignee,
                UpdatedAt = updatedAt
            });
        }

        return list;
    }

    public async Task CacheFlashcardsAsync(string groupId, IEnumerable<Flashcard> flashcards, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string deleteSql = "DELETE FROM CachedFlashcards WHERE GroupId = @groupId;";
        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = (SqliteTransaction)transaction;
            deleteCommand.CommandText = deleteSql;
            deleteCommand.Parameters.AddWithValue("@groupId", groupId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = """
            INSERT INTO CachedFlashcards (CardId, GroupId, Question, Answer, LastReviewedUtc)
            VALUES (@cardId, @groupId, @question, @answer, @lastReviewedUtc);
            """;

        foreach (var card in flashcards)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = (SqliteTransaction)transaction;
            insertCommand.CommandText = insertSql;
            insertCommand.Parameters.AddWithValue("@cardId", card.Id.ToString("N"));
            insertCommand.Parameters.AddWithValue("@groupId", groupId);
            insertCommand.Parameters.AddWithValue("@question", card.Question);
            insertCommand.Parameters.AddWithValue("@answer", card.Answer);
            insertCommand.Parameters.AddWithValue("@lastReviewedUtc", (object?)card.LastReviewedAt?.ToString("O") ?? DBNull.Value);

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Flashcard>> GetCachedFlashcardsAsync(string groupId, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT CardId, GroupId, Question, Answer, LastReviewedUtc
            FROM CachedFlashcards
            WHERE GroupId = @groupId;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@groupId", groupId);

        var list = new List<Flashcard>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string cardIdStr = reader.GetString(0);
            string groupIdStr = reader.GetString(1);
            string question = reader.GetString(2);
            string answer = reader.GetString(3);
            string? lastReviewedStr = reader.IsDBNull(4) ? null : reader.GetString(4);

            var cardId = Guid.TryParse(cardIdStr, out var parsedCardId) ? parsedCardId : Guid.NewGuid();
            var grpId = Guid.TryParse(groupIdStr, out var parsedGrpId) ? parsedGrpId : Guid.Empty;
            DateTimeOffset? lastReviewed = DateTimeOffset.TryParse(lastReviewedStr, out var parsedDate) ? parsedDate : null;

            list.Add(new Flashcard
            {
                Id = cardId,
                GroupId = grpId,
                Question = question,
                Answer = answer,
                LastReviewedAt = lastReviewed
            });
        }

        return list;
    }
}
