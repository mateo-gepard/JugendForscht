@echo off
color 0B
echo ========================================================
echo Oeffne JugendForscht Projekt...
echo ========================================================

:: 1. Oeffnet das Projekt in Visual Studio Code (falls installiert)
code . 2>nul

:: 2. Oeffnet den Projektordner im Windows Explorer
start .

:: 3. Öffnet Unity Hub (Standardpfad)
if exist "C:\Program Files\Unity Hub\Unity Hub.exe" (
    echo Starte Unity Hub...
    start "" "C:\Program Files\Unity Hub\Unity Hub.exe"
) else (
    echo Unity Hub wurde nicht im Standardpfad gefunden.
    echo Bitte starte Unity und waehle den Projektordner manuell aus.
)

echo.
echo ========================================================
echo Projektordner und Editoren wurden geoeffnet!
echo ========================================================
pause
