### Installation

`git submodule update --init --recursive`

### Publish

`dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true`

### Commands

1. Export diff to excel
   `.\ResxManager.Console.exe export-diff "C:\SVN\LenzeEngineering_1.27\Implementation" "C:\Users\xyz\Downloads\2024_09_30.snapshot"`
   a. first argument is path to all resx
