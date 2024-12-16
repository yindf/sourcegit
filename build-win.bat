notepad VERSION
notepad version.json
dotnet publish -c Release -r win-x64 -o ./build src/SourceGit.csproj
cd build
del build.zip
tar -a -c -f build.zip av_libglesv2.dll libHarfBuzzSharp.dll libonigwrap.dll libSkiaSharp.dll README.md SourceGit.exe SourceGit.pdb