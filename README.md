# MolekülVR — Molekulare Geometrie in Virtual Reality

[![Unity](https://img.shields.io/badge/Unity-2022.3_LTS-black?logo=unity)](https://unity.com/)
[![Meta Quest](https://img.shields.io/badge/Meta%20Quest%203-blue?logo=meta)](https://www.meta.com/quest/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

> **Jugend Forscht** — Untersuchung der Effektivität von Virtual Reality beim Erlernen molekularer Strukturen

---

## Übersicht

VR-Lernumgebung für Meta Quest 3, in der Schüler molekulare Geometrien (Tetraeder, Trigonal-planar, Gewinkelt, …) interaktiv in 3D erlernen. Das Projekt besteht aus:

- **VR-App** (Unity / Meta Quest 3) — 3D-Molekülvisualisierung, Tutorial-System, Hand-Tracking
- **iPad-Controller** — WebSocket-basierte Fernsteuerung im Browser
- **Web-Dashboard** — Statistische Auswertung der Studienergebnisse ([Live](https://jugend-forscht.vercel.app))

---

## Schnellstart

### Voraussetzungen

| Werkzeug | Version | Hinweis |
|----------|---------|---------|
| Unity | **2022.3 LTS** | exakte Minor-Version empfohlen, alle 2022.3.x sollten funktionieren |
| Git LFS | 3.x | wird für Video-Dateien benötigt (normalerweise mit Git mitinstalliert) |
| Meta Quest 3 | — | oder Quest 2/Pro, muss im Developer-Modus sein |
| Android SDK | API 29+ | wird vom Unity Android-Modul mitinstalliert |

### 1. Repository klonen

```bash
git lfs install
git clone https://github.com/mateo-gepard/JugendForscht.git
```

> **Wichtig:** `git lfs install` muss einmal ausgeführt werden. Ohne Git LFS werden die Video-Dateien (`.mp4`) nicht korrekt heruntergeladen.

### 2. In Unity öffnen

1. Unity Hub öffnen → **Add** → Ordner `JugendForscht/` auswählen
2. Mit Unity **2022.3 LTS** öffnen (bei Versionsabweichung "Continue" klicken)
3. Warten, bis alle Packages importiert sind (Meta XR SDK, XR Interaction Toolkit etc. werden automatisch heruntergeladen)
4. Szene öffnen: `Assets/Scenes/SampleScene.unity`

### 3. Build auf Quest 3

1. **File → Build Settings** → Plattform `Android` auswählen → **Switch Platform**
2. **Edit → Project Settings → XR Plug-in Management** → Tab "Android" → **Oculus** aktivieren
3. Quest 3 per USB anschließen (Developer-Modus muss aktiv sein)
4. **Build and Run**

### 4. iPad-Controller nutzen

Nach dem Starten der App auf der Quest:

1. IP-Adresse der Quest notieren (wird in der Unity-Konsole geloggt oder unter Quest Settings → Wi-Fi zu finden)
2. Auf dem iPad/Tablet/Laptop im Browser öffnen: `http://<quest-ip>:8080`
3. Die Weboberfläche verbindet sich automatisch per WebSocket

---

## Projektstruktur

```
JugendForscht/
├── Assets/
│   ├── Scripts/
│   │   ├── Chemistry/          # Molekül-Rendering, PubChem-API, Element-Datenbank
│   │   │   ├── MoleculeRenderer.cs     # Ball-and-Stick mit Low-Poly-Meshes
│   │   │   ├── MoleculeLibrary.cs      # PubChem-Integration & Caching
│   │   │   ├── LowPolyMeshes.cs        # Icosphere & Zylinder-Generator
│   │   │   └── ShaderIncluder.cs       # Shader-Referenzen für Standalone-Builds
│   │   ├── Builder/            # Interaktiver VR-Molekülbaukasten
│   │   │   ├── BuilderManager.cs       # Drag&Drop, Bindungen, Compile-Logik
│   │   │   ├── PeriodicTableDisplay.cs # Periodensystem-UI in VR
│   │   │   ├── BuilderAtom.cs          # Valenz, Ladung, Oktettregel
│   │   │   └── BuilderTile.cs          # Poke-Button für PSE und Tools
│   │   ├── Physics/            # Lorentz-Labor — Magnetfeld & Lorentzkraft
│   │   │   ├── LorentzLabManager.cs    # Singleton-Orchestrator (Singleton)
│   │   │   ├── MagneticFieldVolume.cs  # B-Feld-Box mit prozeduraler Pfeil-Visualisierung
│   │   │   ├── ChargedParticle.cs      # Rigidbody-Teilchen mit F_L = q·(v×B)
│   │   │   ├── VectorArrowDisplay.cs   # Echtzeit-Vektorpfeile (v, B, F_L) am Teilchen
│   │   │   ├── FingerRuleChecker.cs    # Hand-Tracking: Drei-Finger-Regel Bewertung
│   │   │   ├── FieldVolumeGrab.cs      # XR-Grab für B-Feld-Box per Pinch
│   │   │   └── LorentzLabSetup.cs      # Szenen-Helfer: erzeugt Lab-Hierarchie
│   │   ├── Quiz/               # Quiz-System
│   │   │   ├── QuizManager.cs
│   │   │   └── QuizButton.cs
│   │   ├── Tutorial/           # Video-basiertes Tutorial-System
│   │   │   ├── TutorialManager.cs      # Haupt-Controller (Singleton)
│   │   │   ├── TutorialTimeline.cs     # ScriptableObject mit Zeitstempeln
│   │   │   ├── TutorialCue.cs          # Datenklassen für Einblendungen
│   │   │   └── Editor/
│   │   │       └── TutorialBuilder.cs  # Editor-Tool zum Generieren der Timeline
│   │   ├── VR/                 # Hand-Tracking, Controller, Rotation
│   │   └── WebSocketServer.cs  # HTTP + WebSocket Server für iPad-Controller
│   ├── Scenes/
│   │   └── SampleScene.unity   # Hauptszene
│   ├── Shaders/
│   │   ├── MoleculeUnlit.shader        # GPU-Instanced Unlit für Atome/Bonds
│   │   └── HandTrackingClean.shader    # Stereo-kompatibles Hand-Mesh
│   ├── Tutorial/
│   │   ├── 0304.mp4                    # Tutorial-Video (Git LFS)
│   │   └── ChromaKeyShader.shader      # Green-Screen-Keying
│   └── Resources/
│       ├── MoleculeContoller.html      # iPad-UI (Premium Dark Theme)
│       └── Prefabs/                    # Molekül-Modelle
│
├── Dokumente+Tabellen/         # Forschungsarbeit, Tests, Rohdaten
├── Visualisierungen+Websiten/  # Web-Dashboard (Vercel)
└── Packages/manifest.json      # Unity Package Dependencies
```

---

## Features

### Molekül-Visualisierung
- **Ball-and-Stick-Modell** mit stereochemischer Darstellung (Keil-/Strichbindungen)
- **PubChem-Integration** — beliebige Moleküle per Name laden (Aspirin, Glucose, Koffein, …)
- **Hand-Tracking** — Moleküle mit Händen greifen und drehen
- Optimiert für Quest 3: Low-Poly-Meshes, GPU-Instancing, Mesh-Combining

### VR-Molekülbaukasten (Builder)
- **Interaktives Periodensystem** — in 3D ausklappbar, realistische physikalische Anordnung
- **Hand-Interaktion** — Atome via *Pinch*-Geste aus dem PSE ziehen und frei im Raum platzieren
- **Dynamischer Aufbau** — Atome durch Heranziehen verbinden (Einfach-, Doppel-, Dreifachbindungen)
- **Modifikations-Tools** — Formale Ladungen ändern (Kationen/Anionen beeinflussen Valenz), Bindungen löschen
- **Chemische Validierung** — Echtzeit-Prüfung der Oktett- und Duettregel per *Compile*-Button; validiert das Konstrukt und konvertiert es ins Rendering-System.

### Tutorial-System
- Einzelnes Video mit automatischen Pausen an definierten Zeitpunkten
- 3D-Einblendungen (Moleküle, Pfeile, Keilstrichformeln) synchron zum Video
- 11 Einheiten: Einführung → Bindungsarten → Keilstrichformel → Elektronenpaarabstoßung → 5 Geometrien → Abschluss
- Steuerung über iPad oder VR-Controller

### Quiz-System
- **20 Fragen** direkt in VR (10 × Keilstrichformel für Klasse 9, 10 × Chiralität für Klasse 11)
- Schwebende VR-Anzeige mit Antwort-Buttons, Fortschrittsanzeige und Erklärungen
- 2D-Bildanzeige: Keilstrich-Diagramme werden als Quad in VR eingeblendet
- Automatischer Molekülwechsel passend zur Frage
- Auswertungsbildschirm mit Punktzahl und Rating
- Steuerung über iPad (Quiz starten/beenden) und VR (Antworten antippen)

### iPad-Controller
- Dunkles Premium-UI, optimiert für Touch
- **Hamburger-Menü** — Fach (Chemie / Physik / Mathematik) und Thema (Keilstrichformel / Chiralität / Lorentz-Labor / Riemannsche Flächen) über Popup wählbar
- **Verbindungsleiste** — Echtzeit-Anzeige verbundener Geräte (rot/grün) und Gerätepopup
- Tutorial starten/pausieren/fortsetzen
- Moleküle laden (Schnellauswahl + Freitextsuche), für beide Themenbereiche
- Chiralitätswerkzeuge: Zentren erkennen, Enantiomere/Diastereomere/Konformere erzeugen, cis/trans, Konstitutionsisomere, Meso-Erkennung
- Lorentz-Labor: Simulation steuern, Ladung/Feld/Geschwindigkeit einstellen, Quiz- und Finger-Regel-Modus

### Lorentz-Labor (Physik-Modul)

Tischplatten-Experiment-Simulation der Lorentzkraft $F_L = q \cdot (\vec{v} \times \vec{B})$:

- **Magnetfeld-Box** (0,5 × 0,5 × 0,5 m) — prozedurales 6×6×6-Pfeilgitter in Echtzeit, greifbar per Pinch-Geste (XR-Grab)
- **Geladenes Teilchen** — fliegt von links in das Feld und wird auf einer Kreisbahn abgelenkt (Rigidbody + FixedUpdate, VR-Handedness-korrigiert)
- **Vektorpfeile am Teilchen** — v (grün), B (cyan), F_L (orange) skalieren und drehen sich live mit der Physik-Simulation
- **Quiz-Modus** — F_L-Pfeil ausblenden; Schüler schätzen Ablenkungsrichtung
- **Drei-Finger-Regel** (UVW-Regel):
  - Über iPad aktivierbar
  - Erkennt per Hand-Tracking die Richtung von Daumen (→ v), Zeigefinger (→ B) und Mittelfinger (→ F_L)
  - Beschriftete Labels schweben an den Fingerspitzen
  - Alle Pfeilspitzen **grün** bei ≤ 20° Abweichung aller drei Finger, **rot** sonst
- **Lehrersteuerung** — Feldstärke (0,1–5 T), Geschwindigkeit (0,05–1 m/s), Ladungsvorzeichen (Proton/Elektron) über iPad-Slider

### Mathematik-Modul: Riemannsche Flächen

Visualisierung von komplexen Funktionen und ihren Riemannschen Flächen in 3D:

- **Echtzeit-Parser & Analytische Fortsetzung** — Eingabe von komplexen Funktionen (z.B. `z^(1/3)`, `log(z)`) über das iPad-UI. Der Algorithmus erkennt automatisch die Anzahl der Blätter und generiert die Fläche über numerische analytische Fortsetzung.
- **Domain Coloring** — Farbton (Hue) zeigt das Argument (Phase), Helligkeit zeigt den Betrag ($|f(z)|$) der komplexen Zahl an.
- **Interaktiver 3D-Graph** — Die generierte Mesh wird in einer 40cm-Box gerendert. Wie bei Molekülen kann die gesamte Fläche per Pinch-Geste gegriffen und im Raum gedreht werden.
- **Finger-Tap Probing** — Berührt man die komplexe Basisebene (y=0) mit dem Zeigefinger, wird eine vertikale Schnittlinie durch die Fläche gezeichnet und die exakten Werte für $z$ und $f(z)$ werden live in VR eingeblendet.
- **Dynamische Skalierung** — Die Definitionsgrenze für den Betrag $|z|$ lässt sich live am iPad verstellen, die Höhe (Y-Achse) wird unabhängig davon automatisch skaliert.

---

## Tutorial bearbeiten

Die Tutorial-Timeline wird über ein Editor-Skript generiert:

1. In Unity: **Tutorial → Build Tutorial Timeline** (Menüleiste)
2. Zeitstempel und Einblendungen in `Assets/Scripts/Tutorial/Editor/TutorialBuilder.cs` anpassen
3. Neues Video in `Assets/Tutorial/` ablegen und im `TutorialManager`-GameObject zuweisen

---

## Performance-Optimierungen (Quest 3)

| Optimierung | Effekt |
|-------------|--------|
| Low-Poly-Icosphere (42 Vertices) | 10× weniger Geometrie pro Atom |
| Atom-Mesh-Combining | Alle Atome gleichen Elements → 1 Draw Call |
| GPU Instancing (MoleculeUnlit Shader) | Bonds in wenigen Batches |
| Bond-Rerender-Debounce (5×/s) | Kein Frame-Drop bei Rotation |
| Keine Bond-Collider | Reduzierte Physik-Last |
| Quality-Tier "Low" für Android | Keine Schatten, keine Reflections |

---

## Technologie-Stack

- **Engine**: Unity 2022.3 LTS (Built-in Render Pipeline)
- **XR SDK**: Meta XR SDK 83.0.0, XR Interaction Toolkit 2.6.5
- **Shader**: Custom Unlit mit GPU-Instancing + Stereo-Support
- **Networking**: Eigener WebSocket-Server (System.Net.Sockets)
- **Physik**: Unity-Rigidbody mit Handedness-Korrektur für Lorentzkraft
- **API**: PubChem REST API (3D-Konformer als JSON)
- **Web**: Plotly.js Dashboard auf Vercel

---

## Lizenz

MIT — siehe [LICENSE](LICENSE)


## 🙏 Danksagungen

- [PubChem](https://pubchem.ncbi.nlm.nih.gov/) für die Moleküldatenbank
- [Meta](https://developer.meta.com/) für das XR SDK
- [Unity](https://unity.com/) für die Game Engine
- [Plotly](https://plotly.com/) für die Visualisierungsbibliothek

---

<p align="center">
  <b>🔬 Wissenschaft trifft Virtual Reality 🥽</b>
</p>
