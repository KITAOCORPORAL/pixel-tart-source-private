# Reference Match V4 protection behavior

The synthetic protection sweep runs neutral, skin, highlight and shadow protection from 0% through 100% and writes measured rows to `PROTECTION_BEHAVIOR_REPORT.json`. The current run produced five valid rows. Skin-candidate delta changed from 0.0455 at 0% to 0.0371 at 100% on the fixture; this confirms a measurable protection path. Neutral, highlight and shadow metrics are recorded for review rather than converted into a universal quality claim from one fixture.

Future acceptance should repeat the sweep over the full quality corpus and define thresholds for each scene class.
