rm -r src/nupkg
source clean.sh
cd src
dotnet pack --configuration Release
cd ..
