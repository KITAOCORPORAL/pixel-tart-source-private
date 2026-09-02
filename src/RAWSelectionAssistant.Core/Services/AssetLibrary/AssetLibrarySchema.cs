using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using System.Text.Json;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

internal static class AssetLibrarySchema
{
    public const int Version = 7;

    public static async Task EnsureAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await RejectFutureVersionAsync(connection, cancellationToken).ConfigureAwait(false);
        var statements = new[]
        {
            "CREATE TABLE IF NOT EXISTS AssetLibrarySchemaInfo(Version INTEGER NOT NULL PRIMARY KEY, AppliedAt TEXT NOT NULL);",
            """
            CREATE TABLE IF NOT EXISTS AssetItems(
                AssetId TEXT NOT NULL PRIMARY KEY,
                SourcePath TEXT NOT NULL,
                NormalizedSourcePath TEXT NOT NULL,
                DuplicateDiscriminator TEXT NOT NULL DEFAULT '',
                DisplayName TEXT NOT NULL,
                Extension TEXT NOT NULL,
                MediaType TEXT NOT NULL,
                FileSize INTEGER NOT NULL DEFAULT 0 CHECK(FileSize >= 0),
                ContentHash TEXT NULL,
                Width INTEGER NULL,
                Height INTEGER NULL,
                Orientation TEXT NULL,
                CaptureTime TEXT NULL,
                AddedAt TEXT NOT NULL,
                ModifiedAt TEXT NOT NULL,
                Rating INTEGER NOT NULL DEFAULT 0 CHECK(Rating BETWEEN 0 AND 5),
                Comment TEXT NOT NULL DEFAULT '',
                IsMissing INTEGER NOT NULL DEFAULT 0 CHECK(IsMissing IN(0,1)),
                IsArchived INTEGER NOT NULL DEFAULT 0 CHECK(IsArchived IN(0,1)),
                ImportMode TEXT NOT NULL DEFAULT 'Reference',
                ManagedCopyPath TEXT NULL,
                UNIQUE(NormalizedSourcePath,DuplicateDiscriminator)
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AssetFolders(
                FolderId TEXT NOT NULL PRIMARY KEY,
                ParentFolderId TEXT NULL,
                Name TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                Icon TEXT NULL,
                Color TEXT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                IsArchived INTEGER NOT NULL DEFAULT 0 CHECK(IsArchived IN(0,1)),
                IsSystem INTEGER NOT NULL DEFAULT 0 CHECK(IsSystem IN(0,1)),
                AutoTagIdsJson TEXT NOT NULL DEFAULT '[]',
                FOREIGN KEY(ParentFolderId) REFERENCES AssetFolders(FolderId) ON DELETE SET NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AssetFolderMemberships(
                AssetId TEXT NOT NULL,
                FolderId TEXT NOT NULL,
                AddedAt TEXT NOT NULL,
                PRIMARY KEY(AssetId,FolderId),
                FOREIGN KEY(AssetId) REFERENCES AssetItems(AssetId) ON DELETE CASCADE,
                FOREIGN KEY(FolderId) REFERENCES AssetFolders(FolderId) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AssetFolderAutoTags(
                FolderId TEXT NOT NULL,
                TagId TEXT NOT NULL,
                PRIMARY KEY(FolderId,TagId),
                FOREIGN KEY(FolderId) REFERENCES AssetFolders(FolderId) ON DELETE CASCADE,
                FOREIGN KEY(TagId) REFERENCES AssetTags(TagId) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS TagGroups(
                TagGroupId TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                IsArchived INTEGER NOT NULL DEFAULT 0 CHECK(IsArchived IN(0,1))
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AssetTags(
                TagId TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                TagGroupId TEXT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                UsageCount INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                IsArchived INTEGER NOT NULL DEFAULT 0 CHECK(IsArchived IN(0,1)),
                UNIQUE(TagGroupId,Name),
                FOREIGN KEY(TagGroupId) REFERENCES TagGroups(TagGroupId) ON DELETE SET NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AssetTagMemberships(
                AssetId TEXT NOT NULL,
                TagId TEXT NOT NULL,
                AddedAt TEXT NOT NULL,
                PRIMARY KEY(AssetId,TagId),
                FOREIGN KEY(AssetId) REFERENCES AssetItems(AssetId) ON DELETE CASCADE,
                FOREIGN KEY(TagId) REFERENCES AssetTags(TagId) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS SmartFolders(
                SmartFolderId TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE,
                Logic TEXT NOT NULL DEFAULT 'And',
                Description TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                IsArchived INTEGER NOT NULL DEFAULT 0 CHECK(IsArchived IN(0,1))
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS SmartFolderRules(
                RuleId TEXT NOT NULL PRIMARY KEY,
                SmartFolderId TEXT NOT NULL,
                Field TEXT NOT NULL,
                Operator TEXT NOT NULL,
                Value TEXT NOT NULL DEFAULT '',
                Negated INTEGER NOT NULL DEFAULT 0 CHECK(Negated IN(0,1)),
                SortOrder INTEGER NOT NULL DEFAULT 0,
                GroupId TEXT NULL,
                GroupLogic TEXT NOT NULL DEFAULT 'And',
                FOREIGN KEY(SmartFolderId) REFERENCES SmartFolders(SmartFolderId) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AssetLibraryUndoJournal(
                OperationId TEXT NOT NULL PRIMARY KEY,
                Description TEXT NOT NULL,
                OperationKind TEXT NOT NULL,
                PayloadJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UndoneAt TEXT NULL,
                JournalVersion INTEGER NOT NULL DEFAULT 1
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AssetVisualAnalysis(
                AssetId TEXT NOT NULL,
                AnalysisVersion TEXT NOT NULL,
                ContentHash TEXT NOT NULL,
                PaletteSize INTEGER NOT NULL DEFAULT 5,
                PaletteSort TEXT NOT NULL DEFAULT 'Weight',
                AnalysisSource TEXT NOT NULL,
                SourceProfile TEXT NOT NULL,
                AnalysisProfile TEXT NOT NULL,
                ResultJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                PRIMARY KEY(AssetId,AnalysisVersion,PaletteSize,PaletteSort),
                FOREIGN KEY(AssetId) REFERENCES AssetItems(AssetId) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AssetVisualFeatures(
                AssetId TEXT NOT NULL,
                AnalysisVersion TEXT NOT NULL,
                PaletteSize INTEGER NOT NULL CHECK(PaletteSize=5),
                PaletteSort TEXT NOT NULL CHECK(PaletteSort='Weight'),
                ContentFingerprint TEXT NOT NULL,
                SourceContentHash TEXT NULL,
                Outcome TEXT NOT NULL,
                FailureReason TEXT NULL,
                AnalysisSource TEXT NOT NULL,
                SourceProfile TEXT NOT NULL,
                AnalysisProfile TEXT NOT NULL,
                Harmony TEXT NULL,
                ToneKey TEXT NULL,
                Contrast TEXT NULL,
                LuminanceSpan TEXT NULL,
                Saturation TEXT NULL,
                WarmCool TEXT NULL,
                DominantHue REAL NULL,
                SecondaryHue REAL NULL,
                AverageHue REAL NULL,
                AverageLuma REAL NULL,
                MedianLuma REAL NULL,
                ContrastMetric REAL NULL,
                LumaSpreadMetric REAL NULL,
                AverageSaturation REAL NULL,
                MedianSaturation REAL NULL,
                AverageLightness REAL NULL,
                WarmCoolMetric REAL NULL,
                DeepShadowRatio REAL NULL,
                ShadowRatio REAL NULL,
                MidtoneRatio REAL NULL,
                HighlightRatio REAL NULL,
                SpecularRatio REAL NULL,
                BlackClipRatio REAL NULL,
                WhiteClipRatio REAL NULL,
                HistogramLumaSignature TEXT NULL,
                PaletteSignature TEXT NULL,
                ResultJson TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                PRIMARY KEY(AssetId,AnalysisVersion),
                FOREIGN KEY(AssetId) REFERENCES AssetItems(AssetId) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AssetVisualPaletteColors(
                AssetId TEXT NOT NULL,
                AnalysisVersion TEXT NOT NULL,
                ColorIndex INTEGER NOT NULL,
                Red INTEGER NOT NULL CHECK(Red BETWEEN 0 AND 255),
                Green INTEGER NOT NULL CHECK(Green BETWEEN 0 AND 255),
                Blue INTEGER NOT NULL CHECK(Blue BETWEEN 0 AND 255),
                LabL REAL NOT NULL,
                LabA REAL NOT NULL,
                LabB REAL NOT NULL,
                Hue REAL NOT NULL,
                Saturation REAL NOT NULL,
                Chroma REAL NOT NULL,
                Weight REAL NOT NULL CHECK(Weight >= 0 AND Weight <= 1),
                Hex TEXT NOT NULL,
                PRIMARY KEY(AssetId,AnalysisVersion,ColorIndex),
                FOREIGN KEY(AssetId,AnalysisVersion)
                    REFERENCES AssetVisualFeatures(AssetId,AnalysisVersion) ON DELETE CASCADE
            );
            """,
            "CREATE INDEX IF NOT EXISTS IX_AssetItems_DisplayName ON AssetItems(DisplayName COLLATE NOCASE);",
            "CREATE INDEX IF NOT EXISTS IX_AssetItems_AddedAt ON AssetItems(AddedAt DESC);",
            "CREATE INDEX IF NOT EXISTS IX_AssetItems_CaptureTime ON AssetItems(CaptureTime DESC);",
            "CREATE INDEX IF NOT EXISTS IX_AssetItems_Rating ON AssetItems(Rating);",
            "CREATE INDEX IF NOT EXISTS IX_AssetItems_ContentHash ON AssetItems(ContentHash) WHERE ContentHash IS NOT NULL;",
            "CREATE INDEX IF NOT EXISTS IX_AssetItems_MissingName ON AssetItems(IsMissing,DisplayName COLLATE NOCASE);",
            "CREATE INDEX IF NOT EXISTS IX_AssetFolderMemberships_Folder ON AssetFolderMemberships(FolderId,AssetId);",
            "CREATE INDEX IF NOT EXISTS IX_AssetFolderAutoTags_Tag ON AssetFolderAutoTags(TagId,FolderId);",
            "CREATE INDEX IF NOT EXISTS IX_AssetTagMemberships_Tag ON AssetTagMemberships(TagId,AssetId);",
            "CREATE INDEX IF NOT EXISTS IX_AssetLibraryUndoJournal_Recent ON AssetLibraryUndoJournal(UndoneAt,CreatedAt DESC);",
            "CREATE INDEX IF NOT EXISTS IX_AssetVisualFeatures_Outcome ON AssetVisualFeatures(AnalysisVersion,Outcome,AssetId);",
            "CREATE INDEX IF NOT EXISTS IX_AssetVisualFeatures_Hue ON AssetVisualFeatures(AnalysisVersion,Outcome,DominantHue,AssetId);",
            "CREATE INDEX IF NOT EXISTS IX_AssetVisualFeatures_Luma ON AssetVisualFeatures(AnalysisVersion,Outcome,AverageLuma,AssetId);",
            "CREATE INDEX IF NOT EXISTS IX_AssetVisualFeatures_Saturation ON AssetVisualFeatures(AnalysisVersion,Outcome,AverageSaturation,AssetId);",
            "CREATE INDEX IF NOT EXISTS IX_AssetVisualFeatures_Contrast ON AssetVisualFeatures(AnalysisVersion,Outcome,ContrastMetric,AssetId);",
            "CREATE INDEX IF NOT EXISTS IX_AssetVisualFeatures_WarmCool ON AssetVisualFeatures(AnalysisVersion,Outcome,WarmCoolMetric,AssetId);",
            "CREATE INDEX IF NOT EXISTS IX_AssetVisualFeatures_Classifications ON AssetVisualFeatures(AnalysisVersion,Outcome,ToneKey,Contrast,Saturation,WarmCool,AssetId);",
            "CREATE INDEX IF NOT EXISTS IX_AssetVisualPaletteColors_HueWeight ON AssetVisualPaletteColors(AnalysisVersion,Hue,Weight,AssetId);",
            "CREATE INDEX IF NOT EXISTS IX_AssetVisualPaletteColors_MaterialHue ON AssetVisualPaletteColors(AnalysisVersion,Hue,AssetId) WHERE Weight>=0.15 AND Saturation>=0.08 AND Chroma>=8;",
            "CREATE INDEX IF NOT EXISTS IX_AssetVisualPaletteColors_LabWeight ON AssetVisualPaletteColors(AnalysisVersion,LabL,LabA,LabB,Weight,AssetId);"
        };

        foreach (var statement in statements)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await EnsureAssetIdentitySchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureAssetIndexesAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureVisualAnalysisCacheSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        await EnsureColumnAsync(connection, "SmartFolderRules", "GroupId", "TEXT NULL", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "SmartFolderRules", "GroupLogic", "TEXT NOT NULL DEFAULT 'And'", cancellationToken).ConfigureAwait(false);
        await EnsureP3QueryDocumentsAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RejectFutureVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='AssetLibrarySchemaInfo';";
        if (Convert.ToInt32(await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) == 0) return;
        await using var version = connection.CreateCommand();
        version.CommandText = "SELECT COALESCE(MAX(Version),0) FROM AssetLibrarySchemaInfo;";
        var current = Convert.ToInt32(await version.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        if (current > Version)
            throw new InvalidDataException($"素材库 schema {current} 高于当前支持的 v{Version}，已拒绝以旧代码打开。");
    }

    private static async Task EnsureP3QueryDocumentsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (var create = connection.CreateCommand())
            {
                create.Transaction = transaction;
                create.CommandText = """
                    CREATE TABLE IF NOT EXISTS SmartFolderQueryDocuments(
                        SmartFolderId TEXT NOT NULL PRIMARY KEY,
                        DocumentVersion INTEGER NOT NULL,
                        QueryJson TEXT NOT NULL,
                        QueryHash TEXT NOT NULL,
                        LegacyRulesBackupJson TEXT NULL,
                        UpdatedAt TEXT NOT NULL,
                        FOREIGN KEY(SmartFolderId) REFERENCES SmartFolders(SmartFolderId) ON DELETE CASCADE
                    );
                    """;
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            var folders = new List<(Guid Id, SmartFolderLogic Logic)>();
            await using (var readFolders = connection.CreateCommand())
            {
                readFolders.Transaction = transaction;
                readFolders.CommandText = "SELECT f.SmartFolderId,f.Logic FROM SmartFolders f LEFT JOIN SmartFolderQueryDocuments q ON q.SmartFolderId=f.SmartFolderId WHERE q.SmartFolderId IS NULL ORDER BY f.SmartFolderId;";
                await using var reader = await readFolders.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!Guid.TryParse(reader.GetString(0), out var id))
                        throw new InvalidDataException("旧智能文件夹包含无效标识，已拒绝 v6→v7 迁移。");
                    if (!Enum.TryParse<SmartFolderLogic>(reader.GetString(1), true, out var logic) || !Enum.IsDefined(logic))
                        throw new InvalidDataException($"旧智能文件夹 {id:D} 包含未知根逻辑，已拒绝 v6→v7 迁移。");
                    folders.Add((id, logic));
                }
            }

            foreach (var folder in folders)
            {
                var legacy = new List<LegacyRuleRow>();
                await using (var readRules = connection.CreateCommand())
                {
                    readRules.Transaction = transaction;
                    readRules.CommandText = "SELECT RuleId,Field,Operator,Value,Negated,SortOrder,GroupId,GroupLogic FROM SmartFolderRules WHERE SmartFolderId=$id ORDER BY SortOrder,RuleId;";
                    readRules.Parameters.AddWithValue("$id", folder.Id.ToString("D"));
                    await using var reader = await readRules.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        legacy.Add(new(
                            reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                            reader.GetInt32(4) != 0, reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetString(7)));
                }

                var converted = ConvertLegacyRules(folder.Logic, legacy);
                if (converted is null) throw new InvalidDataException($"智能文件夹 {folder.Id:D} 包含无法迁移的旧规则，已回滚整个 v6→v7 迁移。");
                converted = await AssetQueryReferenceIntegrity.ResolveLegacyNameReferencesAsync(
                    connection, transaction, converted, cancellationToken).ConfigureAwait(false);
                var queryJson = AssetQueryDocumentCodec.SerializeCanonical(converted);
                var queryHash = AssetQueryDocumentCodec.ComputeHash(converted);
                var backupJson = JsonSerializer.Serialize(legacy);
                await using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO SmartFolderQueryDocuments(SmartFolderId,DocumentVersion,QueryJson,QueryHash,LegacyRulesBackupJson,UpdatedAt) VALUES($id,$version,$json,$hash,$backup,$updated);";
                insert.Parameters.AddWithValue("$id", folder.Id.ToString("D"));
                insert.Parameters.AddWithValue("$version", AssetQueryDocument.CurrentVersion);
                insert.Parameters.AddWithValue("$json", queryJson);
                insert.Parameters.AddWithValue("$hash", queryHash);
                insert.Parameters.AddWithValue("$backup", backupJson);
                insert.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await ValidateP3QueryDocumentsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await using (var version = connection.CreateCommand())
            {
                version.Transaction = transaction;
                version.CommandText = "INSERT OR IGNORE INTO AssetLibrarySchemaInfo(Version,AppliedAt) VALUES($version,$at);";
                version.Parameters.AddWithValue("$version", Version);
                version.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
                await version.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task ValidateP3QueryDocumentsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT SmartFolderId,DocumentVersion,QueryJson,QueryHash,LegacyRulesBackupJson FROM SmartFolderQueryDocuments ORDER BY SmartFolderId;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.GetString(0);
            var version = reader.GetInt32(1);
            if (version != AssetQueryDocument.CurrentVersion) throw new InvalidDataException($"智能文件夹 {id} 使用未知查询版本 {version}。");
            var parsed = AssetQueryDocumentCodec.Parse(reader.GetString(2));
            if (!parsed.IsValid || parsed.Document is null) throw new InvalidDataException($"智能文件夹 {id} 查询文档损坏：{parsed.ErrorMessage}");
            if (!string.Equals(reader.GetString(3), AssetQueryDocumentCodec.ComputeHash(parsed.Document), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"智能文件夹 {id} 查询文档哈希不匹配。");
            if (!reader.IsDBNull(4))
            {
                try
                {
                    using var backup = JsonDocument.Parse(reader.GetString(4));
                    if (backup.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("backup root is not an array");
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException($"智能文件夹 {id} 的旧规则备份损坏。", exception);
                }
            }
        }
    }

    private static AssetQueryDocument? ConvertLegacyRules(SmartFolderLogic rootLogic, IReadOnlyList<LegacyRuleRow> rows)
    {
        var rootChildren = new List<AssetQueryNode>();
        foreach (var row in rows.Where(row => row.GroupId is null))
        {
            var converted = ConvertLegacyRule(row);
            if (converted is null) return null;
            rootChildren.Add(converted);
        }
        foreach (var group in rows.Where(row => row.GroupId is not null).GroupBy(row => row.GroupId, StringComparer.Ordinal))
        {
            if (!Guid.TryParse(group.Key, out _)) return null;
            var groupChildren = new List<AssetQueryNode>();
            foreach (var row in group)
            {
                var converted = ConvertLegacyRule(row);
                if (converted is null) return null;
                groupChildren.Add(converted);
            }
            var groupLogicValues = group.Select(row => row.GroupLogic).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (groupLogicValues.Length != 1 ||
                !Enum.TryParse<SmartFolderLogic>(groupLogicValues[0], true, out var logic) ||
                !Enum.IsDefined(logic)) return null;
            rootChildren.Add(AssetQueryNode.Group(logic == SmartFolderLogic.Or ? AssetQueryLogic.Any : AssetQueryLogic.All, groupChildren));
        }
        return new AssetQueryDocument
        {
            Scope = AssetQueryScope.AllAssets,
            RootGroup = AssetQueryNode.Group(rootLogic == SmartFolderLogic.Or ? AssetQueryLogic.Any : AssetQueryLogic.All, rootChildren)
        };
    }

    private static AssetQueryNode? ConvertLegacyRule(LegacyRuleRow row)
    {
        if (!Guid.TryParse(row.RuleId, out _) ||
            !Enum.TryParse<SmartFolderField>(row.Field, true, out var field) || !Enum.IsDefined(field) ||
            !Enum.TryParse<SmartFolderOperator>(row.Operator, true, out var operation) || !Enum.IsDefined(operation) ||
            !Enum.TryParse<AssetQueryField>(field.ToString(), true, out var queryField)) return null;
        var queryOperator = operation switch
        {
            SmartFolderOperator.Contains => AssetQueryOperator.Contains,
            SmartFolderOperator.Equals => field is SmartFolderField.Folder or SmartFolderField.Tag ? AssetQueryOperator.AnyOf : AssetQueryOperator.Equals,
            SmartFolderOperator.NotEquals => field is SmartFolderField.Folder or SmartFolderField.Tag ? AssetQueryOperator.NoneOf : AssetQueryOperator.NotEquals,
            SmartFolderOperator.StartsWith => AssetQueryOperator.StartsWith,
            SmartFolderOperator.EndsWith => AssetQueryOperator.EndsWith,
            SmartFolderOperator.GreaterThan => AssetQueryOperator.GreaterThan,
            SmartFolderOperator.GreaterThanOrEqual => AssetQueryOperator.GreaterThanOrEqual,
            SmartFolderOperator.LessThan => AssetQueryOperator.LessThan,
            SmartFolderOperator.LessThanOrEqual => AssetQueryOperator.LessThanOrEqual,
            SmartFolderOperator.Regex => AssetQueryOperator.Regex,
            SmartFolderOperator.IsTrue => AssetQueryOperator.IsTrue,
            SmartFolderOperator.IsFalse => AssetQueryOperator.IsFalse,
            SmartFolderOperator.InRange => AssetQueryOperator.Between,
            _ => throw new InvalidOperationException("Unsupported legacy smart-folder operator.")
        };
        IReadOnlyList<string> values = queryOperator switch
        {
            AssetQueryOperator.IsTrue or AssetQueryOperator.IsFalse => [],
            AssetQueryOperator.Between => row.Value.Split(["..", ",", "，"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            _ when queryField is AssetQueryField.Folder or AssetQueryField.Tag => ["name:" + row.Value],
            _ => [NormalizeLegacyQueryValue(queryField, row.Value)]
        };
        return AssetQueryNode.Rule(queryField, queryOperator, values, negated: row.Negated);
    }

    private static string NormalizeLegacyQueryValue(AssetQueryField field, string value) => field == AssetQueryField.VisualAnalysisStatus
        ? value.Trim().ToLowerInvariant() switch
        {
            "analyzed" or "completed" or "succeeded" or "valid" => "Valid",
            "pending" or "notanalyzed" => "NotAnalyzed",
            "failed" => "Failed",
            "stale" or "unavailable" => "Stale",
            _ => throw new InvalidDataException($"未知旧视觉分析状态“{value}”。")
        }
        : value;

    private sealed record LegacyRuleRow(
        string RuleId,
        string Field,
        string Operator,
        string Value,
        bool Negated,
        int SortOrder,
        string? GroupId,
        string GroupLogic);

    private static async Task EnsureVisualAnalysisCacheSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var primaryKeyColumns = new List<string>();
        await using (var info = connection.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(AssetVisualAnalysis);";
            await using var reader = await info.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                if (reader.GetInt32(5) > 0) primaryKeyColumns.Add(reader.GetString(1));
        }
        if (primaryKeyColumns.Contains("PaletteSize", StringComparer.OrdinalIgnoreCase) &&
            primaryKeyColumns.Contains("PaletteSort", StringComparer.OrdinalIgnoreCase)) return;

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var migrate = connection.CreateCommand();
            migrate.Transaction = transaction;
            migrate.CommandText = """
                CREATE TABLE AssetVisualAnalysis_v5(
                    AssetId TEXT NOT NULL, AnalysisVersion TEXT NOT NULL, ContentHash TEXT NOT NULL,
                    PaletteSize INTEGER NOT NULL, PaletteSort TEXT NOT NULL, AnalysisSource TEXT NOT NULL,
                    SourceProfile TEXT NOT NULL, AnalysisProfile TEXT NOT NULL, ResultJson TEXT NOT NULL, CreatedAt TEXT NOT NULL,
                    PRIMARY KEY(AssetId,AnalysisVersion,PaletteSize,PaletteSort),
                    FOREIGN KEY(AssetId) REFERENCES AssetItems(AssetId) ON DELETE CASCADE);
                DROP TABLE AssetVisualAnalysis;
                ALTER TABLE AssetVisualAnalysis_v5 RENAME TO AssetVisualAnalysis;
                """;
            await migrate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task EnsureAssetIdentitySchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var hasDiscriminator = false;
        await using (var info = connection.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(AssetItems);";
            await using var reader = await info.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                if (string.Equals(reader.GetString(1), "DuplicateDiscriminator", StringComparison.OrdinalIgnoreCase)) hasDiscriminator = true;
        }
        if (hasDiscriminator) return;

        await using (var disable = connection.CreateCommand()) { disable.CommandText = "PRAGMA foreign_keys=OFF;"; await disable.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var migrate = connection.CreateCommand();
            migrate.Transaction = transaction;
            migrate.CommandText = """
                CREATE TABLE AssetItems_v4(
                    AssetId TEXT NOT NULL PRIMARY KEY, SourcePath TEXT NOT NULL, NormalizedSourcePath TEXT NOT NULL,
                    DuplicateDiscriminator TEXT NOT NULL DEFAULT '', DisplayName TEXT NOT NULL, Extension TEXT NOT NULL,
                    MediaType TEXT NOT NULL, FileSize INTEGER NOT NULL DEFAULT 0 CHECK(FileSize >= 0), ContentHash TEXT NULL,
                    Width INTEGER NULL, Height INTEGER NULL, Orientation TEXT NULL, CaptureTime TEXT NULL, AddedAt TEXT NOT NULL,
                    ModifiedAt TEXT NOT NULL, Rating INTEGER NOT NULL DEFAULT 0 CHECK(Rating BETWEEN 0 AND 5), Comment TEXT NOT NULL DEFAULT '',
                    IsMissing INTEGER NOT NULL DEFAULT 0 CHECK(IsMissing IN(0,1)), IsArchived INTEGER NOT NULL DEFAULT 0 CHECK(IsArchived IN(0,1)),
                    ImportMode TEXT NOT NULL DEFAULT 'Reference', ManagedCopyPath TEXT NULL,
                    UNIQUE(NormalizedSourcePath,DuplicateDiscriminator));
                INSERT INTO AssetItems_v4(AssetId,SourcePath,NormalizedSourcePath,DuplicateDiscriminator,DisplayName,Extension,MediaType,FileSize,ContentHash,Width,Height,Orientation,CaptureTime,AddedAt,ModifiedAt,Rating,Comment,IsMissing,IsArchived,ImportMode,ManagedCopyPath)
                SELECT AssetId,SourcePath,CASE WHEN instr(NormalizedSourcePath,'|INDEPENDENT|')>0 THEN substr(NormalizedSourcePath,1,instr(NormalizedSourcePath,'|INDEPENDENT|')-1) ELSE NormalizedSourcePath END,
                       CASE WHEN instr(NormalizedSourcePath,'|INDEPENDENT|')>0 THEN substr(NormalizedSourcePath,instr(NormalizedSourcePath,'|INDEPENDENT|')+13) ELSE '' END,
                       DisplayName,Extension,MediaType,FileSize,ContentHash,Width,Height,Orientation,CaptureTime,AddedAt,ModifiedAt,Rating,Comment,IsMissing,IsArchived,ImportMode,ManagedCopyPath FROM AssetItems;
                DROP TABLE AssetItems;
                ALTER TABLE AssetItems_v4 RENAME TO AssetItems;
                """;
            await migrate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await using var enable = connection.CreateCommand(); enable.CommandText = "PRAGMA foreign_keys=ON;"; await enable.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task EnsureAssetIndexesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        foreach (var sql in new[]
        {
            "CREATE INDEX IF NOT EXISTS IX_AssetItems_DisplayName ON AssetItems(DisplayName COLLATE NOCASE);",
            "CREATE INDEX IF NOT EXISTS IX_AssetItems_AddedAt ON AssetItems(AddedAt DESC,AssetId);",
            "CREATE INDEX IF NOT EXISTS IX_AssetItems_Rating ON AssetItems(Rating,AddedAt DESC,AssetId);",
            "CREATE INDEX IF NOT EXISTS IX_AssetItems_ContentHash ON AssetItems(ContentHash) WHERE ContentHash IS NOT NULL;"
        })
        {
            await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EnsureColumnAsync(SqliteConnection connection, string table, string column, string declaration, CancellationToken cancellationToken)
    {
        await using var info = connection.CreateCommand();
        info.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await info.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
        await reader.DisposeAsync().ConfigureAwait(false);
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {declaration};";
        await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
