cd src
rm -r bin
rm -r obj
dotnet restore
cd ../

cd tests
rm -r bin
rm -r obj
dotnet restore
cd ../
