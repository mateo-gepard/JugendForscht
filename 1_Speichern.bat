@echo off
color 0A
echo ========================================================
echo Speichere alle Aenderungen im Projekt...
echo ========================================================
git add .

set /p commitMsg="Bitte gib eine kurze Beschreibung ein (oder druecke Enter fuer 'Auto-Save'): "
if "%commitMsg%"=="" set commitMsg=Zusaetzliche Aenderungen gespeichert

git commit -m "%commitMsg%"
git push

echo.
echo ========================================================
echo Alle Aenderungen wurden erfolgreich sicher gespeichert!
echo ========================================================
pause
