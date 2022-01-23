@ECHO OFF
REM Script must be run from Locales directory.

PUSHD ..\3rdPartyLibs

IF NOT EXIST GNU.Gettext.Xgettext.exe (
	dotnet build GNU.Gettext.Xgettext/GNU.Gettext.Xgettext.csproj
)

FOR /D %%M IN (..\*) DO (
	IF EXIST %%M\%%~nxM.csproj (
		GNU.Gettext.Xgettext.exe -D ..\%%~nxM --recursive -o ..\Locales\%%~nxM\%%~nxM.pot
	)
)

FOR /D %%M IN (..\Contrib\*) DO (
	IF EXIST %%M\%%~nxM.csproj (
		GNU.Gettext.Xgettext.exe -D ..\Contrib\%%~nxM --recursive -o ..\Locales\%%~nxM\%%~nxM.pot
	)
)

FOR /D %%M IN (..\Contrib\ActivityEditor\*) DO (
	IF EXIST %%M\%%~nxM.csproj (
		GNU.Gettext.Xgettext.exe -D ..\Contrib\ActivityEditor\%%~nxM --recursive -o ..\Locales\%%~nxM\%%~nxM.pot
	)
)
POPD

IF "%~1"=="" PAUSE
