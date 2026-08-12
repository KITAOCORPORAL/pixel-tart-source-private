using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Business;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version230Rc3BusinessCoreTests
{
    [TestMethod]
    public async Task SchemaFour_CreatesOnlyFourRc3BusinessTables()
    {
        using var setup = await DatabaseSetup.CreateAsync();
        var names = await TableNamesAsync(setup.Database);
        foreach (var expected in new[] { "BookingContacts", "BookingStaffMembers", "FinanceCategories", "FinanceTransactions" })
            CollectionAssert.Contains(names, expected);
        Assert.IsFalse(names.Any(name => name.Contains("Attachment", StringComparison.OrdinalIgnoreCase) && name != "FinanceTransactions"));
    }

    [TestMethod]
    public async Task SchemaFour_RecordsVersionFourAndPassesIntegrityCheck()
    {
        using var setup = await DatabaseSetup.CreateAsync();
        await using var connection = await setup.Database.OpenConnectionAsync();
        await using var version = connection.CreateCommand();
        version.CommandText = "SELECT MAX(Version) FROM SchemaInfo;";
        Assert.AreEqual(5L, (long)(await version.ExecuteScalarAsync())!);
        await using var integrity = connection.CreateCommand();
        integrity.CommandText = "PRAGMA integrity_check;";
        Assert.AreEqual("ok", (string)(await integrity.ExecuteScalarAsync())!);
    }

    [TestMethod]
    public async Task SchemaFour_IsIdempotent()
    {
        using var setup = await DatabaseSetup.CreateAsync();
        var result = await new DatabaseMigrator(setup.Database, new DatabaseBackupService(setup.Database, setup.Temp.Combine("repeat-backups"))).MigrateAsync();
        Assert.IsTrue(result.Success);
        Assert.AreEqual(5, result.CurrentVersion);
    }

    [TestMethod]
    public async Task SchemaFour_DefaultCategoriesContainEightIncomeAndFourteenExpense()
    {
        using var setup = await DatabaseSetup.CreateAsync();
        await using var connection = await setup.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Kind,COUNT(*) FROM FinanceCategories GROUP BY Kind ORDER BY Kind;";
        await using var reader = await command.ExecuteReaderAsync();
        var counts = new Dictionary<string, long>();
        while (await reader.ReadAsync()) counts[reader.GetString(0)] = reader.GetInt64(1);
        Assert.AreEqual(8L, counts["Income"]);
        Assert.AreEqual(14L, counts["Expense"]);
    }

    [TestMethod]
    public async Task SchemaFour_AttachmentsAreReferencesNotBinaryColumns()
    {
        using var setup = await DatabaseSetup.CreateAsync();
        await using var connection = await setup.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(FinanceTransactions);";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<(string Name, string Type)>();
        while (await reader.ReadAsync()) columns.Add((reader.GetString(1), reader.GetString(2)));
        CollectionAssert.Contains(columns.Select(item => item.Name).ToArray(), "AttachmentReferencesJson");
        Assert.IsFalse(columns.Any(item => item.Type.Contains("BLOB", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task SchemaFour_ForeignKeysRemainEnabled()
    {
        using var setup = await DatabaseSetup.CreateAsync();
        await using var connection = await setup.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys;";
        Assert.AreEqual(1L, (long)(await command.ExecuteScalarAsync())!);
    }

    [TestMethod]
    public async Task PeopleService_TrimsValuesAndPreservesOnePrimaryContact()
    {
        var repository = new PeopleRepository();
        var service = new BookingPeopleService(repository);
        var bookingId = Guid.NewGuid();
        await service.SaveAsync(bookingId,
            [new() { DisplayName = " 客户A ", Phone = " 13800000000 ", IsPrimary = true, Note = " 备注 " }],
            [new() { DisplayName = " 摄影师A ", Role = BookingStaffRole.Photographer, SortOrder = 9 }]);
        Assert.AreEqual("客户A", repository.Contacts.Single().DisplayName);
        Assert.AreEqual("13800000000", repository.Contacts.Single().Phone);
        Assert.AreEqual("备注", repository.Contacts.Single().Note);
        Assert.IsTrue(repository.Contacts.Single().IsPrimary);
    }

    [TestMethod]
    public async Task PeopleService_ReordersStaffBySubmittedOrder()
    {
        var repository = new PeopleRepository();
        var service = new BookingPeopleService(repository);
        await service.SaveAsync(Guid.NewGuid(), [], [new() { DisplayName = "B" }, new() { DisplayName = "A" }]);
        CollectionAssert.AreEqual(new[] { 0, 1 }, repository.Staff.Select(item => item.SortOrder).ToArray());
        CollectionAssert.AreEqual(new[] { "B", "A" }, repository.Staff.Select(item => item.DisplayName).ToArray());
    }

    [TestMethod] public async Task PeopleService_RejectsEmptyBookingId() =>
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => new BookingPeopleService(new PeopleRepository()).SaveAsync(Guid.Empty, [], []));

    [TestMethod] public async Task PeopleService_RejectsBlankContactName() =>
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => new BookingPeopleService(new PeopleRepository()).SaveAsync(Guid.NewGuid(), [new() { DisplayName = " " }], []));

    [TestMethod] public async Task PeopleService_RejectsBlankStaffName() =>
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => new BookingPeopleService(new PeopleRepository()).SaveAsync(Guid.NewGuid(), [], [new() { DisplayName = " " }]));

    [TestMethod] public async Task PeopleService_RejectsMultiplePrimaryContacts() =>
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => new BookingPeopleService(new PeopleRepository()).SaveAsync(Guid.NewGuid(), [new() { DisplayName = "A", IsPrimary = true }, new() { DisplayName = "B", IsPrimary = true }], []));

    [TestMethod]
    public async Task FinanceService_NormalizesCurrencyTextAndAttachmentReferences()
    {
        using var temp = new TempDirectory();
        var attachment = temp.CreateFile("凭证.txt", [1, 2, 3]);
        var repository = FinanceRepository.Create();
        var service = new FinanceService(repository);
        var result = await service.SaveAsync(Transaction(repository.Income.Id) with { CurrencyCode = " cny ", Counterparty = " 客户 ", AttachmentPaths = [attachment, attachment] });
        Assert.AreEqual("CNY", result.CurrencyCode);
        Assert.AreEqual("客户", result.Counterparty);
        Assert.AreEqual(1, result.AttachmentCount);
        Assert.AreEqual(Path.GetFullPath(attachment), result.AttachmentPaths.Single());
    }

    [TestMethod] public async Task FinanceService_RejectsZeroAmount() => await AssertFinanceFailure(Transaction(FinanceRepository.Create().Income.Id) with { AmountMinor = 0 }, typeof(ArgumentOutOfRangeException));
    [TestMethod] public async Task FinanceService_RejectsNegativeAmount() => await AssertFinanceFailure(Transaction(FinanceRepository.Create().Income.Id) with { AmountMinor = -1 }, typeof(ArgumentOutOfRangeException));
    [TestMethod] public async Task FinanceService_RejectsInvalidCurrencyScale() => await AssertFinanceFailure(Transaction(FinanceRepository.Create().Income.Id) with { CurrencyScale = 5 }, typeof(ArgumentOutOfRangeException));
    [TestMethod] public async Task FinanceService_RejectsBlankCurrency() => await AssertFinanceFailure(Transaction(FinanceRepository.Create().Income.Id) with { CurrencyCode = " " }, typeof(ArgumentException));

    [TestMethod]
    public async Task FinanceService_RejectsDirectoryAttachment()
    {
        using var temp = new TempDirectory();
        var directory = temp.Combine("folder"); Directory.CreateDirectory(directory);
        await AssertFinanceFailure(Transaction(FinanceRepository.Create().Income.Id) with { AttachmentPaths = [directory] }, typeof(ArgumentException));
    }

    [TestMethod]
    public async Task FinanceService_RejectsMissingAttachment()
    {
        using var temp = new TempDirectory();
        await AssertFinanceFailure(Transaction(FinanceRepository.Create().Income.Id) with { AttachmentPaths = [temp.Combine("missing.pdf")] }, typeof(FileNotFoundException));
    }

    [TestMethod]
    public async Task FinanceService_RejectsMismatchedCategoryKind()
    {
        var repository = FinanceRepository.Create();
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => new FinanceService(repository).SaveAsync(Transaction(repository.Expense.Id)));
    }

    [TestMethod]
    public async Task FinanceService_RejectsDisabledCategory()
    {
        var repository = FinanceRepository.Create();
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => new FinanceService(repository).SaveAsync(Transaction(repository.Disabled.Id)));
    }

    [TestMethod]
    public async Task FinanceService_RequiresExplicitDeleteConfirmation()
    {
        var service = new FinanceService(FinanceRepository.Create());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.DeleteAsync(Guid.NewGuid(), false));
    }

    [TestMethod]
    public async Task FinanceService_SummarySeparatesCashReceivableAndPayable()
    {
        var repository = FinanceRepository.Create();
        repository.Items.AddRange([
            Transaction(repository.Income.Id) with { AmountMinor = 10000, PaymentStatus = FinancePaymentStatus.Received },
            Transaction(repository.Income.Id) with { AmountMinor = 5000, PaymentStatus = FinancePaymentStatus.Receivable },
            Transaction(repository.Expense.Id, FinanceTransactionKind.Expense) with { AmountMinor = 3000, PaymentStatus = FinancePaymentStatus.Paid },
            Transaction(repository.Expense.Id, FinanceTransactionKind.Expense) with { AmountMinor = 2000, PaymentStatus = FinancePaymentStatus.Payable },
            Transaction(repository.Income.Id) with { AmountMinor = 999, PaymentStatus = FinancePaymentStatus.Cancelled }
        ]);
        var summary = await new FinanceService(repository).SummarizeAsync(new());
        Assert.AreEqual(10000, summary.IncomeMinor);
        Assert.AreEqual(3000, summary.ExpenseMinor);
        Assert.AreEqual(7000, summary.NetCashFlowMinor);
        Assert.AreEqual(5000, summary.ReceivableMinor);
        Assert.AreEqual(2000, summary.PayableMinor);
        Assert.AreEqual(10000, summary.ExpectedProfitMinor);
    }

    [TestMethod]
    public async Task FinanceCsv_UsesCreateNewChineseLabelsAndNoAttachmentPaths()
    {
        using var temp = new TempDirectory();
        var repository = FinanceRepository.Create();
        repository.Items.Add(Transaction(repository.Income.Id) with { AttachmentPaths = ["C:\\private\\client.pdf"], AttachmentCount = 1, Note = "含,逗号" });
        var output = temp.Combine("finance.csv");
        var service = new FinanceService(repository);
        await service.ExportCsvAsync(output, new());
        var content = await File.ReadAllTextAsync(output);
        StringAssert.Contains(content, "类型,分类,金额");
        StringAssert.Contains(content, "收入");
        StringAssert.Contains(content, "已收");
        Assert.IsFalse(content.Contains("client.pdf", StringComparison.Ordinal));
        await Assert.ThrowsExactlyAsync<IOException>(() => service.ExportCsvAsync(output, new()));
    }

    [TestMethod]
    [DataRow("拍摄定金")][DataRow("拍摄尾款")][DataRow("加片费用")][DataRow("修图费用")][DataRow("加急费用")][DataRow("授权费用")][DataRow("场地或道具代收")][DataRow("其他收入")]
    [DataRow("场地费")][DataRow("模特费")][DataRow("化妆师")][DataRow("造型师")][DataRow("摄影助理")][DataRow("灯光器材")][DataRow("道具服装")][DataRow("交通")][DataRow("餐饮")][DataRow("住宿")][DataRow("快递打印")][DataRow("平台抽成")][DataRow("退款或赔付")][DataRow("其他支出")]
    public void SchemaFour_DeclaresRequiredDefaultCategory(string category) => StringAssert.Contains(Source("src/RAWSelectionAssistant.Core/Services/Database/BusinessSchemaMigration.cs"), $"\"{category}\"");

    [TestMethod]
    [DataRow(".exe")][DataRow(".com")][DataRow(".bat")][DataRow(".cmd")][DataRow(".ps1")][DataRow(".vbs")][DataRow(".js")][DataRow(".jse")][DataRow(".msi")][DataRow(".msp")][DataRow(".dll")][DataRow(".scr")][DataRow(".lnk")]
    public void DocumentSafety_BlocksExecutableOrScriptExtension(string extension) => StringAssert.Contains(Source("src/RAWSelectionAssistant.Core/Services/Bookings/BookingDocumentFileSafety.cs"), $"\"{extension}\"");

    private static async Task AssertFinanceFailure(FinanceTransaction transaction, Type exceptionType)
    {
        var repository = FinanceRepository.Create();
        transaction = transaction with { CategoryId = transaction.Kind == FinanceTransactionKind.Income ? repository.Income.Id : repository.Expense.Id };
        Exception? exception = null;
        try { await new FinanceService(repository).SaveAsync(transaction); }
        catch (Exception caught) { exception = caught; }
        Assert.IsNotNull(exception);
        Assert.AreEqual(exceptionType, exception.GetType());
    }

    private static FinanceTransaction Transaction(Guid categoryId, FinanceTransactionKind kind = FinanceTransactionKind.Income) => new()
    {
        Kind = kind, CategoryId = categoryId, AmountMinor = 100, PaymentStatus = kind == FinanceTransactionKind.Income ? FinancePaymentStatus.Received : FinancePaymentStatus.Paid
    };

    private static async Task<string[]> TableNamesAsync(PixelTartDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<string>(); while (await reader.ReadAsync()) result.Add(reader.GetString(0)); return result.ToArray();
    }

    private sealed class PeopleRepository : IBookingPeopleRepository
    {
        public IReadOnlyList<BookingContact> Contacts { get; private set; } = [];
        public IReadOnlyList<BookingStaffMember> Staff { get; private set; } = [];
        public Task<IReadOnlyList<BookingContact>> ListContactsAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult(Contacts);
        public Task<IReadOnlyList<BookingStaffMember>> ListStaffAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult(Staff);
        public Task ReplaceAsync(Guid bookingId, IReadOnlyList<BookingContact> contacts, IReadOnlyList<BookingStaffMember> staff, CancellationToken cancellationToken = default) { Contacts = contacts; Staff = staff; return Task.CompletedTask; }
    }

    private sealed class FinanceRepository : IFinanceRepository
    {
        public FinanceCategory Income { get; } = new() { Kind = FinanceTransactionKind.Income, Name = "拍摄定金" };
        public FinanceCategory Expense { get; } = new() { Kind = FinanceTransactionKind.Expense, Name = "场地费" };
        public FinanceCategory Disabled { get; } = new() { Kind = FinanceTransactionKind.Income, Name = "停用", IsDisabled = true };
        public List<FinanceTransaction> Items { get; } = [];
        public static FinanceRepository Create() => new();
        public Task<IReadOnlyList<FinanceCategory>> ListCategoriesAsync(bool includeDisabled = false, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FinanceCategory>>([Income, Expense, Disabled]);
        public Task<FinanceTransaction?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(item => item.Id == id));
        public Task SaveAsync(FinanceTransaction transaction, CancellationToken cancellationToken = default) { Items.RemoveAll(item => item.Id == transaction.Id); Items.Add(transaction); return Task.CompletedTask; }
        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.RemoveAll(item => item.Id == id) > 0);
        public Task<IReadOnlyList<FinanceTransaction>> QueryAsync(FinanceQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FinanceTransaction>>(Items.ToArray());
    }

    private sealed class DatabaseSetup(TempDirectory temp, PixelTartDatabase database) : IDisposable
    {
        public TempDirectory Temp { get; } = temp;
        public PixelTartDatabase Database { get; } = database;
        public static async Task<DatabaseSetup> CreateAsync()
        {
            var temp = new TempDirectory(); var database = new PixelTartDatabase(temp.Combine("data", "rc3.db"));
            Assert.IsTrue((await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync()).Success);
            return new(temp, database);
        }
        public void Dispose() { SqliteTestIsolation.ClearPool(Database); Temp.Dispose(); }
    }

    private static string Source(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName; throw new DirectoryNotFoundException(); }
}
