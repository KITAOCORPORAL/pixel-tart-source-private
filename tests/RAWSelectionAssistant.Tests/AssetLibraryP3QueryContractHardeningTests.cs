using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryP3QueryContractHardeningTests
{
    [TestMethod]
    public async Task EmptyAndAllDisabledGroupsUseBooleanIdentitiesBeforeNegation()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var allAssets = new[] { setup.A, setup.B, setup.C };
        var disabledRule = AssetQueryNode.Rule(
            AssetQueryField.Rating,
            AssetQueryOperator.GreaterThan,
            ["5"],
            enabled: false);

        await AssertIdsAsync(setup, allAssets, AssetQueryNode.Group(AssetQueryLogic.All));
        await AssertIdsAsync(setup, [], AssetQueryNode.Group(AssetQueryLogic.Any));
        await AssertIdsAsync(setup, allAssets, AssetQueryNode.Group(AssetQueryLogic.All, [disabledRule]));
        await AssertIdsAsync(setup, [], AssetQueryNode.Group(AssetQueryLogic.Any, [disabledRule]));
        await AssertIdsAsync(setup, [], AssetQueryNode.Group(AssetQueryLogic.All, negated: true));
        await AssertIdsAsync(setup, allAssets, AssetQueryNode.Group(AssetQueryLogic.Any, negated: true));
    }

    [TestMethod]
    public async Task CaseInsensitiveAnyOfAndNoneOfApplyCollationToTheComparedColumn()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();

        await AssertIdsAsync(
            setup,
            [setup.A, setup.C],
            AssetQueryNode.Rule(
                AssetQueryField.Extension,
                AssetQueryOperator.AnyOf,
                [".JPG"],
                caseSensitivity: AssetQueryCaseSensitivity.Insensitive));
        await AssertIdsAsync(
            setup,
            [setup.B],
            AssetQueryNode.Rule(
                AssetQueryField.Extension,
                AssetQueryOperator.NoneOf,
                [".JPG"],
                caseSensitivity: AssetQueryCaseSensitivity.Insensitive));
        await AssertIdsAsync(
            setup,
            [],
            AssetQueryNode.Rule(
                AssetQueryField.Extension,
                AssetQueryOperator.AnyOf,
                [".JPG"],
                caseSensitivity: AssetQueryCaseSensitivity.Sensitive));
    }

    [TestMethod]
    public void NullChildrenAndValuesInJsonAreRejectedWithoutThrowing()
    {
        var empty = AssetQueryDocumentCodec.SerializeCanonical(new AssetQueryDocument());
        var nullChildren = empty.Replace("\"children\":[]", "\"children\":null", StringComparison.Ordinal);
        var childResult = AssetQueryDocumentCodec.Parse(nullChildren);
        Assert.IsFalse(childResult.IsValid);
        Assert.IsNull(childResult.Document);
        Assert.IsNotEmpty(childResult.Errors);

        var withRule = AssetQueryDocumentCodec.SerializeCanonical(new AssetQueryDocument
        {
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                AssetQueryNode.Rule(AssetQueryField.MediaType, AssetQueryOperator.Equals, ["Image"])
            ])
        });
        var nullValues = withRule.Replace("\"values\":[\"Image\"]", "\"values\":null", StringComparison.Ordinal);
        var valueResult = AssetQueryDocumentCodec.Parse(nullValues);
        Assert.IsFalse(valueResult.IsValid);
        Assert.IsNull(valueResult.Document);
        Assert.IsNotEmpty(valueResult.Errors);
    }

    [TestMethod]
    public void TypedValuesRejectOutOfDomainNumbersReversedRangesAndUnknownEnums()
    {
        var invalidRules = new[]
        {
            Rule(AssetQueryField.Rating, "-1"),
            Rule(AssetQueryField.Rating, "1.5"),
            Rule(AssetQueryField.Rating, "6"),
            Rule(AssetQueryField.FileSize, "-1"),
            Rule(AssetQueryField.FileSize, "1.5"),
            Rule(AssetQueryField.Width, "-1"),
            Rule(AssetQueryField.Width, "1.5"),
            Rule(AssetQueryField.AspectRatio, "0"),
            Rule(AssetQueryField.VisualDominantHue, "-0.1"),
            Rule(AssetQueryField.VisualDominantHue, "360"),
            Rule(AssetQueryField.VisualAverageLuma, "255.1"),
            Rule(AssetQueryField.VisualAverageSaturation, "1.1"),
            Rule(AssetQueryField.VisualLumaSpread, "-0.1"),
            Rule(AssetQueryField.VisualShadowRatio, "1.1"),
            Rule(AssetQueryField.VisualHighlightRatio, "-0.1"),
            Rule(AssetQueryField.VisualBlackClipRatio, "1.1"),
            Rule(AssetQueryField.VisualWhiteClipRatio, "-0.1"),
            AssetQueryNode.Rule(AssetQueryField.Rating, AssetQueryOperator.Between, ["4", "2"]),
            AssetQueryNode.Rule(AssetQueryField.AddedAt, AssetQueryOperator.Between,
                ["2026-09-03T00:00:00Z", "2026-09-02T00:00:00Z"]),
            Rule(AssetQueryField.MediaType, "FutureMediaType"),
            Rule(AssetQueryField.Orientation, "Diagonal"),
            Rule(AssetQueryField.VisualAnalysisStatus, "FutureStatus"),
            Rule(AssetQueryField.VisualHarmony, "FutureHarmony"),
            Rule(AssetQueryField.VisualToneKey, "Middle"),
            Rule(AssetQueryField.VisualContrast, "Extreme"),
            Rule(AssetQueryField.VisualSaturation, "Extreme"),
            Rule(AssetQueryField.VisualWarmCool, "Hot"),
            Rule(AssetQueryField.VisualDominantColor, "#12GG00")
        };

        foreach (var rule in invalidRules)
        {
            var result = Normalize(rule);
            Assert.IsFalse(result.IsValid, $"{rule.Field}/{string.Join(',', rule.Values)} must fail closed.");
            Assert.IsNull(result.Document);
            Assert.IsNotEmpty(result.Errors);
        }
    }

    [TestMethod]
    public void TypedValuesCanonicalizeEnumColorAndLegalBoundariesDeterministically()
    {
        var document = new AssetQueryDocument
        {
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                Rule(AssetQueryField.Rating, "5"),
                Rule(AssetQueryField.FileSize, "0"),
                Rule(AssetQueryField.Width, "0"),
                Rule(AssetQueryField.AspectRatio, "0.0001"),
                Rule(AssetQueryField.VisualDominantHue, "359.999"),
                Rule(AssetQueryField.VisualAverageLuma, "255"),
                Rule(AssetQueryField.VisualAverageSaturation, "1"),
                Rule(AssetQueryField.VisualLumaSpread, "0"),
                Rule(AssetQueryField.VisualShadowRatio, "1"),
                Rule(AssetQueryField.MediaType, "image"),
                Rule(AssetQueryField.Orientation, "landscape"),
                Rule(AssetQueryField.VisualAnalysisStatus, "notanalyzed"),
                Rule(AssetQueryField.VisualHarmony, "splitcomplementary"),
                Rule(AssetQueryField.VisualToneKey, "mid"),
                Rule(AssetQueryField.VisualContrast, "medium"),
                Rule(AssetQueryField.VisualSaturation, "high"),
                Rule(AssetQueryField.VisualWarmCool, "neutral"),
                Rule(AssetQueryField.VisualDominantColor, "abcdef")
            ])
        };

        var result = AssetQueryDocumentCodec.Normalize(document);
        Assert.IsTrue(result.IsValid, result.ErrorMessage);
        Assert.IsNotNull(result.Document);
        var values = result.Document.RootGroup.Children.Select(child => child.Values.Single()).ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                "5", "0", "0", "0.0001", "359.999", "255", "1", "0", "1",
                "Image", "Landscape", "NotAnalyzed", "SplitComplementary", "Mid", "Medium", "High", "Neutral", "#ABCDEF"
            },
            values);

        var alternate = document with
        {
            RootGroup = document.RootGroup with
            {
                Children = document.RootGroup.Children
                    .Select(child => child.Field == AssetQueryField.VisualDominantColor
                        ? child with { Values = ["#aBcDeF"] }
                        : child)
                    .ToArray()
            }
        };
        Assert.AreEqual(AssetQueryDocumentCodec.ComputeHash(document), AssetQueryDocumentCodec.ComputeHash(alternate));

        var wrappingHueRange = Normalize(AssetQueryNode.Rule(
            AssetQueryField.VisualDominantHue,
            AssetQueryOperator.Between,
            ["350", "10"]));
        Assert.IsTrue(wrappingHueRange.IsValid, wrappingHueRange.ErrorMessage);
    }

    [TestMethod]
    public void InsensitiveSetsUseAStableSqliteNoCaseRepresentativeIndependentOfInputOrder()
    {
        var first = Normalize(AssetQueryNode.Rule(
            AssetQueryField.Extension,
            AssetQueryOperator.AnyOf,
            [".a", ".A", ".b"],
            caseSensitivity: AssetQueryCaseSensitivity.Insensitive));
        var second = Normalize(AssetQueryNode.Rule(
            AssetQueryField.Extension,
            AssetQueryOperator.AnyOf,
            [".B", ".a", ".A"],
            caseSensitivity: AssetQueryCaseSensitivity.Insensitive));

        Assert.IsTrue(first.IsValid, first.ErrorMessage);
        Assert.IsTrue(second.IsValid, second.ErrorMessage);
        Assert.IsNotNull(first.Document);
        Assert.IsNotNull(second.Document);
        CollectionAssert.AreEqual(
            new[] { ".A", ".B" },
            first.Document.RootGroup.Children.Single().Values.ToArray());
        Assert.AreEqual(
            AssetQueryDocumentCodec.SerializeCanonical(first.Document),
            AssetQueryDocumentCodec.SerializeCanonical(second.Document));

        var sensitive = Normalize(AssetQueryNode.Rule(
            AssetQueryField.Extension,
            AssetQueryOperator.AnyOf,
            [".a", ".A"],
            caseSensitivity: AssetQueryCaseSensitivity.Sensitive));
        Assert.IsTrue(sensitive.IsValid, sensitive.ErrorMessage);
        Assert.IsNotNull(sensitive.Document);
        CollectionAssert.AreEqual(
            new[] { ".A", ".a" },
            sensitive.Document.RootGroup.Children.Single().Values.ToArray());
    }

    [TestMethod]
    public void CanonicalNodesRemovePropertiesThatDoNotAffectTheirSemantics()
    {
        var dirtyNumericRule = new AssetQueryNode
        {
            Kind = AssetQueryNodeKind.Rule,
            Logic = AssetQueryLogic.Any,
            Field = AssetQueryField.Rating,
            Operator = AssetQueryOperator.Equals,
            Values = ["3"],
            CaseSensitivity = AssetQueryCaseSensitivity.Sensitive
        };
        var sensitiveTextRule = AssetQueryNode.Rule(
            AssetQueryField.Comment,
            AssetQueryOperator.Equals,
            ["MiXeD"],
            caseSensitivity: AssetQueryCaseSensitivity.Sensitive);
        var dirtyEnumRule = AssetQueryNode.Rule(
            AssetQueryField.MediaType,
            AssetQueryOperator.Equals,
            ["image"],
            caseSensitivity: AssetQueryCaseSensitivity.Sensitive);
        var dirtyRoot = new AssetQueryNode
        {
            Kind = AssetQueryNodeKind.Group,
            Logic = AssetQueryLogic.All,
            Locked = true,
            CaseSensitivity = AssetQueryCaseSensitivity.Sensitive,
            Children = [dirtyNumericRule, sensitiveTextRule, dirtyEnumRule]
        };

        var result = AssetQueryDocumentCodec.Normalize(new AssetQueryDocument { RootGroup = dirtyRoot });

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
        Assert.IsNotNull(result.Document);
        var root = result.Document.RootGroup;
        Assert.IsFalse(root.Locked);
        Assert.AreEqual(AssetQueryCaseSensitivity.Insensitive, root.CaseSensitivity);
        Assert.AreEqual(AssetQueryLogic.All, root.Children[0].Logic);
        Assert.AreEqual(AssetQueryCaseSensitivity.Insensitive, root.Children[0].CaseSensitivity);
        Assert.AreEqual(AssetQueryCaseSensitivity.Sensitive, root.Children[1].CaseSensitivity);
        Assert.AreEqual(AssetQueryCaseSensitivity.Insensitive, root.Children[2].CaseSensitivity);
        Assert.AreEqual("Image", root.Children[2].Values.Single());
    }

    [TestMethod]
    public void SemanticHashIgnoresCommutativeChildOrderButCanonicalSerializationPreservesEditorOrder()
    {
        var rating = AssetQueryNode.Rule(AssetQueryField.Rating, AssetQueryOperator.Equals, ["3"]);
        var comment = AssetQueryNode.Rule(AssetQueryField.Comment, AssetQueryOperator.Contains, ["mixed"]);
        var orientation = AssetQueryNode.Rule(AssetQueryField.Orientation, AssetQueryOperator.Equals, ["Landscape"]);
        var missing = AssetQueryNode.Rule(AssetQueryField.IsMissing, AssetQueryOperator.IsFalse);
        var first = new AssetQueryDocument
        {
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                rating,
                AssetQueryNode.Group(AssetQueryLogic.Any, [orientation, missing]),
                comment
            ])
        };
        var reordered = first with
        {
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                comment,
                AssetQueryNode.Group(AssetQueryLogic.Any, [missing, orientation]),
                rating
            ])
        };

        var firstJson = AssetQueryDocumentCodec.SerializeCanonical(first);
        var reorderedJson = AssetQueryDocumentCodec.SerializeCanonical(reordered);
        Assert.AreNotEqual(firstJson, reorderedJson, "Persisted JSON must retain the editor's visible rule order.");
        Assert.AreEqual(AssetQueryDocumentCodec.ComputeHash(first), AssetQueryDocumentCodec.ComputeHash(reordered));

        var differentLogic = reordered with
        {
            RootGroup = reordered.RootGroup with { Logic = AssetQueryLogic.Any }
        };
        Assert.AreNotEqual(AssetQueryDocumentCodec.ComputeHash(first), AssetQueryDocumentCodec.ComputeHash(differentLogic));

        var roundTrip = AssetQueryDocumentCodec.Parse(firstJson);
        Assert.IsTrue(roundTrip.IsValid, roundTrip.ErrorMessage);
        Assert.IsNotNull(roundTrip.Document);
        CollectionAssert.AreEqual(
            new AssetQueryField?[] { AssetQueryField.Rating, null, AssetQueryField.Comment },
            roundTrip.Document.RootGroup.Children.Select(child => child.Field).ToArray());
    }

    [TestMethod]
    public void SharedCapabilityMatrixKeepsRegexAndCaseSensitivityOnSupportedTextFieldsOnly()
    {
        foreach (var field in Enum.GetValues<AssetQueryField>())
            Assert.IsNotEmpty(AssetQueryDocumentCodec.GetSupportedOperators(field), $"{field} must expose at least one supported operator.");

        CollectionAssert.Contains(
            AssetQueryDocumentCodec.GetSupportedOperators(AssetQueryField.FileName).ToArray(),
            AssetQueryOperator.Regex);
        CollectionAssert.Contains(
            AssetQueryDocumentCodec.GetSupportedOperators(AssetQueryField.Comment).ToArray(),
            AssetQueryOperator.Regex);
        foreach (var field in Enum.GetValues<AssetQueryField>().Except([AssetQueryField.FileName, AssetQueryField.Comment]))
            CollectionAssert.DoesNotContain(
                AssetQueryDocumentCodec.GetSupportedOperators(field).ToArray(),
                AssetQueryOperator.Regex,
                $"{field} must not advertise a Regex operator rejected by the codec.");

        Assert.IsTrue(AssetQueryDocumentCodec.SupportsCaseSensitivity(AssetQueryField.FileName));
        Assert.IsTrue(AssetQueryDocumentCodec.SupportsCaseSensitivity(AssetQueryField.Extension));
        Assert.IsTrue(AssetQueryDocumentCodec.SupportsCaseSensitivity(AssetQueryField.Comment));
        Assert.IsFalse(AssetQueryDocumentCodec.SupportsCaseSensitivity(AssetQueryField.MediaType));
    }

    [TestMethod]
    public async Task DisabledAncestorSkipsReferenceAndRegexRuntimeValidationUntilReenabled()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var missingId = Guid.NewGuid();
        var disabledRoot = AssetQueryNode.Group(AssetQueryLogic.All,
        [
            AssetQueryNode.Group(AssetQueryLogic.All,
            [
                AssetQueryNode.Rule(AssetQueryField.Folder, AssetQueryOperator.AnyOf, [$"id:{missingId:D}"]),
                AssetQueryNode.Rule(AssetQueryField.FileName, AssetQueryOperator.Regex, ["["])
            ], enabled: false)
        ]);
        var disabledDocument = new AssetQueryDocument { RootGroup = disabledRoot };

        Assert.IsEmpty(await setup.Repository.ValidateQueryReferencesAsync(disabledDocument));
        var disabledPage = await setup.Repository.QueryAsync(new AssetLibraryQuery(PageSize: 100) { Document = disabledDocument });
        Assert.IsTrue(string.IsNullOrWhiteSpace(disabledPage.RegexError), disabledPage.RegexError);
        Assert.AreEqual(3, disabledPage.TotalCount);

        var enabledGroup = disabledRoot.Children.Single() with { Enabled = true };
        var enabledDocument = disabledDocument with
        {
            RootGroup = disabledRoot with { Children = [enabledGroup] }
        };
        Assert.IsNotEmpty(await setup.Repository.ValidateQueryReferencesAsync(enabledDocument));

        var regexOnly = enabledDocument with
        {
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                AssetQueryNode.Group(AssetQueryLogic.All,
                    [AssetQueryNode.Rule(AssetQueryField.FileName, AssetQueryOperator.Regex, ["["])])
            ])
        };
        var enabledPage = await setup.Repository.QueryAsync(new AssetLibraryQuery(PageSize: 100) { Document = regexOnly });
        StringAssert.Contains(enabledPage.RegexError, "正则表达式无效");
        Assert.AreEqual(0, enabledPage.TotalCount);
    }

    private static AssetQueryNode Rule(AssetQueryField field, string value) =>
        AssetQueryNode.Rule(field, AssetQueryOperator.Equals, [value]);

    private static AssetQueryValidationResult Normalize(AssetQueryNode rule) =>
        AssetQueryDocumentCodec.Normalize(new AssetQueryDocument
        {
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All, [rule])
        });

    private static async Task AssertIdsAsync(
        AssetLibraryP3TestSetup setup,
        IReadOnlyCollection<Guid> expected,
        AssetQueryNode root)
    {
        var page = await setup.Repository.QueryAsync(new AssetLibraryQuery(PageSize: 100)
        {
            Document = new AssetQueryDocument
            {
                RootGroup = root.Kind == AssetQueryNodeKind.Group
                    ? root
                    : AssetQueryNode.Group(AssetQueryLogic.All, [root])
            }
        });
        Assert.IsTrue(string.IsNullOrWhiteSpace(page.RegexError), page.RegexError);
        CollectionAssert.AreEquivalent(expected.ToArray(), page.Items.Select(item => item.AssetId).ToArray());
        Assert.AreEqual(expected.Count, page.TotalCount);
    }
}
