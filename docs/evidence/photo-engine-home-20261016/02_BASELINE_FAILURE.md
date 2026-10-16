# Stage 2 baseline first failure

Initial complete home WPF adapter discovery/selected/results=1626/1626/1626, Passed1602 Failed1 NotExecuted23, no missing. Failed exact method AssetLibraryP3WpfTests.TagManagerRefreshPreservesTheSelectedGroupForConsecutiveTagCreates. Raw first summary/TRX/log retained under artifacts/home-photo/02-baseline-wpf, not overwritten.

Inspection: the existing regression observes !P3TagManagerLoading and the new tag already in ObservableCollection, while AsyncCommand.Execute is still awaiting its operation and has not run finally to release _running. It then asserts next CanExecute immediately; this races the command completion. No persistent production error or wrong selected group has been proven. Preserve the same consecutive creates and group/identity assertions; await CreateP3TagCommand.ExecutionTask and the actual CanExecute state before each next invocation. No repository/UI layout or product AsyncCommand behavior changed; no sleeps or assertion relaxation.

Required full rerun after correction is separate evidence. The original failure remains a true failing test run and blocks approval until complete regression returns. Windows navigation and foreground behavior are NOT_RUN, not inferred from this fix.
