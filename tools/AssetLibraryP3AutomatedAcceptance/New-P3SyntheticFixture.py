import datetime
import hashlib
import json
import pathlib
import sqlite3
import sys
import uuid


if len(sys.argv) != 4:
    raise RuntimeError(
        "New-P3SyntheticFixture.py expects exactly three arguments: "
        "fixture root, current-v7 database, and legacy-v6 database"
    )

root_arg, current_arg, legacy_arg = sys.argv[1:]
root = pathlib.Path(root_arg)
current_db = pathlib.Path(current_arg)
legacy_db = pathlib.Path(legacy_arg)
if not root.is_absolute() or not current_db.is_absolute() or not legacy_db.is_absolute():
    raise RuntimeError("all fixture paths must be absolute")
root = root.resolve()
current_db = current_db.resolve()
legacy_db = legacy_db.resolve()
if not root.is_dir():
    raise RuntimeError(f"fixture root is not an existing directory: {root}")
if current_db.parent != root or current_db.name != "asset-library-v16.db":
    raise RuntimeError(f"current database must be <fixture-root>/asset-library-v16.db: {current_db}")
if legacy_db.parent != root or legacy_db.name != "asset-library-v16-legacy-v6.db":
    raise RuntimeError(f"legacy database must be <fixture-root>/asset-library-v16-legacy-v6.db: {legacy_db}")
if current_db.exists() or legacy_db.exists():
    raise RuntimeError("P3 fixture databases must not already exist")


def uid(name: str) -> str:
    return str(uuid.uuid5(uuid.NAMESPACE_URL, "pixel-tart-p3-" + name))


def create_schema(connection: sqlite3.Connection, version: int) -> None:
    query_document_table = """
    CREATE TABLE SmartFolderQueryDocuments(
      SmartFolderId TEXT NOT NULL PRIMARY KEY, DocumentVersion INTEGER NOT NULL,
      QueryJson TEXT NOT NULL, QueryHash TEXT NOT NULL, LegacyRulesBackupJson TEXT NULL,
      UpdatedAt TEXT NOT NULL);
    """ if version >= 7 else ""
    connection.executescript(f"""
    PRAGMA journal_mode=DELETE;
    CREATE TABLE AssetLibrarySchemaInfo(Version INTEGER NOT NULL PRIMARY KEY, AppliedAt TEXT NOT NULL);
    CREATE TABLE AssetItems(
      AssetId TEXT NOT NULL PRIMARY KEY, SourcePath TEXT NOT NULL,
      NormalizedSourcePath TEXT NOT NULL, DuplicateDiscriminator TEXT NOT NULL DEFAULT '',
      DisplayName TEXT NOT NULL, Extension TEXT NOT NULL, MediaType TEXT NOT NULL,
      FileSize INTEGER NOT NULL DEFAULT 0 CHECK(FileSize >= 0), ContentHash TEXT NULL,
      Width INTEGER NULL, Height INTEGER NULL, Orientation TEXT NULL, CaptureTime TEXT NULL,
      AddedAt TEXT NOT NULL, ModifiedAt TEXT NOT NULL,
      Rating INTEGER NOT NULL DEFAULT 0 CHECK(Rating BETWEEN 0 AND 5),
      Comment TEXT NOT NULL DEFAULT '', IsMissing INTEGER NOT NULL DEFAULT 0 CHECK(IsMissing IN(0,1)),
      IsArchived INTEGER NOT NULL DEFAULT 0 CHECK(IsArchived IN(0,1)),
      ImportMode TEXT NOT NULL DEFAULT 'Reference', ManagedCopyPath TEXT NULL,
      UNIQUE(NormalizedSourcePath,DuplicateDiscriminator));
    CREATE TABLE AssetVisualAnalysis(
      AssetId TEXT NOT NULL, AnalysisVersion TEXT NOT NULL, ContentHash TEXT NOT NULL,
      PaletteSize INTEGER NOT NULL DEFAULT 5, PaletteSort TEXT NOT NULL DEFAULT 'Weight',
      AnalysisSource TEXT NOT NULL, SourceProfile TEXT NOT NULL, AnalysisProfile TEXT NOT NULL,
      ResultJson TEXT NOT NULL, CreatedAt TEXT NOT NULL,
      PRIMARY KEY(AssetId,AnalysisVersion,PaletteSize,PaletteSort));
    CREATE TABLE AssetVisualFeatures(
      AssetId TEXT NOT NULL, AnalysisVersion TEXT NOT NULL,
      PaletteSize INTEGER NOT NULL CHECK(PaletteSize=5), PaletteSort TEXT NOT NULL CHECK(PaletteSort='Weight'),
      ContentFingerprint TEXT NOT NULL, SourceContentHash TEXT NULL,
      Outcome TEXT NOT NULL, FailureReason TEXT NULL,
      AnalysisSource TEXT NOT NULL, SourceProfile TEXT NOT NULL, AnalysisProfile TEXT NOT NULL,
      Harmony TEXT NULL, ToneKey TEXT NULL, Contrast TEXT NULL, LuminanceSpan TEXT NULL,
      Saturation TEXT NULL, WarmCool TEXT NULL, DominantHue REAL NULL, SecondaryHue REAL NULL,
      AverageHue REAL NULL, AverageLuma REAL NULL, MedianLuma REAL NULL, ContrastMetric REAL NULL,
      LumaSpreadMetric REAL NULL, AverageSaturation REAL NULL, MedianSaturation REAL NULL,
      AverageLightness REAL NULL, WarmCoolMetric REAL NULL, DeepShadowRatio REAL NULL,
      ShadowRatio REAL NULL, MidtoneRatio REAL NULL, HighlightRatio REAL NULL, SpecularRatio REAL NULL,
      BlackClipRatio REAL NULL, WhiteClipRatio REAL NULL, HistogramLumaSignature TEXT NULL,
      PaletteSignature TEXT NULL, ResultJson TEXT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
      PRIMARY KEY(AssetId,AnalysisVersion));
    CREATE TABLE AssetVisualPaletteColors(
      AssetId TEXT NOT NULL, AnalysisVersion TEXT NOT NULL, ColorIndex INTEGER NOT NULL,
      Red INTEGER NOT NULL CHECK(Red BETWEEN 0 AND 255), Green INTEGER NOT NULL CHECK(Green BETWEEN 0 AND 255),
      Blue INTEGER NOT NULL CHECK(Blue BETWEEN 0 AND 255), LabL REAL NOT NULL, LabA REAL NOT NULL,
      LabB REAL NOT NULL, Hue REAL NOT NULL, Saturation REAL NOT NULL, Chroma REAL NOT NULL,
      Weight REAL NOT NULL CHECK(Weight>=0 AND Weight<=1), Hex TEXT NOT NULL,
      PRIMARY KEY(AssetId,AnalysisVersion,ColorIndex));
    CREATE TABLE AssetFolders(
      FolderId TEXT NOT NULL PRIMARY KEY, ParentFolderId TEXT NULL, Name TEXT NOT NULL,
      Description TEXT NOT NULL DEFAULT '', Icon TEXT NULL, Color TEXT NULL,
      SortOrder INTEGER NOT NULL DEFAULT 0, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
      IsArchived INTEGER NOT NULL DEFAULT 0, IsSystem INTEGER NOT NULL DEFAULT 0,
      AutoTagIdsJson TEXT NOT NULL DEFAULT '[]');
    CREATE TABLE AssetFolderMemberships(
      AssetId TEXT NOT NULL, FolderId TEXT NOT NULL, AddedAt TEXT NOT NULL,
      PRIMARY KEY(AssetId,FolderId));
    CREATE TABLE AssetFolderAutoTags(FolderId TEXT NOT NULL,TagId TEXT NOT NULL,PRIMARY KEY(FolderId,TagId));
    CREATE TABLE TagGroups(
      TagGroupId TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL UNIQUE,
      SortOrder INTEGER NOT NULL DEFAULT 0, CreatedAt TEXT NOT NULL,
      IsArchived INTEGER NOT NULL DEFAULT 0);
    CREATE TABLE AssetTags(
      TagId TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, TagGroupId TEXT NULL,
      SortOrder INTEGER NOT NULL DEFAULT 0, UsageCount INTEGER NOT NULL DEFAULT 0,
      CreatedAt TEXT NOT NULL, IsArchived INTEGER NOT NULL DEFAULT 0,
      UNIQUE(TagGroupId,Name));
    CREATE TABLE AssetTagMemberships(
      AssetId TEXT NOT NULL, TagId TEXT NOT NULL, AddedAt TEXT NOT NULL,
      PRIMARY KEY(AssetId,TagId));
    CREATE TABLE SmartFolders(
      SmartFolderId TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL UNIQUE,
      Logic TEXT NOT NULL DEFAULT 'And', Description TEXT NOT NULL DEFAULT '',
      CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, IsArchived INTEGER NOT NULL DEFAULT 0);
    CREATE TABLE SmartFolderRules(
      RuleId TEXT NOT NULL PRIMARY KEY, SmartFolderId TEXT NOT NULL,
      Field TEXT NOT NULL, Operator TEXT NOT NULL, Value TEXT NOT NULL DEFAULT '',
      Negated INTEGER NOT NULL DEFAULT 0, SortOrder INTEGER NOT NULL DEFAULT 0,
      GroupId TEXT NULL, GroupLogic TEXT NOT NULL DEFAULT 'And');
    {query_document_table}
    CREATE TABLE AssetLibraryUndoJournal(
      OperationId TEXT NOT NULL PRIMARY KEY, Description TEXT NOT NULL,
      OperationKind TEXT NOT NULL, PayloadJson TEXT NOT NULL, CreatedAt TEXT NOT NULL,
      UndoneAt TEXT NULL, JournalVersion INTEGER NOT NULL DEFAULT 1);
    CREATE INDEX IX_AssetItems_DisplayName ON AssetItems(DisplayName COLLATE NOCASE);
    CREATE INDEX IX_AssetItems_AddedAt ON AssetItems(AddedAt DESC,AssetId);
    CREATE INDEX IX_AssetItems_CaptureTime ON AssetItems(CaptureTime DESC);
    CREATE INDEX IX_AssetItems_Rating ON AssetItems(Rating,AddedAt DESC,AssetId);
    CREATE INDEX IX_AssetItems_MissingName ON AssetItems(IsMissing,DisplayName COLLATE NOCASE);
    CREATE INDEX IX_AssetFolderMemberships_Folder ON AssetFolderMemberships(FolderId,AssetId);
    CREATE INDEX IX_AssetTagMemberships_Tag ON AssetTagMemberships(TagId,AssetId);
    CREATE INDEX IX_AssetLibraryUndoJournal_Recent ON AssetLibraryUndoJournal(UndoneAt,CreatedAt DESC);
    CREATE INDEX IX_AssetVisualFeatures_Outcome ON AssetVisualFeatures(AnalysisVersion,Outcome,AssetId);
    CREATE INDEX IX_AssetVisualFeatures_Hue ON AssetVisualFeatures(AnalysisVersion,Outcome,DominantHue,AssetId);
    CREATE INDEX IX_AssetVisualFeatures_Luma ON AssetVisualFeatures(AnalysisVersion,Outcome,AverageLuma,AssetId);
    CREATE INDEX IX_AssetVisualFeatures_Classifications
      ON AssetVisualFeatures(AnalysisVersion,Outcome,ToneKey,Contrast,Saturation,WarmCool,AssetId);
    CREATE INDEX IX_AssetVisualPaletteColors_HueWeight
      ON AssetVisualPaletteColors(AnalysisVersion,Hue,Weight,AssetId);
    """)


now = datetime.datetime(2026, 9, 2, tzinfo=datetime.timezone.utc)
formats = [
    (".jpg", "Image"),
    (".cr3", "Raw"),
    (".png", "Image"),
    (".mp4", "Video"),
    (".mp3", "Other"),
    (".pdf", "Other"),
    (".ttf", "Other"),
]


current = sqlite3.connect(current_db)
try:
    create_schema(current, 7)
    current.execute(
        "INSERT INTO AssetLibrarySchemaInfo(Version,AppliedAt) VALUES(?,?)",
        (7, now.isoformat()),
    )
    item_rows = []
    for index in range(10128):
        extension, media_type = formats[index % len(formats)]
        archived = 1 if index >= 10000 else 0
        missing = 1 if index < 512 else 0
        asset_id = str(uuid.uuid5(uuid.NAMESPACE_URL, f"pixel-tart-p3-fixture-{index:05d}"))
        source = str(root / "media" / f"P3_{index:05d}{extension}")
        display_name = f"P3_{index:05d} 人物素材 查询样本 {index % 31 + 1:02d}{extension}"
        content_hash = hashlib.sha256(f"pixel-tart-p3-source-{index:05d}".encode("ascii")).hexdigest()
        capture_time = None if index % 23 == 0 else (now + datetime.timedelta(days=index % 91, seconds=index)).isoformat()
        added_at = (now + datetime.timedelta(seconds=index)).isoformat()
        width = None if index % 19 == 0 else 640 + index % 17 * 64
        height = None if width is None else 480 + index % 13 * 48
        orientation = None if width is None else ("Square" if width == height else "Landscape" if width > height else "Portrait")
        item_rows.append((
            asset_id, source, source.lower(), display_name, extension, media_type,
            4096 + index * 37, content_hash, width, height, orientation, capture_time,
            added_at, added_at, index % 6, "" if index % 11 == 0 else f"fixture comment {index:05d}",
            missing, archived, "Reference", None,
        ))
    current.executemany("""
      INSERT INTO AssetItems(
        AssetId,SourcePath,NormalizedSourcePath,DisplayName,Extension,MediaType,
        FileSize,ContentHash,Width,Height,Orientation,CaptureTime,AddedAt,ModifiedAt,
        Rating,Comment,IsMissing,IsArchived,ImportMode,ManagedCopyPath)
      VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
    """, item_rows)
    ids = [row[0] for row in item_rows]

    analysis_version = "visual-analysis-v2"
    feature_rows = []
    for index, asset_id in enumerate(ids[:4096]):
        outcome = "Succeeded" if index < 3072 else "Failed"
        source_hash = item_rows[index][7]
        feature_rows.append((
            asset_id, analysis_version, 5, "Weight", source_hash, source_hash, outcome,
            None if outcome == "Succeeded" else "deterministic synthetic analysis failure",
            "RasterOriginal", "UnknownAssumedSrgb", "sRGB IEC61966-2.1",
            ("Complementary", "Analogous", "Monochrome")[index % 3],
            ("Low", "Mid", "High")[index % 3],
            ("Low", "Medium", "High")[index % 3], "Medium",
            ("Low", "Medium", "High")[index % 3],
            ("Cool", "Neutral", "Warm")[index % 3],
            float((index * 13) % 360), float((index * 17) % 360), float((index * 19) % 360),
            64.0 + index % 160, 62.0 + index % 150, (index % 101) / 100.0,
            (index % 71) / 100.0, (index % 91) / 100.0, (index % 83) / 100.0,
            50.0 + index % 40, ((index % 201) - 100) / 100.0,
            0.1, 0.2, 0.4, 0.2, 0.1, 0.0, 0.0,
            "synthetic-luma", "synthetic-palette", None, now.isoformat(), now.isoformat(),
        ))
    current.executemany("""
      INSERT INTO AssetVisualFeatures(
        AssetId,AnalysisVersion,PaletteSize,PaletteSort,ContentFingerprint,SourceContentHash,Outcome,FailureReason,
        AnalysisSource,SourceProfile,AnalysisProfile,Harmony,ToneKey,Contrast,LuminanceSpan,Saturation,WarmCool,
        DominantHue,SecondaryHue,AverageHue,AverageLuma,MedianLuma,ContrastMetric,LumaSpreadMetric,
        AverageSaturation,MedianSaturation,AverageLightness,WarmCoolMetric,DeepShadowRatio,ShadowRatio,
        MidtoneRatio,HighlightRatio,SpecularRatio,BlackClipRatio,WhiteClipRatio,HistogramLumaSignature,
        PaletteSignature,ResultJson,CreatedAt,UpdatedAt)
      VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
    """, feature_rows)
    palette_rows = []
    for index, asset_id in enumerate(ids[:3072]):
        for color_index in range(5):
            red = (index * 17 + color_index * 31) % 256
            green = (index * 23 + color_index * 29) % 256
            blue = (index * 37 + color_index * 13) % 256
            palette_rows.append((
                asset_id, analysis_version, color_index, red, green, blue,
                50.0 + color_index, -10.0 + color_index, 12.0 + color_index,
                float((index * 13 + color_index * 7) % 360), 0.25, 20.0, 0.2,
                f"#{red:02X}{green:02X}{blue:02X}",
            ))
    current.executemany("""
      INSERT INTO AssetVisualPaletteColors(
        AssetId,AnalysisVersion,ColorIndex,Red,Green,Blue,LabL,LabA,LabB,Hue,Saturation,Chroma,Weight,Hex)
      VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?)
    """, palette_rows)

    folders = [
        (uid("folder-people"), None, "人物", 0),
        (uid("folder-portrait"), uid("folder-people"), "人像", 0),
        (uid("folder-fashion"), uid("folder-people"), "时尚", 1),
        (uid("folder-light"), None, "灯光", 1),
        (uid("folder-warm"), uid("folder-light"), "暖光", 0),
        (uid("folder-cool"), uid("folder-light"), "冷光", 1),
        (uid("folder-project"), None, "项目", 2),
        (uid("folder-archive"), None, "旧项目", 3),
    ]
    current.executemany(
        "INSERT INTO AssetFolders(FolderId,ParentFolderId,Name,Description,SortOrder,CreatedAt,UpdatedAt,IsArchived) VALUES(?,?,?,'',?,?,?,?)",
        [(fid, parent, name, order, now.isoformat(), now.isoformat(), 1 if name == "旧项目" else 0)
         for fid, parent, name, order in folders],
    )
    folder_memberships = []
    leaves = [uid("folder-portrait"), uid("folder-fashion"), uid("folder-warm"), uid("folder-cool")]
    for index, asset_id in enumerate(ids[:10000]):
        folder_memberships.append((asset_id, leaves[index % len(leaves)], now.isoformat()))
        if index % 2 == 0:
            folder_memberships.append((asset_id, uid("folder-project"), now.isoformat()))
    current.executemany("INSERT INTO AssetFolderMemberships VALUES(?,?,?)", folder_memberships)

    group_names = ["主题", "色调", "用途", "状态"]
    groups = [(uid(f"group-{index}"), name, index, now.isoformat(), 0) for index, name in enumerate(group_names)]
    current.executemany("INSERT INTO TagGroups VALUES(?,?,?,?,?)", groups)
    tag_names = [
        ["人像", "时尚", "建筑", "自然"],
        ["暖色", "冷色", "高调", "低调"],
        ["精选", "参考", "交付", "待选"],
        ["已检查", "待处理", "失败", "不可用"],
    ]
    tags = []
    for group_index, names in enumerate(tag_names):
        for tag_index, name in enumerate(names):
            key = f"tag-{group_index}-{tag_index}"
            tags.append((uid(key), name, uid(f"group-{group_index}"), tag_index, 0, now.isoformat(), 0))
    current.executemany("INSERT INTO AssetTags VALUES(?,?,?,?,?,?,?)", tags)
    tag_memberships = []
    for index, asset_id in enumerate(ids[:10000]):
        for group_index in range(4):
            tag_index = (index + group_index) % 4
            tag_memberships.append((asset_id, uid(f"tag-{group_index}-{tag_index}"), now.isoformat()))
        if index % 5 == 0:
            tag_memberships.append((asset_id, uid("tag-2-0"), now.isoformat()))
    current.executemany("INSERT OR IGNORE INTO AssetTagMemberships VALUES(?,?,?)", tag_memberships)
    current.execute("UPDATE AssetTags SET UsageCount=(SELECT COUNT(*) FROM AssetTagMemberships m WHERE m.TagId=AssetTags.TagId)")
    current.commit()
    if current.execute("PRAGMA quick_check").fetchone()[0] != "ok":
        raise RuntimeError("current-v7 fixture quick_check failed")
finally:
    current.close()


legacy = sqlite3.connect(legacy_db)
try:
    create_schema(legacy, 6)
    legacy.execute(
        "INSERT INTO AssetLibrarySchemaInfo(Version,AppliedAt) VALUES(?,?)",
        (6, now.isoformat()),
    )
    legacy_rows = []
    for index in range(64):
        asset_id = str(uuid.uuid5(uuid.NAMESPACE_URL, f"pixel-tart-p3-legacy-{index:03d}"))
        source = str(root / "legacy-media" / f"legacy-{index:03d}.jpg")
        added = (now + datetime.timedelta(seconds=index)).isoformat()
        legacy_rows.append((
            asset_id, source, source.lower(), f"旧规则迁移样本 {index:03d}.jpg", ".jpg", "Image",
            8192 + index, hashlib.sha256(f"legacy-{index:03d}".encode("ascii")).hexdigest(),
            800, 600, "Landscape", added, added, added, index % 6, "legacy",
            0, 1 if index >= 60 else 0, "Reference", None,
        ))
    legacy.executemany("""
      INSERT INTO AssetItems(
        AssetId,SourcePath,NormalizedSourcePath,DisplayName,Extension,MediaType,FileSize,ContentHash,
        Width,Height,Orientation,CaptureTime,AddedAt,ModifiedAt,Rating,Comment,IsMissing,IsArchived,
        ImportMode,ManagedCopyPath)
      VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
    """, legacy_rows)
    legacy_smart = uid("legacy-smart-folder")
    legacy.execute(
        "INSERT INTO SmartFolders VALUES(?,?,?,?,?,?,?)",
        (legacy_smart, "旧版嵌套迁移", "And", "P3 v6 migration fixture", now.isoformat(), now.isoformat(), 0),
    )
    legacy_rules = [
        (uid("legacy-rule-rating"), legacy_smart, "Rating", "GreaterThanOrEqual", "4", 0, 0, None, "And"),
        (uid("legacy-rule-name"), legacy_smart, "FileName", "Contains", "迁移", 0, 1, uid("legacy-group-a"), "Or"),
        (uid("legacy-rule-comment"), legacy_smart, "Comment", "Contains", "legacy", 0, 2, uid("legacy-group-a"), "Or"),
    ]
    legacy.executemany("INSERT INTO SmartFolderRules VALUES(?,?,?,?,?,?,?,?,?)", legacy_rules)
    legacy.commit()
    if legacy.execute("PRAGMA quick_check").fetchone()[0] != "ok":
        raise RuntimeError("legacy-v6 fixture quick_check failed")
finally:
    legacy.close()


expectations = {
    "schema": "pixel-tart-p3-synthetic-fixture-expectations/v1",
    "current": {
        "schema_version": 7,
        "total_count": 10128,
        "active_count": 10000,
        "archived_count": 128,
        "missing_count": 512,
        "visual_valid_count": 3072,
        "visual_failed_count": 1024,
        "visual_not_analyzed_count": 6032,
        "folder_count": 8,
        "tag_group_count": 4,
        "tag_count": 16,
    },
    "legacy": {
        "schema_version": 6,
        "total_count": 64,
        "active_count": 60,
        "archived_count": 4,
        "legacy_smart_folder_count": 1,
        "legacy_rule_count": 3,
    },
    "distribution_formula": "deterministic-index-modulo/v1",
}
expectations_path = root / "fixture-expectations.json"
expectations_path.write_text(
    json.dumps(expectations, ensure_ascii=False, sort_keys=True, separators=(",", ":")),
    encoding="utf-8",
)

current_source_paths = [str(pathlib.Path(row[1]).resolve()) for row in item_rows]
legacy_source_paths = [str(pathlib.Path(row[1]).resolve()) for row in legacy_rows]


def source_path_hash(paths: list[str]) -> str:
    return hashlib.sha256("\n".join(paths).encode("utf-8")).hexdigest()


def source_paths_inside_fixture(paths: list[str]) -> int:
    count = 0
    for source_path in paths:
        pathlib.Path(source_path).resolve().relative_to(root)
        count += 1
    return count


current_source_path_sha256 = source_path_hash(current_source_paths)
legacy_source_path_sha256 = source_path_hash(legacy_source_paths)
source_path_tree_sha256 = hashlib.sha256(
    f"{current_source_path_sha256}\n{legacy_source_path_sha256}".encode("ascii")
).hexdigest()
all_source_paths = current_source_paths + legacy_source_paths
source_paths_inside_count = source_paths_inside_fixture(all_source_paths)

print(json.dumps({
    "schema_version": 7,
    "total_count": 10128,
    "active_count": 10000,
    "archived_count": 128,
    "display_name_count": 10128,
    "content_hash_count": 10128,
    "missing_count": 512,
    "visual_feature_counts": {
        "analysis_version": "visual-analysis-v2",
        "valid": 3072,
        "failed": 1024,
        "not_analyzed": 6032,
        "feature_rows": 4096,
    },
    "legacy_variant": {
        "schema_version": 6,
        "total_count": 64,
        "active_count": 60,
        "archived_count": 4,
    },
    "source_path_observation": "sqlite-sourcepath-enumeration/v1",
    "source_path_count": len(all_source_paths),
    "source_paths_inside_fixture_count": source_paths_inside_count,
    "source_paths_outside_fixture_count": len(all_source_paths) - source_paths_inside_count,
    "current_source_path_sha256": current_source_path_sha256,
    "legacy_source_path_sha256": legacy_source_path_sha256,
    "source_path_tree_sha256": source_path_tree_sha256,
    "expectations_path": str(expectations_path),
}, ensure_ascii=False, separators=(",", ":")))
