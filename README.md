# 🧪 MolekülVR - Molekülvisualisierung in Virtual Reality

[![Unity](https://img.shields.io/badge/Unity-2022.3-black?logo=unity)](https://unity.com/)
[![Meta Quest](https://img.shields.io/badge/Meta%20Quest-3-blue?logo=meta)](https://www.meta.com/quest/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Vercel](https://img.shields.io/badge/Vercel-Deployed-black?logo=vercel)](https://vercel.com)

> **Jugend Forscht 2025** - Untersuchung der Effektivität von Virtual Reality beim Erlernen molekularer Strukturen

<p align="center">
  <img src="docs/banner.png" alt="MolekülVR Banner" width="800">
</p>

## 📋 Projektübersicht

Dieses Projekt untersucht, ob das Lernen von Molekülstrukturen in Virtual Reality effektiver ist als mit traditionellen 2D-Methoden. Das Projekt besteht aus:

| Komponente | Beschreibung |
|------------|--------------|
| 🥽 **VR-Anwendung** | Unity-basierte Meta Quest 3 App zur interaktiven 3D-Molekülvisualisierung |
| 📊 **Datenauswertung** | Statistische Analyse der Testergebnisse (VR vs. Nicht-VR Gruppe) |
| 📝 **Forschungsarbeit** | Wissenschaftliche Dokumentation und Schriftlicher Test |
| 🌐 **Web-Dashboard** | Interaktive Visualisierung der Studienergebnisse |

---

## 🗂️ Projektstruktur

```
JugendForscht/
├── Assets/                          # Unity-Projektdateien
│   ├── Scripts/                     # C# Skripte
│   │   ├── Molecule/                # Molekül-Rendering & PubChem-API
│   │   ├── Tutorial/                # Tutorial-System
│   │   ├── UI/                      # Benutzeroberfläche
│   │   └── Interaction/             # VR-Interaktionen
│   ├── Scenes/                      # Unity-Szenen
│   ├── Prefabs/                     # Vorgefertigte Objekte
│   ├── Resources/                   # Laufzeit-Ressourcen
│   └── Shaders/                     # Quest-optimierte Shader
│
├── Dokumente+Tabellen/              # Forschungsdokumentation
│   ├── Final Jugend Forscht (1).pdf # Forschungsarbeit
│   ├── Schriftlicher Test.pdf       # Test für Studienteilnehmer
│   ├── AuswertungTabelle.xlsx       # Rohdaten der Studie
│   └── AuswerungTabelleAbsoulutePunktzahl.xlsx  # Punkteauswertung
│
├── Visualisierungen+Websiten/       # Web-Visualisierungen (Vercel)
│   └── DatenVisualisierung.html     # Interaktives Ergebnis-Dashboard
│
├── Packages/                        # Unity Package Manager
├── ProjectSettings/                 # Unity Projekteinstellungen
└── vercel.json                      # Vercel Deployment-Konfiguration
```

---

## 🥽 VR-Anwendung

### Features

- **🔬 Molekül-Visualisierung**: Echtzeit-3D-Rendering von Molekülen
- **🌐 PubChem-Integration**: Laden von Molekülen direkt aus der PubChem-Datenbank
- **✋ Hand-Tracking**: Interaktion mit Molekülen über Meta Quest Hand-Tracking
- **📐 Ebenen-Visualisierung**: Darstellung von Molekülebenen mit Normalen
- **📚 Tutorial-System**: Geführtes Lernen von Molekülgeometrien
- **📱 Tablet-Steuerung**: WebSocket-basierte Fernsteuerung via Browser

### Unterstützte Moleküle

| Molekül | Geometrie | Bindungswinkel |
|---------|-----------|----------------|
| H₂O | Gewinkelt | 104.5° |
| CH₄ | Tetraeder | 109.5° |
| NH₃ | Trigonal-pyramidal | 107° |
| BF₃ | Trigonal-planar | 120° |
| C₂H₆O | Ethanol | - |
| C₆H₆ | Benzol (planar) | 120° |

### Systemanforderungen

- **VR-Headset**: Meta Quest 2/3/Pro
- **Unity**: 2022.3 LTS oder höher
- **Android SDK**: API Level 29+

### Installation & Build

```bash
# Repository klonen
git clone https://github.com/mateo-gepard/JugendForscht.git

# Unity Hub öffnen und Projekt hinzufügen
# Build-Ziel: Android (Meta Quest)
```

1. Öffne das Projekt in Unity 2022.3+
2. Gehe zu `File > Build Settings`
3. Wähle `Android` als Plattform
4. Aktiviere `Meta Quest` unter XR Plugin Management
5. Klicke `Build and Run` mit verbundenem Quest

---

## 📊 Studienergebnisse

### Methodik

- **Teilnehmer**: Schüler/innen einer Chemie-Klasse
- **Design**: Randomisierte Kontrollstudie (VR vs. 2D-Lernen)
- **Messung**: Schriftlicher Test zu Molekülgeometrien

### Ergebnisse auf einen Blick

Die detaillierten Ergebnisse sind im [Web-Dashboard](https://jugend-forscht.vercel.app) interaktiv visualisiert.

| Metrik | VR-Gruppe | Kontrollgruppe |
|--------|-----------|----------------|
| Durchschnittspunktzahl | *siehe Dashboard* | *siehe Dashboard* |
| Verständnis 3D-Geometrie | *siehe Dashboard* | *siehe Dashboard* |

---

## 🌐 Web-Dashboard

Das interaktive Dashboard visualisiert die Studienergebnisse mit Plotly.js.

**Live**: [https://jugend-forscht.vercel.app](https://jugend-forscht.vercel.app)

### Lokale Entwicklung

```bash
# Im Ordner Visualisierungen+Websiten
cd Visualisierungen+Websiten

# Mit einem lokalen Server öffnen
python -m http.server 8080
# oder
npx serve
```

---

## 📁 Dokumentation

Die vollständige Forschungsdokumentation befindet sich in `Dokumente+Tabellen/`:

| Datei | Beschreibung |
|-------|--------------|
| `Final Jugend Forscht (1).pdf` | Vollständige Forschungsarbeit |
| `Schriftlicher Test.pdf` | Verwendeter Test für beide Gruppen |
| `AuswertungTabelle.xlsx` | Rohdaten mit allen Testergebnissen |
| `AuswerungTabelleAbsoulutePunktzahl.xlsx` | Berechnete Punktzahlen |

---

## 🛠️ Technologie-Stack

### VR-Anwendung
- **Engine**: Unity 2022.3 LTS
- **XR**: Meta XR SDK, XR Interaction Toolkit
- **Rendering**: Quest-optimierte Shader (Mobile/Unlit)
- **Networking**: WebSocket für Tablet-Steuerung
- **API**: PubChem REST API für Moleküldaten

### Web-Dashboard
- **Visualisierung**: Plotly.js
- **Hosting**: Vercel
- **Styling**: Custom CSS (Dark Theme)

---

## 👥 Team

**Jugend Forscht 2025**

- Entwicklung & Forschung: Mateo Gepard

---

## 📄 Lizenz

Dieses Projekt ist unter der MIT-Lizenz lizenziert - siehe [LICENSE](LICENSE) für Details.

---

## 🙏 Danksagungen

- [PubChem](https://pubchem.ncbi.nlm.nih.gov/) für die Moleküldatenbank
- [Meta](https://developer.meta.com/) für das XR SDK
- [Unity](https://unity.com/) für die Game Engine
- [Plotly](https://plotly.com/) für die Visualisierungsbibliothek

---

<p align="center">
  <b>🔬 Wissenschaft trifft Virtual Reality 🥽</b>
</p>
