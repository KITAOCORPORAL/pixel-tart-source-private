using System.Globalization;
using System.Text;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryP3QueryDocumentTests
{
    [TestMethod]
    public void OptionalSearchClausesNormalizeRoundTripAndKeepLegacyDocumentsCanonical()
    {
        var legacy = new AssetQueryDocument { Text = " legacy " };
        var legacyJson = AssetQueryDocumentCodec.SerializeCanonical(legacy);
        Assert.DoesNotContain("searchClauses", legacyJson, StringComparison.Ordinal);
        var legacyRoundTrip = AssetQueryDocumentCodec.Parse(legacyJson);
        Assert.IsTrue(legacyRoundTrip.IsValid, legacyRoundTrip.ErrorMessage);
        Assert.IsNull(legacyRoundTrip.Document!.SearchClauses);

        var composed = legacy with
        {
            SearchClauses = [" mixed ", "100", "mixed", "Cafe\u0301"]
        };
        var normalized = AssetQueryDocumentCodec.Normalize(composed);
        Assert.IsTrue(normalized.IsValid, normalized.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "mixed", "100", "Caf\u00e9" }, normalized.Document!.SearchClauses!.ToArray());
        var canonical = AssetQueryDocumentCodec.SerializeCanonical(composed);
        var roundTrip = AssetQueryDocumentCodec.Parse(canonical);
        Assert.IsTrue(roundTrip.IsValid, roundTrip.ErrorMessage);
        Assert.AreEqual(canonical, AssetQueryDocumentCodec.SerializeCanonical(roundTrip.Document!));
        Assert.AreEqual(AssetQueryDocumentCodec.ComputeHash(composed), AssetQueryDocumentCodec.ComputeHash(roundTrip.Document!));
        Assert.AreEqual(
            AssetQueryDocumentCodec.ComputeHash(composed),
            AssetQueryDocumentCodec.ComputeHash(composed with { SearchClauses = composed.SearchClauses!.Reverse().ToArray() }),
            "Independent text clauses are ANDed, so semantic hashes must not depend on their order.");
    }

    [TestMethod]
    public void CanonicalCodecNormalizesUnicodeNumbersSetsAndProducesStableHash()
    {
        var firstTag = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondTag = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var document = new AssetQueryDocument
        {
            Scope = AssetQueryScope.AllAssets,
            Text = "  Cafe\u0301  ",
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                AssetQueryNode.Rule(
                    AssetQueryField.Tag,
                    AssetQueryOperator.AllOf,
                    [$" ID:{secondTag:D} ", $"id:{firstTag:D}", $"id:{secondTag:D}"]),
                AssetQueryNode.Rule(AssetQueryField.FileSize, AssetQueryOperator.Between, [" 1e3 ", "2000.0"]),
                AssetQueryNode.Rule(AssetQueryField.Comment, AssetQueryOperator.Contains, ["  \u4e2d\u6587\ud83d\udcf7  "])
            ])
        };

        var normalized = AssetQueryDocumentCodec.Normalize(document);

        Assert.IsTrue(normalized.IsValid, normalized.ErrorMessage);
        Assert.IsNotNull(normalized.Document);
        Assert.AreEqual("Caf\u00e9", normalized.Document.Text);
        CollectionAssert.AreEqual(
            new[] { $"id:{firstTag:D}", $"id:{secondTag:D}" },
            normalized.Document.RootGroup.Children[0].Values.ToArray());
        CollectionAssert.AreEqual(
            new[] { "1000", "2000" },
            normalized.Document.RootGroup.Children[1].Values.ToArray(),
            $"Actual: [{string.Join(',', normalized.Document.RootGroup.Children[1].Values)}]");
        CollectionAssert.AreEqual(new[] { "\u4e2d\u6587\ud83d\udcf7" }, normalized.Document.RootGroup.Children[2].Values.ToArray());

        var canonical = AssetQueryDocumentCodec.SerializeCanonical(document);
        var parsed = AssetQueryDocumentCodec.Parse(canonical);
        Assert.IsTrue(parsed.IsValid, parsed.ErrorMessage);
        Assert.IsNotNull(parsed.Document);
        Assert.AreEqual(canonical, AssetQueryDocumentCodec.SerializeCanonical(parsed.Document));
        Assert.AreEqual(64, AssetQueryDocumentCodec.ComputeHash(document).Length);
        Assert.AreEqual(AssetQueryDocumentCodec.ComputeHash(document), AssetQueryDocumentCodec.ComputeHash(parsed.Document));
    }

    [TestMethod]
    public void CanonicalCodecUsesInvariantCultureForTypedValues()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var valid = AssetQueryDocumentCodec.Normalize(DocumentWith(
                AssetQueryNode.Rule(AssetQueryField.AspectRatio, AssetQueryOperator.Equals, ["1.5"]),
                AssetQueryNode.Rule(AssetQueryField.AddedAt, AssetQueryOperator.GreaterThanOrEqual, ["2026-09-02T08:30:00+08:00"])));

            Assert.IsTrue(valid.IsValid, valid.ErrorMessage);
            Assert.IsNotNull(valid.Document);
            Assert.AreEqual("1.5", valid.Document.RootGroup.Children[0].Values.Single());
            Assert.AreEqual("2026-09-02T00:30:00.0000000+00:00", valid.Document.RootGroup.Children[1].Values.Single());

            var commaDecimal = AssetQueryDocumentCodec.Normalize(DocumentWith(
                AssetQueryNode.Rule(AssetQueryField.AspectRatio, AssetQueryOperator.Equals, ["1,5"])));
            Assert.IsFalse(commaDecimal.IsValid, "Locale-specific decimal separators must not alter the persisted contract.");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [TestMethod]
    public void CodecFailsClosedForUnknownMembersEnumsVersionsAndIllegalRuleContracts()
    {
        var canonical = AssetQueryDocumentCodec.SerializeCanonical(DocumentWith(
            AssetQueryNode.Rule(AssetQueryField.FileName, AssetQueryOperator.Contains, ["safe"])));

        AssertInvalid(canonical[..^1] + ",\"futureMember\":true}");
        AssertInvalid(canonical.Replace("\"fileName\"", "\"futureField\"", StringComparison.Ordinal));
        AssertInvalid(AssetQueryDocumentCodec.SerializeCanonical(DocumentWith())
            .Replace("\"version\":1", "\"version\":99", StringComparison.Ordinal));
        AssertInvalid("{not-json");
        AssertInvalid(string.Empty);

        var rootRule = new AssetQueryDocument
        {
            RootGroup = AssetQueryNode.Rule(AssetQueryField.FileName, AssetQueryOperator.Contains, ["x"])
        };
        Assert.IsFalse(AssetQueryDocumentCodec.Normalize(rootRule).IsValid);

        Assert.IsFalse(AssetQueryDocumentCodec.Normalize(DocumentWith(
            AssetQueryNode.Rule(AssetQueryField.Folder, AssetQueryOperator.Contains, ["name:folder"]))).IsValid);
        Assert.IsFalse(AssetQueryDocumentCodec.Normalize(DocumentWith(
            AssetQueryNode.Rule(AssetQueryField.Rating, AssetQueryOperator.Between, ["1"]))).IsValid);
        Assert.IsFalse(AssetQueryDocumentCodec.Normalize(DocumentWith(
            AssetQueryNode.Rule(AssetQueryField.Comment, AssetQueryOperator.IsEmpty, ["unexpected"]))).IsValid);
        Assert.IsFalse(AssetQueryDocumentCodec.Normalize(DocumentWith(
            AssetQueryNode.Rule(AssetQueryField.Rating, AssetQueryOperator.Equals, ["NaN"]))).IsValid);
        Assert.IsFalse(AssetQueryDocumentCodec.Normalize(DocumentWith(
            AssetQueryNode.Rule(AssetQueryField.AddedAt, AssetQueryOperator.Equals, ["not-a-date"]))).IsValid);
    }

    [TestMethod]
    public void NestedAllAnyNotRoundTripsAndClearUnlockedKeepsOnlyLockedRules()
    {
        var locked = AssetQueryNode.Rule(
            AssetQueryField.IsMissing,
            AssetQueryOperator.IsFalse,
            locked: true);
        var unlocked = AssetQueryNode.Rule(
            AssetQueryField.Comment,
            AssetQueryOperator.Contains,
            ["temporary"]);
        var disabled = AssetQueryNode.Rule(
            AssetQueryField.Rating,
            AssetQueryOperator.GreaterThanOrEqual,
            ["3"],
            enabled: false);
        var document = new AssetQueryDocument
        {
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                locked,
                AssetQueryNode.Group(AssetQueryLogic.Any,
                [
                    unlocked,
                    AssetQueryNode.Group(
                        AssetQueryLogic.All,
                        [disabled],
                        negated: true)
                ], negated: true)
            ])
        };

        var canonical = AssetQueryDocumentCodec.SerializeCanonical(document);
        var roundTrip = AssetQueryDocumentCodec.Parse(canonical);
        Assert.IsTrue(roundTrip.IsValid, roundTrip.ErrorMessage);
        Assert.IsNotNull(roundTrip.Document);
        Assert.IsTrue(roundTrip.Document.RootGroup.Children[1].Negated);
        Assert.IsTrue(roundTrip.Document.RootGroup.Children[1].Children[1].Negated);
        Assert.IsFalse(roundTrip.Document.RootGroup.Children[1].Children[1].Children[0].Enabled);

        var cleared = AssetQueryDocumentCodec.ClearUnlocked(document);
        Assert.HasCount(1, cleared.RootGroup.Children);
        Assert.AreEqual(AssetQueryNodeKind.Rule, cleared.RootGroup.Children[0].Kind);
        Assert.IsTrue(cleared.RootGroup.Children[0].Locked);
        Assert.AreEqual(AssetQueryField.IsMissing, cleared.RootGroup.Children[0].Field);
    }

    [TestMethod]
    public void EmptyRuleIsRemovedWithWarningButMalformedNonEmptyRuleIsRejected()
    {
        var emptyRule = new AssetQueryNode
        {
            Kind = AssetQueryNodeKind.Rule,
            Values = ["  "]
        };
        var normalized = AssetQueryDocumentCodec.Normalize(DocumentWith(emptyRule));

        Assert.IsTrue(normalized.IsValid, normalized.ErrorMessage);
        Assert.IsNotNull(normalized.Document);
        Assert.IsEmpty(normalized.Document.RootGroup.Children);
        Assert.HasCount(1, normalized.Warnings);

        var malformed = emptyRule with { Values = ["content"] };
        Assert.IsFalse(AssetQueryDocumentCodec.Normalize(DocumentWith(malformed)).IsValid);
    }

    [TestMethod]
    public void EveryFieldAndOperatorCombinationHasAnExplicitFailClosedCodecContract()
    {
        foreach (var field in Enum.GetValues<AssetQueryField>())
        foreach (var operation in Enum.GetValues<AssetQueryOperator>())
        {
            var expected = IsSupported(field, operation);
            var actual = AssetQueryDocumentCodec.Normalize(DocumentWith(
                AssetQueryNode.Rule(field, operation, ValuesFor(field, operation))));
            Assert.AreEqual(
                expected,
                actual.IsValid,
                $"Unexpected codec contract for {field}/{operation}: {actual.ErrorMessage}");
        }
    }

    [TestMethod]
    public void WorkspaceSettingsPersistOnlyLegalCanonicalP3StateAndBoundedHistory()
    {
        var now = DateTimeOffset.Parse("2026-09-02T10:00:00+08:00", CultureInfo.InvariantCulture);
        var document = DocumentWith(AssetQueryNode.Rule(
            AssetQueryField.Comment,
            AssetQueryOperator.Contains,
            ["  Cafe\u0301  "]));
        var settings = new AssetLibraryWorkspaceSettings
        {
            QueryScope = AssetQueryScope.AllAssets,
            QueryDocumentJson = AssetQueryDocumentCodec.SerializeCanonical(document),
            QueryHistory =
            [
                new("  Cafe\u0301  ", now.AddMinutes(-1)),
                new("CAF\u00c9", now),
                .. Enumerable.Range(0, 60).Select(index => new AssetQueryHistoryEntry($"query-{index:00}", now.AddMinutes(-index - 2)))
            ]
        };

        settings.Normalize();

        Assert.AreEqual(AssetQueryScope.AllAssets, settings.QueryScope);
        Assert.IsNotNull(settings.QueryDocumentJson);
        Assert.AreEqual(settings.QueryDocumentJson, AssetQueryDocumentCodec.SerializeCanonical(AssetQueryDocumentCodec.Parse(settings.QueryDocumentJson).Document!));
        Assert.HasCount(50, settings.QueryHistory);
        Assert.HasCount(1, settings.QueryHistory.Where(item => string.Equals(item.Text, "Caf\u00e9", StringComparison.OrdinalIgnoreCase)));

        settings.QueryScope = (AssetQueryScope)999;
        settings.QueryDocumentJson = settings.QueryDocumentJson.Replace("\"version\":1", "\"version\":99", StringComparison.Ordinal);
        settings.Normalize();
        Assert.AreEqual(AssetQueryScope.Current, settings.QueryScope);
        Assert.IsNull(settings.QueryDocumentJson);
    }

    private static AssetQueryDocument DocumentWith(params AssetQueryNode[] nodes) => new()
    {
        RootGroup = AssetQueryNode.Group(AssetQueryLogic.All, nodes)
    };

    private static void AssertInvalid(string json)
    {
        var parsed = AssetQueryDocumentCodec.Parse(json);
        Assert.IsFalse(parsed.IsValid, json);
        Assert.IsNull(parsed.Document);
        Assert.IsNotEmpty(parsed.Errors);
    }

    private static bool IsSupported(AssetQueryField field, AssetQueryOperator operation)
    {
        if (field is AssetQueryField.Folder or AssetQueryField.Tag)
            return operation is AssetQueryOperator.AnyOf or AssetQueryOperator.AllOf or AssetQueryOperator.NoneOf;
        if (field is AssetQueryField.IsUncategorized or AssetQueryField.IsUntagged or AssetQueryField.IsMissing or AssetQueryField.IsArchived)
            return operation is AssetQueryOperator.IsTrue or AssetQueryOperator.IsFalse;
        if (IsNumeric(field) || field is AssetQueryField.AddedAt or AssetQueryField.CaptureTime)
            return operation is
                AssetQueryOperator.Equals or AssetQueryOperator.NotEquals or
                AssetQueryOperator.GreaterThan or AssetQueryOperator.GreaterThanOrEqual or
                AssetQueryOperator.LessThan or AssetQueryOperator.LessThanOrEqual or
                AssetQueryOperator.Between or AssetQueryOperator.Unknown or AssetQueryOperator.Known;
        if (field == AssetQueryField.VisualDominantColor)
            return operation is AssetQueryOperator.Equals or AssetQueryOperator.NotEquals;
        if (operation == AssetQueryOperator.Regex)
            return field is AssetQueryField.FileName or AssetQueryField.Comment;
        return operation is
            AssetQueryOperator.Contains or AssetQueryOperator.NotContains or
            AssetQueryOperator.Equals or AssetQueryOperator.NotEquals or
            AssetQueryOperator.StartsWith or AssetQueryOperator.EndsWith or
            AssetQueryOperator.IsEmpty or AssetQueryOperator.IsNotEmpty or
            AssetQueryOperator.AnyOf or AssetQueryOperator.NoneOf or
            AssetQueryOperator.Unknown or AssetQueryOperator.Known;
    }

    private static IReadOnlyList<string> ValuesFor(AssetQueryField field, AssetQueryOperator operation)
    {
        if (operation is
            AssetQueryOperator.IsEmpty or AssetQueryOperator.IsNotEmpty or
            AssetQueryOperator.IsTrue or AssetQueryOperator.IsFalse or
            AssetQueryOperator.Unknown or AssetQueryOperator.Known)
            return [];
        if (operation == AssetQueryOperator.Between)
            return field is AssetQueryField.AddedAt or AssetQueryField.CaptureTime
                ? ["2026-01-01T00:00:00Z", "2026-12-31T23:59:59Z"]
                : field is AssetQueryField.VisualAverageSaturation or AssetQueryField.VisualLumaSpread or
                    AssetQueryField.VisualShadowRatio or AssetQueryField.VisualHighlightRatio or
                    AssetQueryField.VisualBlackClipRatio or AssetQueryField.VisualWhiteClipRatio
                    ? ["0.1", "0.9"]
                    : ["1", "2"];
        if (field is AssetQueryField.Folder or AssetQueryField.Tag)
            return ["name:first", "name:second"];
        if (operation is AssetQueryOperator.AnyOf or AssetQueryOperator.AllOf or AssetQueryOperator.NoneOf)
            return field switch
            {
                AssetQueryField.MediaType => ["Image", "Raw"],
                AssetQueryField.Orientation => ["Landscape", "Portrait"],
                AssetQueryField.VisualAnalysisStatus => ["Valid", "Stale"],
                AssetQueryField.VisualHarmony => ["Analogous", "Complementary"],
                AssetQueryField.VisualToneKey or AssetQueryField.VisualContrast or AssetQueryField.VisualSaturation => ["Low", "High"],
                AssetQueryField.VisualWarmCool => ["Cool", "Warm"],
                _ => ["first", "second"]
            };
        if (IsNumeric(field)) return field is
            AssetQueryField.Rating or AssetQueryField.FileSize or AssetQueryField.Width or AssetQueryField.Height or
            AssetQueryField.LongEdge or AssetQueryField.ShortEdge or AssetQueryField.PixelCount
                ? ["1"]
                : field is AssetQueryField.VisualAverageSaturation or AssetQueryField.VisualLumaSpread or
                    AssetQueryField.VisualShadowRatio or AssetQueryField.VisualHighlightRatio or
                    AssetQueryField.VisualBlackClipRatio or AssetQueryField.VisualWhiteClipRatio
                    ? ["0.5"]
                    : ["1.5"];
        if (field is AssetQueryField.AddedAt or AssetQueryField.CaptureTime) return ["2026-09-02T00:00:00Z"];
        return field switch
        {
            AssetQueryField.MediaType => ["Image"],
            AssetQueryField.Orientation => ["Landscape"],
            AssetQueryField.VisualAnalysisStatus => ["Valid"],
            AssetQueryField.VisualHarmony => ["Analogous"],
            AssetQueryField.VisualToneKey => ["Mid"],
            AssetQueryField.VisualContrast => ["Medium"],
            AssetQueryField.VisualSaturation => ["Medium"],
            AssetQueryField.VisualWarmCool => ["Neutral"],
            AssetQueryField.VisualDominantColor => ["#AABBCC"],
            _ => ["value"]
        };
    }

    private static bool IsNumeric(AssetQueryField field) => field is
        AssetQueryField.Rating or AssetQueryField.FileSize or AssetQueryField.Width or AssetQueryField.Height or
        AssetQueryField.LongEdge or AssetQueryField.ShortEdge or AssetQueryField.PixelCount or AssetQueryField.AspectRatio or
        AssetQueryField.VisualDominantHue or AssetQueryField.VisualAverageLuma or AssetQueryField.VisualAverageSaturation or
        AssetQueryField.VisualLumaSpread or AssetQueryField.VisualShadowRatio or AssetQueryField.VisualHighlightRatio or
        AssetQueryField.VisualBlackClipRatio or AssetQueryField.VisualWhiteClipRatio;
}
