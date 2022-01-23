@ECHO OFF
REM Script must be run from Locales directory.

PUSHD ..\3rdPartyLibs

IF NOT EXIST GNU.Gettext.Msgfmt.exe (
	dotnet build GNU.Gettext.Msgfmt/GNU.Gettext.Msgfmt.csproj
)

FOR /D %%M IN (..\Locales\*) DO (
	FOR %%L IN (%%M\*.po) DO GNU.Gettext.Msgfmt.exe -l %%~nL -r %%~nxM -d ..\..\Program -L ..\..\Program %%L
)
POPD
IF "%~1"=="" PAUSE
