param([Parameter(Mandatory=$true)][string]$ResearchRoot)
$ErrorActionPreference='Stop'
# Setup is intentionally explicit: use separately downloaded, hash-recorded tools.
$root=[IO.Path]::GetFullPath($ResearchRoot)
$src=Join-Path $root 'source2';$build=Join-Path $root 'build-vulkan'
if((git -C $src rev-parse HEAD) -ne 'c1447c6ab618fa4b1cb11fd2fe12826855007059'){throw 'Wrong engine source'}
$cmake=Join-Path $root 'pytools/cmake/data/bin/cmake.exe';$ctest=Join-Path $root 'pytools/cmake/data/bin/ctest.exe';$tool=Join-Path $root 'toolchain/llvm-mingw-20250402-ucrt-x86_64/bin'
& $cmake -S $src -B $build -G Ninja -DCMAKE_BUILD_TYPE=Release "-DCMAKE_CXX_COMPILER=$tool/clang++.exe" "-DCMAKE_C_COMPILER=$tool/clang.exe" "-DCMAKE_MAKE_PROGRAM=$root/pytools/bin/ninja.exe" -DBUILD_TESTING=ON -DSPEKTRALAB_REQUIRE_VULKAN=ON -DSPEKTRALAB_BUILD_TEST_DLL=ON -DSPEKTRALAB_BUILD_NATIVE_RAW=OFF -DSPEKTRALAB_BUILD_DESKTOP=OFF "-DSPEKTRALAB_VULKAN_HEADERS=$root/Vulkan-Headers/include" "-DSPEKTRALAB_GLSLANG=$root/glslang/bin/glslang.exe"
if($LASTEXITCODE -ne 0){throw 'Configure failed'}
& $cmake --build $build --parallel 4
if($LASTEXITCODE -ne 0){throw 'Build failed'}
Get-ChildItem -LiteralPath $tool -Filter '*.dll' | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $build 'engine')}
& $ctest --test-dir $build --output-on-failure
if($LASTEXITCODE -ne 0){throw 'CTest failed; retain failure, do not raise tolerance'}
