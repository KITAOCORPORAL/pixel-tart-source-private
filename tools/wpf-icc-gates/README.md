# WPF/ICC gate repro

PrivateWpfHost createsnoninteractiveuniqueWinSta anddesktop; nevercallsSwitchDesktop orinputAPIs. Runworkspace.NET10.0.302host with absolutePython, private_suite.py, host-report.json then --assembly/--output/--resource-root [--classes...] args. Childrefusesinteractivestation; WPFclassprocessisolationpreserved. No sharedclipboard because separateWindowStation. NotphysicalDPI/input/monitorapproval.

Example (shell paths adjusted to checkout, notassumedD):

    dotnet tools/PrivateWpfHost/bin/Release/net10.0-windows/PrivateWpfHost.dll <python> tools/wpf-icc-gates/private_suite.py <newhostreport.json> --assembly <actualpublishedtesthost/RAWSelectionAssistant.WpfTests.dll> --output <newrun> --resource-root <checkout>

BuildWPF/tests/VerifiedTestRunner, copytesthosttonewC/Eroot, replaceproductDLLsonlyfromsealedRelease, hashbindallcomponents. Do notmodifyactualRelease. Fullsuite212classes/1626tests,23conditionalexternal/manualcasesnotPassed. First34failures retained; fixesrestorefileparts/designsubdirectory andnewreportoutputpath, notassertionweakening.

ICC domain_audit.py hash-renderedphoto+profiles/floatrangestats; extended_compare.py needscompiledextended_refs.cpp againstfixedskcms97adc... andexactexternalLCMS2.16, noDLLincluded. V2tools requirelocalnumpy/tifffile/Pillow, exactexternalenginehash. run_v2_suite.py generatesgray/saturated/skin/highlightsfloatfixtures and fullCanonphotographicderivative, writesbefore-mapfloatTIFF and explicitlossyExportClip0To1V2. ThisisnotperceptualmappinganddoesnotchangeV1. Recordallclippedpixels/readbackprofiles/recipeidentities. Runtime/artifactsnotGit.

Prepare-NativeAcceptance.ps1 defaultonlyhashes/prepares; -LaunchWhenIdle reserveddesktoponly>=10minidle, notproofdesktopreserved. No automateclicks or DPIsettings. ActualhumanmuststoponuserinputUNKNOWN; WPS/backend/AX/screenshotvisualevidencedistinct. No recurringdesktopprobes.

native_package_audit.py readsoriginalcondapackageinfo withlocalzstandard0.25.0; pe_audit.py import+delaytable. MinimalPATHcurrenthostsmoke notcleanVM, so binarybundlingentrygate remainsCLOSED. OriginalMITnotice/identityincludedinevidence, notsubstituteforlicensingreview. NoSonycandidatebundle/Structureproductionchange. CalibrationEligible=false.
