# 3. Systementwicklung

Die Entwicklung des VR-Lernsystems für Molekülgeometrien stellte eine interdisziplinäre Herausforderung dar, die Aspekte der Softwareentwicklung, Hardwareintegration, chemiedidaktischen Konzeption und User-Experience-Optimierung vereinte. Im Folgenden werden die einzelnen Entwicklungsbereiche detailliert erläutert, wobei besonderer Wert auf die Begründung von Designentscheidungen, aufgetretene Probleme und deren iterative Lösung gelegt wird.

---

## 3.1 Hardware-Setup (Quest 3, iPad-UI, Tracking)

### 3.1.1 Wahl der VR-Hardware: Meta Quest 3

Die Entscheidung für die Meta Quest 3 als Zielplattform basierte auf mehreren technischen und praktischen Überlegungen:

**Standalone-Fähigkeit:** Im Gegensatz zu PC-gebundenen VR-Systemen (z.B. HTC Vive, Valve Index) ermöglicht die Quest 3 einen kabellosen Betrieb ohne externe Rechnerinfrastruktur. Dies war für den schulischen Einsatz essentiell, da weder leistungsstarke Gaming-PCs noch komplexe Verkabelungen in Klassenzimmern vorausgesetzt werden können. Die Standalone-Architektur reduziert zudem potenzielle Fehlerquellen und verkürzt die Setup-Zeit erheblich.

**Mixed-Reality-Passthrough:** Die Quest 3 verfügt über hochauflösende Farbkameras, die eine Echtzeit-Durchsicht der physischen Umgebung ermöglichen (Passthrough-Technologie). Diese Funktion war für das pädagogische Konzept von zentraler Bedeutung: Lernende sollten nicht vollständig von ihrer Umgebung isoliert werden, sondern die virtuellen Molekülmodelle in ihrem realen Lernkontext betrachten können. Die Integration wurde über die `PassthroughManager`-Klasse realisiert, die den Passthrough-Layer zur Laufzeit aktiviert und konfiguriert.

**Inside-Out-Tracking:** Die Quest 3 verwendet sechs integrierte Kameras für das Positions-Tracking ohne externe Sensoren (Inside-Out-Tracking). Dies eliminiert die Notwendigkeit von Basisstationen und ermöglicht eine flexible Nutzung in verschiedenen Räumlichkeiten. Die Tracking-Genauigkeit erwies sich für die Manipulation von Molekülmodellen als ausreichend präzise.

**Problemstellung und Iteration:** Initial traten Probleme mit dem Clipping von 3D-Objekten auf, wenn diese zu nah an die Kamera gebracht wurden. Die Standard-Near-Clipping-Plane der Unity-Kamera war für Desktop-Anwendungen optimiert und schnitt Objekte im Nahbereich ab. Dies wurde durch Anpassung der `VRClippingDebugger`-Komponente behoben, die die Near-Clipping-Plane dynamisch auf 0.01m reduziert – ein Wert, der physisch sinnvoll ist, da Objekte näher als 1cm am Auge nicht mehr fokussiert werden können.

### 3.1.2 iPad als Operator-Interface

Parallel zur VR-Anwendung wurde ein Tablet-basiertes Kontrollsystem entwickelt, das es einer Lehrkraft ermöglicht, die VR-Erfahrung von außen zu steuern.

**Technische Umsetzung:** Die Kommunikation zwischen iPad und Quest 3 erfolgt über einen WebSocket-Server, der in der Unity-Anwendung implementiert wurde (`WebSocketServer.cs`). Das iPad verbindet sich über eine Web-Oberfläche mit dem Server und kann Befehle wie Molekülauswahl, Tutorial-Navigation und Systemsteuerung übermitteln.

**Begründung:** Das Operator-Konzept adressiert mehrere pädagogische Anforderungen:
1. Lehrkräfte können den Unterrichtsverlauf steuern, ohne selbst ein VR-Headset tragen zu müssen
2. Synchrone Lernerfahrungen werden ermöglicht, bei denen alle Schüler dasselbe Molekül betrachten
3. Die URL-Anzeige (`TabletURLDisplay.cs`) zeigt die Verbindungsadresse direkt im VR-Interface an, was die Erstverbindung vereinfacht

**Iterationen:** Die erste Implementierung verwendete HTTP-Polling, was zu spürbaren Latenzen führte. Die Umstellung auf WebSockets ermöglichte eine nahezu verzögerungsfreie bidirektionale Kommunikation mit Latenzzeiten unter 50ms im lokalen Netzwerk.

### 3.1.3 Tracking-Konfiguration

Die Tracking-Konfiguration erfolgt über die XR-Plugin-Architektur von Unity in Kombination mit dem Meta XR SDK:

- **OculusLoader.asset:** Konfiguriert den Oculus-spezifischen XR-Loader für Quest-Hardware
- **OpenXRLoader.asset:** Bietet Fallback-Unterstützung für andere OpenXR-kompatible Geräte
- **XRGeneralSettingsPerBuildTarget.asset:** Definiert plattformspezifische XR-Einstellungen

Die Tracking-Einstellungen wurden iterativ optimiert, um einen Kompromiss zwischen Tracking-Genauigkeit und Rechenleistung zu finden. Die finale Konfiguration nutzt 6DoF-Tracking (Six Degrees of Freedom) für Kopf und Hände bei einer Aktualisierungsrate von 72Hz, was der nativen Bildwiederholrate der Quest 3 entspricht.

---

## 3.2 Interaktion (Finger- und Controller-Tracking)

### 3.2.1 Dual-Input-Strategie

Die Anwendung unterstützt sowohl Controller-basierte als auch Hand-Tracking-basierte Interaktion. Diese Entscheidung wurde aus folgenden Gründen getroffen:

**Hand-Tracking (primär):** Die Quest 3 verfügt über fortschrittliches Hand-Tracking, das individuelle Fingerbewegungen erkennt. Für die intuitive Manipulation von Molekülmodellen bietet dies erhebliche Vorteile: Nutzer können Moleküle "greifen" und drehen, als würden sie ein physisches Modell in den Händen halten. Die natürliche Interaktion senkt die Einstiegshürde für VR-Neulinge erheblich.

**Controller-Tracking (sekundär):** Controller bieten präzisere Eingaben und haptisches Feedback durch Vibrationsmotoren. Für Nutzer, die Schwierigkeiten mit dem Hand-Tracking haben, oder in Situationen mit schwierigen Lichtverhältnissen (Hand-Tracking basiert auf Kamerabildern), stellen Controller eine zuverlässige Alternative dar.

### 3.2.2 Implementierung der Molekülrotation

Die Rotation von Molekülmodellen wurde in der `MoleculeRotationController.cs`-Klasse implementiert:

**Greif-Geste:** Ein Molekül wird "gegriffen", wenn eine Pinch-Geste (Daumen und Zeigefinger zusammen) oder der Grip-Button eines Controllers erkannt wird. Der `XR Interaction Toolkit` von Unity stellt hierfür standardisierte Events bereit (`selectEntered`, `selectExited`).

**Rotationsberechnung:** Die Rotation erfolgt relativ zur Handbewegung. Die Differenz zwischen der aktuellen und der vorherigen Handorientierung wird auf das Molekül übertragen. Dies wurde in mehreren Iterationen verfeinert:

- *Iteration 1:* Direkte Übertragung der Handrotation führte zu ruckartigen Bewegungen
- *Iteration 2:* Einführung eines Smoothing-Algorithmus (Lerp mit Faktor 0.15) für flüssigere Bewegungen
- *Iteration 3:* Implementierung einer "Dead Zone" von 2°, um unbeabsichtigte Mikrorotationen durch Hand-Zittern zu filtern

**Positions-Lock:** Die `MoleculePositionLock.cs`-Komponente verhindert, dass Moleküle beim Rotieren versehentlich verschoben werden. Die Position wird beim Greifen fixiert und erst beim Loslassen wieder freigegeben.

### 3.2.3 Hand-Rotation-Controller

Der `HandRotationController.cs` erweitert die Interaktionsmöglichkeiten um eine zweihändige Manipulation:

**Zwei-Hand-Skalierung:** Wenn beide Hände ein Molekül greifen, wird der Abstand zwischen den Händen zur Skalierung des Moleküls verwendet. Dies ermöglicht ein intuitives "Auseinanderziehen" zum Vergrößern.

**Problemstellung:** Initial führte die gleichzeitige Rotation und Skalierung zu inkonsistentem Verhalten. Die Lösung bestand in einer Priorisierung: Bei Zwei-Hand-Interaktion wird ausschließlich skaliert, während Rotation nur bei Ein-Hand-Interaktion aktiv ist.

### 3.2.4 Ray-Interaktion für UI-Elemente

Für die Interaktion mit 2D-UI-Elementen (Buttons, Menüs) wurde eine Ray-basierte Interaktion implementiert:

**Technische Umsetzung:** Von der Handposition ausgehend wird ein virtueller Strahl projiziert. Der Schnittpunkt mit UI-Elementen wird berechnet und visuell durch einen Laser-Pointer dargestellt. Die `RayInteractorDebug.cs`-Komponente diente während der Entwicklung zur Visualisierung und Fehlerdiagnose des Ray-Castings.

**Begründung:** Während direktes Berühren für 3D-Objekte intuitiv ist, eignen sich Rays besser für UI-Elemente, da diese oft außerhalb der direkten Reichweite positioniert sind und präzisere Selektion erfordern.

---

## 3.3 Molekülbibliothek und Suchfunktion

### 3.3.1 Architektur der Moleküldatenbank

Die Molekülbibliothek wurde als hierarchisches System konzipiert:

**ElementData.cs:** Definiert die Grundeigenschaften chemischer Elemente (Symbol, Name, Atommasse, Atomradius, CPK-Farbe nach dem Corey-Pauling-Koltun-Farbschema). Diese Daten sind als ScriptableObjects implementiert, was eine einfache Bearbeitung im Unity-Editor ermöglicht.

**ElementDatabase.cs:** Aggregiert alle ElementData-Objekte zu einer durchsuchbaren Datenbank. Die `MainElementDatabase.asset` enthält Einträge für die im Chemieunterricht relevanten Elemente (H, C, N, O, F, B, Cl, etc.).

**MoleculeData.cs:** Beschreibt vollständige Moleküle mit:
- Liste der enthaltenen Atome und deren 3D-Positionen
- Bindungsinformationen (Einfach-, Doppel-, Dreifachbindungen)
- Molekülgeometrie-Klassifikation (linear, gewinkelt, trigonal-planar, trigonal-pyramidal, tetraedrisch)
- Optionale Metadaten (IUPAC-Name, Summenformel, Strukturformel)

**MoleculeLibrary.cs:** Verwaltet die Sammlung aller verfügbaren Moleküle und stellt Suchmethoden bereit.

### 3.3.2 PubChem-API-Integration

Um über die vordefinierten Moleküle hinaus flexibel auf chemische Strukturdaten zugreifen zu können, wurde eine Integration mit der PubChem-Datenbank des NIH (National Institutes of Health) implementiert:

**PubChemAPI.cs:** Diese Klasse kapselt HTTP-Anfragen an die PubChem-REST-API. Moleküle können nach Name, Summenformel oder CID (Compound Identifier) gesucht werden. Die API liefert Strukturdaten im SDF-Format (Structure Data File).

**SDFParser.cs:** Parst SDF-Dateien und extrahiert:
- Atomkoordinaten (x, y, z)
- Atomtypen (Elementnummern)
- Bindungsmatrix (welche Atome sind verbunden, Bindungsordnung)

**Problemstellungen und Lösungen:**

1. *Asynchrone Anfragen:* HTTP-Anfragen blockieren den Hauptthread und würden die VR-Erfahrung unterbrechen. Lösung: Verwendung von Unity Coroutines (`IEnumerator`) für nicht-blockierende Netzwerkanfragen.

2. *3D-Koordinaten-Transformation:* PubChem liefert Koordinaten in Ångström (10⁻¹⁰ m), während Unity Meter verwendet. Zusätzlich musste das Koordinatensystem angepasst werden (PubChem: Y-up, rechthändig; Unity: Y-up, linkshändig).

3. *Fehlerbehandlung:* Netzwerkfehler, ungültige Molekülnamen und Timeout-Situationen werden abgefangen und dem Nutzer verständlich kommuniziert.

### 3.3.3 Molekül-Rendering

Der `MoleculeRenderer.cs` ist für die visuelle Darstellung der Molekülstrukturen verantwortlich:

**Ball-and-Stick-Modell:** Atome werden als Kugeln dargestellt, deren Radius proportional zum Atomradius ist. Bindungen werden als Zylinder zwischen den Atompositionen gerendert.

**Shader-Entwicklung:** Der custom `MoleculeUnlit.shader` wurde entwickelt, um eine einheitliche, nicht-beleuchtungsabhängige Darstellung zu gewährleisten. Dies verhindert Schatteneffekte, die bei der Betrachtung aus verschiedenen Winkeln die Farbwahrnehmung verfälschen könnten.

**Farbcodierung:** Die CPK-Farbkonvention wird verwendet:
- Wasserstoff: Weiß
- Kohlenstoff: Grau/Schwarz
- Stickstoff: Blau
- Sauerstoff: Rot
- Fluor: Grün
- Bor: Rosa

**Performance-Optimierung:** Für komplexe Moleküle mit vielen Atomen wurde ein LOD-System (Level of Detail) implementiert. Ab einer Entfernung von 2m werden vereinfachte Geometrien verwendet, um die Framerate stabil bei 72 FPS zu halten.

### 3.3.4 Molekül-Suchanzeige

Die `MoleculeSearchUI.cs` implementiert eine VR-native Suchoberfläche:

**Virtuelle Tastatur:** Eine in der VR-Umgebung schwebende Tastatur ermöglicht die Texteingabe durch Zeigen und Auswählen. Die Anordnung entspricht dem QWERTZ-Layout für deutschsprachige Nutzer.

**Autovervollständigung:** Nach Eingabe von mindestens drei Zeichen werden passende Molekülnamen aus der lokalen Datenbank vorgeschlagen. Dies reduziert Tippfehler und beschleunigt die Navigation.

**Ergebnisanzeige:** Gefundene Moleküle werden als interaktive 3D-Vorschaukarten dargestellt, die bei Auswahl das vollständige Molekülmodell laden.

---

## 3.4 VR-Tutorial und Unterrichtsmodul

### 3.4.1 Didaktisches Konzept

Das Tutorial-System wurde nach konstruktivistischen Lernprinzipien entwickelt:

**Scaffolding:** Der Lerninhalt wird in aufeinander aufbauende Schritte gegliedert, wobei jeder Schritt auf dem Vorwissen des vorherigen aufbaut. Die Komplexität steigt graduell von einfachen Bindungstypen zu komplexen Molekülgeometrien.

**Aktives Lernen:** Passive Informationsaufnahme wird durch interaktive Elemente ergänzt. Lernende können Moleküle manipulieren, Bindungswinkel selbst messen und Hypothesen durch Exploration überprüfen.

**Multimodale Präsentation:** Informationen werden visuell (3D-Modelle, Texteinblendungen), auditiv (Erklärvideos) und kinästhetisch (Manipulation) präsentiert, um verschiedene Lerntypen anzusprechen.

### 3.4.2 Tutorial-Architektur

**TutorialStep.cs (ScriptableObject):** Jeder Tutorial-Schritt ist als ScriptableObject definiert mit:
- Titel und Beschreibungstext
- Zugehöriges Video (VideoClip-Referenz)
- Liste der anzuzeigenden Molekül-Prefabs
- Optionale Highlight-Bereiche (z.B. Winkelmarkierungen)
- Bedingungen für den Fortschritt zum nächsten Schritt

**TutorialManager.cs:** Koordiniert den Ablauf des Tutorials:
- Lädt und entlädt Schritte dynamisch
- Verwaltet den Video-Player (`VideoPlayer`-Komponente)
- Synchronisiert Molekül-Spawning mit Video-Timestamps
- Implementiert Vorwärts/Rückwärts-Navigation

**TutorialEvent.cs:** Definiert Events, die während des Tutorials ausgelöst werden können (OnStepStarted, OnStepCompleted, OnMoleculeSpawned), um externe Systeme zu benachrichtigen.

### 3.4.3 Video-Integration

Die Integration von Erklärvideos stellte eine besondere technische Herausforderung dar:

**Chroma-Key-Shader:** Videos wurden vor einem Greenscreen aufgenommen. Der `ChromaKeyShader.shader` entfernt die grüne Hintergrundfarbe in Echtzeit, sodass Videos als schwebende Präsentation in der VR-Umgebung erscheinen. Die Farbtoleranz wurde empirisch auf einen HSV-Bereich von 100-140° (Grün) mit einer Toleranz von ±15° optimiert.

**Render-Texture-Pipeline:** Videos werden nicht direkt auf UI-Elemente gerendert, sondern zunächst in eine `TutorialVideoRT.renderTexture` geschrieben. Diese Textur wird dann auf eine 3D-Plane im Raum projiziert. Dieser Umweg ermöglicht:
- Anwendung von Post-Processing-Effekten (Chroma-Key)
- Flexible Positionierung und Skalierung der Video-Plane
- Unabhängigkeit von der UI-Render-Pipeline

**Problemstellung Video-Material:** Ein persistentes Problem war das "Pink-Material"-Phänomen: Nach dem Laden einer neuen Szene verlor die Video-Plane ihre Material-Referenz und erschien pink (Unity's Fallback für fehlende Shader). Die Lösung bestand darin, das Material als Asset zu speichern (`TutorialVideoMat.mat`) statt es zur Laufzeit zu generieren, sowie eine explizite Material-Zuweisung in `Awake()` zu erzwingen.

### 3.4.4 Implementierte Tutorial-Schritte

Die Tutorial-Sequenz umfasst neun Schritte, die in `Assets/Tutorial/Steps/` als ScriptableObjects gespeichert sind:

| Schritt | Datei | Inhalt |
|---------|-------|--------|
| 1 | Step01_BondIntroduction.asset | Einführung in Bindungstypen (Einfach-, Doppel-, Dreifachbindung) |
| 2 | Step02_MethanKeilstrich.asset | Keilstrich-Schreibweise am Beispiel Methan |
| 3 | Step03_Molekuelgeometrie.asset | Konzept der räumlichen Molekülstruktur |
| 4 | Step04_LinearerBau.asset | Linearer Molekülbau (CO₂, 180°) |
| 5 | Step05_GewinkelterBau.asset | Gewinkelter Bau (H₂O, 104.5°) |
| 6 | Step06_TrigonalPlanar.asset | Trigonal-planare Geometrie (BF₃, 120°) |
| 7 | Step07_TrigonalPyramidal.asset | Trigonal-pyramidale Geometrie (NH₃, 107°) |
| 8 | Step08_Tetraedrisch.asset | Tetraedrische Geometrie (CH₄, 109.5°) |
| 9 | Step09_Abschluss.asset | Zusammenfassung und Abschlusstest |

### 3.4.5 Dynamisches Molekül-Spawning

Die Positionierung der Molekülmodelle während des Tutorials wurde iterativ optimiert:

**Initiale Implementierung:** Moleküle wurden an festen Weltkoordinaten gespawnt. Problem: Bei unterschiedlichen Startpositionen der Nutzer waren Moleküle möglicherweise außerhalb des Sichtfeldes.

**Iteration 2:** Moleküle werden relativ zur Kamera-Position gespawnt (0.6m vor dem Nutzer). Problem: Bei schnellen Kopfbewegungen "verfolgten" Moleküle den Nutzer, was Desorientierung verursachte.

**Finale Lösung:** Moleküle werden bei Step-Beginn an einer Position 0.35m vor der aktuellen Blickrichtung gespawnt und dort fixiert. Die `MoleculePlaneAlignment.cs`-Komponente richtet planare Moleküle automatisch zur Kamera aus, sodass sie optimal sichtbar sind. Zusätzlich wurde eine Outline-Visualisierung für die Molekülebene implementiert, um die räumliche Ausdehnung zu verdeutlichen.

**Überlappungsvermeidung:** Die Methode `GetNonOverlappingPosition()` prüft vor dem Spawning, ob die Zielposition mit bereits vorhandenen Molekülen oder UI-Elementen kollidiert, und verschiebt das neue Objekt gegebenenfalls.

### 3.4.6 Winkel-Visualisierung

Für das Verständnis von Bindungswinkeln wurde ein visuelles Hilfssystem entwickelt:

**Winkel-Indikator:** Die `CreateAngleIndicator()`-Methode erzeugt einen bogenförmigen Mesh zwischen zwei Bindungen, der den Winkel visuell hervorhebt. Der Bogen wird prozedural generiert und passt sich dynamisch an verschiedene Winkelgrößen an.

**Material-Persistenz:** Das Material für Winkelmarkierungen (`TutorialAngleMaterial.mat`) wird als Asset gespeichert, um das Pink-Material-Problem zu vermeiden (siehe 3.4.3).

**Beschriftung:** Winkelwerte werden als TextMeshPro-Elemente in der Nähe des Bogens angezeigt. Die `FixOverlappingLabels()`-Methode verhindert, dass sich Beschriftungen gegenseitig überdecken.

---

## 3.5 Operator-Konzept und Stabilität

### 3.5.1 Rationale des Operator-Konzepts

Das Operator-Konzept adressiert eine fundamentale Herausforderung des VR-Einsatzes im Unterricht: Die Lehrkraft muss den Lernprozess beobachten und steuern können, ohne selbst in der virtuellen Realität "gefangen" zu sein.

**Pädagogische Begründung:**
1. *Klassenmanagement:* Lehrkräfte müssen das Verhalten aller Schüler im Blick behalten, auch wenn diese VR-Headsets tragen
2. *Synchronisation:* Gemeinsame Lernerfahrungen erfordern, dass alle Schüler denselben Inhalt sehen
3. *Intervention:* Bei Verständnisproblemen muss die Lehrkraft gezielt eingreifen können
4. *Assessment:* Die Beobachtung des Lernfortschritts erfordert Einblick in die Nutzeraktionen

### 3.5.2 WebSocket-Server-Implementierung

Der WebSocket-Server (`WebSocketServer.cs`) bildet das Rückgrat der Operator-Kommunikation:

**Architektur:** Der Server läuft als separater Thread innerhalb der Unity-Anwendung und lauscht auf Port 8080. Verbindungen werden über das WebSocket-Protokoll (RFC 6455) etabliert, das Full-Duplex-Kommunikation über eine einzelne TCP-Verbindung ermöglicht.

**Befehlsprotokoll:** Befehle werden als JSON-Objekte übertragen:
```json
{
  "command": "loadMolecule",
  "parameters": {
    "moleculeName": "methane",
    "position": [0, 1.5, 2]
  }
}
```

**Implementierte Befehle:**
- `loadMolecule`: Lädt ein Molekül in die Szene
- `nextStep` / `previousStep`: Tutorial-Navigation
- `resetPosition`: Setzt Molekülpositionen zurück
- `togglePassthrough`: Aktiviert/deaktiviert Passthrough
- `getStatus`: Fragt den aktuellen Systemzustand ab

**Thread-Sicherheit:** Da Unity nicht thread-safe ist, werden eingehende Befehle in eine Queue eingereiht und im Hauptthread (via `Update()`) verarbeitet. Dies verhindert Race Conditions und Abstürze.

### 3.5.3 URL-Display-System

Die `TabletURLDisplay.cs`-Komponente löst ein praktisches Problem: Wie erfährt die Lehrkraft die IP-Adresse des VR-Headsets, um sich zu verbinden?

**Lösung:** Die lokale IP-Adresse wird automatisch ermittelt und als QR-Code sowie als lesbarer Text im VR-Interface angezeigt. Die Lehrkraft kann diesen Code mit dem Tablet scannen, um die Verbindung herzustellen.

**Netzwerk-Discovery:** Für Netzwerke mit dynamischer IP-Vergabe wurde ein mDNS-basierter Discovery-Mechanismus implementiert, der das VR-Headset unter einem festen Hostnamen (z.B. `vr-classroom.local`) erreichbar macht.

### 3.5.4 Stabilitätsmaßnahmen

Die Stabilität des Systems wurde durch mehrere Maßnahmen sichergestellt:

**Fehlerbehandlung:**
- Try-Catch-Blöcke um kritische Operationen (Netzwerk, Datei-I/O)
- Graceful Degradation: Bei Verbindungsverlust arbeitet die VR-Anwendung standalone weiter
- Automatischer Reconnect: Der WebSocket-Client versucht alle 5 Sekunden, die Verbindung wiederherzustellen

**Memory Management:**
- Explizites Cleanup von Molekül-GameObjects beim Szenenwechsel (`ClearPlane()`-Methode)
- Object Pooling für häufig instanziierte Objekte (Atome, Bindungen)
- Texture-Komprimierung für Video-Assets zur Reduzierung des GPU-Speicherverbrauchs

**Performance-Monitoring:**
- Die `RendererDebugger.cs`-Komponente protokolliert Frame-Zeiten und warnt bei Drops unter 72 FPS
- Automatische LOD-Anpassung bei Performance-Problemen
- Shader-Fallbacks für ältere GPU-Architekturen

### 3.5.5 Iterationen und Problembehebung

Die Entwicklung durchlief mehrere Iterationen, in denen kritische Probleme identifiziert und behoben wurden:

**Problem 1: VR-Plane nicht sichtbar**
*Symptom:* Die Molekülebene (Outline) war in manchen Situationen unsichtbar.
*Ursache:* Die Berechnung der Molekülgröße (`moleculeRadius`) ergab bei einatomigen Molekülen 0, was zu einer Outline mit Größe 0 führte.
*Lösung:* Implementierung eines Mindestradius von 0.1m und Fallback-Logik.

**Problem 2: Text-Überlappung**
*Symptom:* Beschriftungen mehrerer Moleküle oder Winkel überlagerten sich und waren unleserlich.
*Ursache:* Keine Kollisionserkennung bei der Text-Positionierung.
*Lösung:* Implementierung von `FixOverlappingLabels()`, die alle TextMeshPro-Komponenten in der Szene findet und überlappende Elemente verschiebt.

**Problem 3: Prefab-Positionierung**
*Symptom:* Molekül-Prefabs erschienen zu weit rechts vom Video-Player.
*Ursache:* Fester X-Offset, der nicht zur Videoposition passte.
*Lösung:* Reduzierung des X-Offsets um 50% und Berechnung relativ zur Video-Plane-Position.

**Problem 4: WebSocket-Verbindungsabbrüche**
*Symptom:* Die Verbindung zum Operator-Tablet brach nach einigen Minuten Inaktivität ab.
*Ursache:* TCP-Keep-Alive war nicht konfiguriert, und NAT-Router schlossen inaktive Verbindungen.
*Lösung:* Implementierung von Ping-Pong-Frames alle 30 Sekunden gemäß WebSocket-Protokoll.

**Problem 5: Shader-Kompilierung auf Quest**
*Symptom:* Pink-Material auf der Quest, während es im Editor funktionierte.
*Ursache:* Der Shader verwendete Desktop-spezifische Features, die auf der mobilen GPU (Adreno) nicht verfügbar waren.
*Lösung:* Entwicklung des `MoleculeUnlit.shader` mit explizitem Mobile-Target und Fallback-Subshader.

### 3.5.6 Build-Konfiguration

Der Build-Prozess wurde durch `BuildSetup.cs` im Editor automatisiert:

**Android-Konfiguration:** Die Quest 3 läuft auf einer Android-Basis. Die Build-Einstellungen umfassen:
- Minimum API Level: Android 10 (API 29)
- Target API Level: Android 12 (API 31)
- Scripting Backend: IL2CPP für bessere Performance
- Architecture: ARM64

**XR-Plug-ins:** Die Build-Pipeline aktiviert automatisch die korrekten XR-Loader (Oculus, OpenXR) basierend auf der Zielplattform.

**Asset-Bundles:** Video-Assets werden als separate Asset-Bundles gebaut, um die initiale APK-Größe zu reduzieren. Downloads erfolgen bei Bedarf über den StreamingAssets-Ordner.

---

## Zusammenfassung

Die Systementwicklung des VR-Lernsystems für Molekülgeometrien war ein iterativer Prozess, der technische Herausforderungen mit pädagogischen Anforderungen in Einklang bringen musste. Die Wahl der Meta Quest 3 als Plattform ermöglichte einen flexiblen, kabellosen Einsatz im Klassenzimmer. Die Dual-Input-Strategie (Hand-Tracking und Controller) gewährleistet Zugänglichkeit für verschiedene Nutzergruppen. Die Molekülbibliothek mit PubChem-Integration bietet sowohl vorgefertigte Inhalte als auch Erweiterbarkeit. Das Tutorial-System verbindet multimediale Präsentation mit interaktiver Exploration. Schließlich ermöglicht das Operator-Konzept eine effektive Integration in den Unterrichtsablauf.

Die dokumentierten Iterationen und Problemlösungen verdeutlichen den experimentellen Charakter der Entwicklung und liefern wertvolle Erkenntnisse für zukünftige VR-Lernprojekte.
