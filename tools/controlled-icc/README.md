# Batch reproducibility

Source frozen e230038; product increment instructions tools/ControlledIccWorkflow/README.md. Research dependencies are external pinned artifacts from originalmanifest, not bundled. Photographicfixtures/generatedoutputs stay local (D junction to E), onlyoriginalmathICCprofiles in repository. Use workspace.NET10.0.302 and Zig0.16.0.

1 prepare_photo.py builds exactknownprofiles/directLCMSphotographicderivative (requires priorauthorizedCanonrenderhash and local numpy/tifffile); does not modifyraw. Matrixreference/check scripts reproduce10points/profileXYZD50.
2 Build ControlledIccWorkflow; --explicit-research asREADME; --native-selftest. direct_compare.py <new-resultdir> <workingICC> independently compares allfloatoutputs.
3 raw_rerun.ps1 hashesoriginals then product0.21.1/external0.22.2 process; RTtrue/falsecommands/envisolated in03_RT files. NevercallsharedLibRawindependent.
4 StructureScratchExperiment selftest; modefull/tile/candidate/cancel3boundedrounds; inputCanonhash mandatory, noGC/trim.
5 fullCore with verified_runner --single-process; exactpublishedhostmust be under checkoutancestor becauselegacytests sourcewalk. CopynewpublishedDLLs into separatehostandverifySHAbinding, do not changeactualRelease. WPFsafe5classes16tests; wholeWPFnotrunonactivedesktop.
6 publish originalguardPixelTartVersion2.3.0 with explicitInformationalVersion; newfolder notoverwriteoldRelease. Freeze sourcebefore build. Recursivelyinventoryeveryfile inRELEASE_FILES. verify_delivery.py checksold/newhashes/classification/goldengate/archiveCRC; integritypass notnativeacceptance.

BusinesstimezoneAsia/Shanghai peruser; clienttimezonecapturedseparately. Oldlogs+08unchanged. Packaging/legal/nativegates remainopen. Noinstaller; no userchartpurchase.
