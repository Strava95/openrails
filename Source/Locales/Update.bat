@ECHO OFF
REM Script must be run from Locales directory.

PUSHD ..\3rdPartyLibs

IF NOT EXIST GNU.Gettext.Xgettext.exe (
	dotnet build GNU.Gettext.Xgettext/GNU.Gettext.Xgettext.csproj
)

IF NOT EXIST GNU.Gettext.Msgfmt.exe (
	dotnet build GNU.Gettext.Msgfmt/GNU.Gettext.Msgfmt.csproj
)

FOR /D %%M IN (..\Locales\*) DO (
	GNU.Gettext.Xgettext.exe -D ..\%%~nxM --recursive -o ..\Locales\%%~nxM\%%~nxM.pot
	FOR %%L IN (%%M\*.po) DO GNU.Gettext.Msgfmt.exe -l %%~nL -r %%~nxM -d ..\..\Program -L ..\..\Program %%L
)
POPD
IF "%~1"=="" PAUSE
