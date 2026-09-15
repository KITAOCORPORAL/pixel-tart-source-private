import pathlib
import shutil
import sqlite3
import sys
import uuid


if len(sys.argv) != 3:
    raise RuntimeError("expects: <fresh source fixture database> <fresh output directory>")

source = pathlib.Path(sys.argv[1]).resolve()
output = pathlib.Path(sys.argv[2]).resolve()
if not source.is_file():
    raise RuntimeError(f"source fixture does not exist: {source}")
if output.exists():
    raise RuntimeError(f"output directory must be fresh: {output}")
output.mkdir(parents=True)


def build(size: int) -> None:
    target = output / f"asset-library-{size}.db"
    shutil.copyfile(source, target)
    connection = sqlite3.connect(target)
    try:
        connection.execute("PRAGMA journal_mode=DELETE")
        columns = [row[1] for row in connection.execute("PRAGMA table_info(AssetItems)")]
        rows = connection.execute("SELECT * FROM AssetItems ORDER BY AddedAt,AssetId LIMIT ?", (min(size, 10000),)).fetchall()
        if len(rows) < min(size, 10000):
            raise RuntimeError("source fixture must contain at least 10,000 assets")
        connection.execute("DELETE FROM AssetItems")
        placeholders = ",".join("?" for _ in columns)
        insert = f"INSERT INTO AssetItems({','.join(columns)}) VALUES({placeholders})"
        asset_id = columns.index("AssetId")
        source_path = columns.index("SourcePath")
        normalized = columns.index("NormalizedSourcePath")
        discriminator = columns.index("DuplicateDiscriminator")
        display_name = columns.index("DisplayName")
        batch = []
        for index in range(size):
            values = list(rows[index % len(rows)])
            suffix = f"RC12_SCALE_{size}_{index:06d}"
            path = str(output / "missing-media" / f"{suffix}.jpg")
            values[asset_id] = str(uuid.uuid5(uuid.NAMESPACE_URL, "pixel-tart-" + suffix))
            values[source_path] = path
            values[normalized] = path.upper()
            values[discriminator] = ""
            values[display_name] = f"{suffix} 人像素材.jpg"
            batch.append(tuple(values))
            if len(batch) == 2000:
                connection.executemany(insert, batch)
                batch.clear()
        if batch:
            connection.executemany(insert, batch)
        connection.commit()
        count = connection.execute("SELECT COUNT(*) FROM AssetItems").fetchone()[0]
        if count != size:
            raise RuntimeError(f"fixture count mismatch for {size}: {count}")
        connection.execute("VACUUM")
    finally:
        connection.close()


for fixture_size in (10000, 50000, 100000):
    build(fixture_size)
print(output)
