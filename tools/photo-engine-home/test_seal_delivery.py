import copy
import pathlib
import tempfile
import unittest
from seal_delivery import check_suite, contained_file


class SealDeliveryTests(unittest.TestCase):
    def fixture(self):
        discovery = [{"fullyQualifiedName": "Exact.Class.Passed"}, {"fullyQualifiedName": "Exact.Class.Skipped"}]
        summary = {"discovered": 2, "selected": 2, "executionPlan": [{"cases": discovery}],
                   "runs": [{"expected": 2, "results": [{"name": "Exact.Class.Passed", "outcome": "Passed"},
                                                       {"name": "Exact.Class.Skipped", "outcome": "NotExecuted"}]}],
                   "counts": {"Passed": 1, "NotExecuted": 1}, "status": "FAILED"}
        return summary, discovery

    def test_integrity_does_not_reclassify_skips_or_acceptance(self):
        summary, discovery = self.fixture()
        before = copy.deepcopy(summary)
        result = check_suite(summary, discovery)
        self.assertEqual(1, result["executed"])
        self.assertEqual("FAILED", result["originalStatus"])
        self.assertEqual(before, summary)

    def test_wrong_identity_with_equal_count_is_rejected(self):
        summary, discovery = self.fixture()
        summary["runs"][0]["results"][1]["name"] = "Other.Class.Skipped"
        with self.assertRaises(ValueError):
            check_suite(summary, discovery)

    def test_counter_and_timeout_mismatch_are_rejected(self):
        for field in ("count", "timeout", "missing"):
            summary, discovery = self.fixture()
            if field == "count": summary["counts"]["Passed"] = 2
            if field == "timeout": summary["runs"][0]["timeout"] = True
            if field == "missing": summary["runs"][0]["results"].pop()
            with self.subTest(field=field), self.assertRaises(ValueError):
                check_suite(summary, discovery)

    def test_only_contained_existing_regular_files_are_admitted(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            (root / "owned.log").write_text("owned")
            (root / "nested").mkdir()
            (root / "nested/owned.log").write_text("owned")
            self.assertEqual(root / "nested/owned.log", contained_file(root, r"nested\owned.log"))
            self.assertEqual(root / "owned.log", contained_file(root, "owned.log"))
            for value in ("../foreign.log", str(root / "owned.log"), "absent.log", "."):
                with self.subTest(value=value), self.assertRaises((ValueError, FileNotFoundError)):
                    contained_file(root, value)


if __name__ == "__main__":
    unittest.main()
