using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.Database;

public sealed class BusinessSchemaMigration : IMigration
{
    public int Version => 4;
    public string Name => "BookingPeopleAndFinanceMvp";

    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var statements = new[]
        {
            """
            CREATE TABLE IF NOT EXISTS BookingContacts (
                Id TEXT NOT NULL PRIMARY KEY,
                BookingId TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Phone TEXT NULL,
                WeChat TEXT NULL,
                Email TEXT NULL,
                OtherContact TEXT NULL,
                IsPrimary INTEGER NOT NULL DEFAULT 0 CHECK(IsPrimary IN (0,1)),
                Note TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY(BookingId) REFERENCES ShootBookings(Id) ON DELETE RESTRICT
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS BookingStaffMembers (
                Id TEXT NOT NULL PRIMARY KEY,
                BookingId TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Role TEXT NOT NULL,
                ArrivalTime TEXT NULL,
                Phone TEXT NULL,
                WeChat TEXT NULL,
                Email TEXT NULL,
                Note TEXT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY(BookingId) REFERENCES ShootBookings(Id) ON DELETE RESTRICT
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS FinanceCategories (
                Id TEXT NOT NULL PRIMARY KEY,
                Kind TEXT NOT NULL CHECK(Kind IN ('Income','Expense')),
                Name TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                IsSystemDefault INTEGER NOT NULL DEFAULT 0 CHECK(IsSystemDefault IN (0,1)),
                IsDisabled INTEGER NOT NULL DEFAULT 0 CHECK(IsDisabled IN (0,1)),
                UNIQUE(Kind,Name)
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS FinanceTransactions (
                Id TEXT NOT NULL PRIMARY KEY,
                Kind TEXT NOT NULL CHECK(Kind IN ('Income','Expense')),
                CategoryId TEXT NOT NULL,
                AmountMinor INTEGER NOT NULL CHECK(AmountMinor >= 0),
                CurrencyCode TEXT NOT NULL DEFAULT 'CNY',
                CurrencyScale INTEGER NOT NULL DEFAULT 2 CHECK(CurrencyScale BETWEEN 0 AND 4),
                OccurredOn TEXT NOT NULL,
                PaymentStatus TEXT NOT NULL CHECK(PaymentStatus IN ('Expected','Receivable','Received','Payable','Paid','Cancelled')),
                BookingId TEXT NULL,
                ProjectId TEXT NULL,
                Counterparty TEXT NULL,
                PaymentMethod TEXT NULL,
                Note TEXT NULL,
                AttachmentCount INTEGER NOT NULL DEFAULT 0 CHECK(AttachmentCount >= 0),
                AttachmentReferencesJson TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY(CategoryId) REFERENCES FinanceCategories(Id) ON DELETE RESTRICT,
                FOREIGN KEY(BookingId) REFERENCES ShootBookings(Id) ON DELETE SET NULL,
                FOREIGN KEY(ProjectId) REFERENCES Projects(Id) ON DELETE SET NULL
            );
            """,
            "CREATE INDEX IF NOT EXISTS IX_BookingContacts_BookingId_Primary ON BookingContacts(BookingId,IsPrimary DESC,UpdatedAtUtc);",
            "CREATE INDEX IF NOT EXISTS IX_BookingStaffMembers_BookingId_SortOrder ON BookingStaffMembers(BookingId,SortOrder);",
            "CREATE INDEX IF NOT EXISTS IX_FinanceCategories_Kind_SortOrder ON FinanceCategories(Kind,IsDisabled,SortOrder);",
            "CREATE INDEX IF NOT EXISTS IX_FinanceTransactions_OccurredOn_Kind ON FinanceTransactions(OccurredOn DESC,Kind);",
            "CREATE INDEX IF NOT EXISTS IX_FinanceTransactions_BookingId ON FinanceTransactions(BookingId,OccurredOn DESC);",
            "CREATE INDEX IF NOT EXISTS IX_FinanceTransactions_ProjectId ON FinanceTransactions(ProjectId,OccurredOn DESC);"
        };

        foreach (var statement in statements)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var income = new[] { "拍摄定金", "拍摄尾款", "加片费用", "修图费用", "加急费用", "授权费用", "场地或道具代收", "其他收入" };
        var expense = new[] { "场地费", "模特费", "化妆师", "造型师", "摄影助理", "灯光器材", "道具服装", "交通", "餐饮", "住宿", "快递打印", "平台抽成", "退款或赔付", "其他支出" };
        var sequence = 1;
        foreach (var item in income.Select(name => (Kind: "Income", Name: name)).Concat(expense.Select(name => (Kind: "Expense", Name: name))))
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT OR IGNORE INTO FinanceCategories(Id,Kind,Name,SortOrder,IsSystemDefault,IsDisabled) VALUES($id,$kind,$name,$sort,1,0);";
            insert.Parameters.AddWithValue("$id", $"00000000-0000-4000-8000-{sequence:000000000000}");
            insert.Parameters.AddWithValue("$kind", item.Kind);
            insert.Parameters.AddWithValue("$name", item.Name);
            insert.Parameters.AddWithValue("$sort", sequence);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            sequence++;
        }
    }
}
