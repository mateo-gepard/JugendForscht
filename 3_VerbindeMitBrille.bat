@echo off
chcp 65001 >nul 2>&1
echo.
echo === MolekuelVR - Verbinde mit Quest 3 (WiFi) ===
echo.

set "ADB=C:\Program Files\Unity\Hub\Editor\2022.3.52f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
set QUEST_IP=192.168.2.165

echo [1/3] Verbinde mit Quest...
"%ADB%" connect %QUEST_IP%:5555
timeout /t 2 /nobreak >nul

echo [2/3] Port-Forwarding...
"%ADB%" forward tcp:8080 tcp:8080

echo [3/3] Starte App...
"%ADB%" shell am start -n com.DefaultCompany.JugendForscht/com.unity3d.player.UnityPlayerActivity

echo.
echo ==========================================
echo  BEREIT! Browser oeffnet sich...
echo ==========================================
echo.
start "" http://localhost:8080
pause
