#!/bin/bash

LIBGIT2SHA=`cat ./LibGit2Sharp/libgit2_hash.txt`
SHORTSHA=${LIBGIT2SHA:0:7}
EXTRADEFINE="$1"

cd libgit2
git fetch origin refs/pull/2798/head:refs/remotes/pr/2798
git checkout b66d909063c76e1606b56d9256ab40c1c1a383c8
cd ..

rm -rf libgit2/build
mkdir libgit2/build
pushd libgit2/build
export _BINPATH=`pwd`

cmake -DCMAKE_BUILD_TYPE:STRING=RelWithDebInfo \
      -DBUILD_CLAR:BOOL=OFF \
      -DUSE_SSH=OFF \
      -DENABLE_TRACE=ON \
      -DLIBGIT2_FILENAME=git2-$SHORTSHA \
      -DCMAKE_OSX_ARCHITECTURES="i386;x86_64" \
      ..
cmake --build .

export LD_LIBRARY_PATH=$_BINPATH:$LD_LIBRARY_PATH
export DYLD_LIBRARY_PATH=$_BINPATH:$DYLD_LIBRARY_PATH

popd

export MONO_OPTIONS=--debug

echo $DYLD_LIBRARY_PATH
echo $LD_LIBRARY_PATH

# Required for NuGet package restore to run.
mozroots --import --sync

mono Lib/NuGet/NuGet.exe restore LibGit2Sharp.sln
xbuild CI/build.msbuild /target:Deploy /property:ExtraDefine="$EXTRADEFINE"

exit $?
