# 3. Systementwicklung

Die Entwicklung unserer VR-Lernanwendung für Molekülgeometrien folgte einem nutzerzentrierten Ansatz: Zunächst definierten wir, *was* Schüler lernen sollen (Programmkonzept), dann *womit* sie lernen (Hardware), und schließlich *wie* die technische Umsetzung erfolgt. Dieser rote Faden – vom pädagogischen Ziel über die Gerätewahl bis zur Implementierung – strukturiert die folgende Dokumentation.

---

## 3.1 Unser Programm kurz erklärt

Unsere Anwendung besteht aus zwei Kernmodulen, die unterschiedliche Lernphasen adressieren: Das **Tutorial** vermittelt strukturiertes Wissen über Molekülgeometrien durch geführte Lektionen, während der **Molekülviewer** freies Explorieren ermöglicht. Diese Zweiteilung entspricht dem pädagogischen Prinzip "erst verstehen, dann anwenden".

### 3.1.1 Tutorial

Das Tutorial ist das Herzstück der Lernerfahrung. Es führt Schüler schrittweise durch die Konzepte der Molekülgeometrie – von einfachen Bindungstypen bis zu komplexen räumlichen Strukturen.

**Didaktisches Konzept:**

Der Aufbau folgt dem Scaffolding-Prinzip: Jede Lektion baut auf der vorherigen auf, sodass Komplexität graduell zunimmt. Ein Schüler beginnt mit der Frage "Was ist eine chemische Bindung?" und endet mit dem Verständnis, warum Methan tetraedrisch und Wasser gewinkelt ist.

**Die neun Lernschritte im Überblick:**

| Schritt | Thema | Lernziel |
|---------|-------|----------|
| 1 | Bindungstypen | Unterscheidung von Einfach-, Doppel- und Dreifachbindungen |
| 2 | Keilstrich-Schreibweise | Interpretation von 2D-Strukturformeln als 3D-Modelle |
| 3 | Molekülgeometrie-Konzept | Verständnis, dass Moleküle räumliche Strukturen haben |
| 4 | Linearer Bau | CO₂ als Beispiel für 180°-Geometrie |
| 5 | Gewinkelter Bau | H₂O mit 104,5°-Bindungswinkel |
| 6 | Trigonal-planar | BF₃ mit 120°-Winkeln in einer Ebene |
| 7 | Trigonal-pyramidal | NH₃ – warum freie Elektronenpaare die Geometrie beeinflussen |
| 8 | Tetraedrisch | CH₄ mit dem charakteristischen 109,5°-Winkel |
| 9 | Zusammenfassung | Verknüpfung aller Konzepte |

**Multimediale Präsentation:**

Jeder Schritt kombiniert drei Elemente:
1. **Erklärvideo:** Eine vorproduzierte Videosequenz (mit Greenscreen-Chroma-Keying in die VR-Umgebung integriert) erläutert das Konzept verbal und visuell.
2. **3D-Molekülmodell:** Das besprochene Molekül erscheint als interaktives Modell neben dem Video. Schüler können es drehen, von allen Seiten betrachten und die räumliche Struktur erfassen.
3. **Winkelvisualisierung:** Bei relevanten Schritten werden Bindungswinkel durch farbige Bögen und Gradangaben hervorgehoben.

Diese Kombination adressiert verschiedene Lerntypen: auditive Lerner profitieren vom Video, visuelle vom 3D-Modell, und kinästhetische von der Interaktion.

**Navigationsmöglichkeiten:**

Schüler können:
- Mit Vorwärts-/Rückwärts-Buttons zwischen Schritten wechseln
- Das aktuelle Video pausieren und wiederholen
- Moleküle während des Videos bereits manipulieren
- (Optional) Über das Operator-Tablet von der Lehrkraft durch das Tutorial geführt werden

### 3.1.2 Molekülviewer

Der Molekülviewer ist das "Freiflug-Modul": Nach dem strukturierten Tutorial können Schüler hier eigenständig Moleküle erkunden.

**Funktionsumfang:**

- **Molekülsuche:** Über eine virtuelle Tastatur können Molekülnamen eingegeben werden (z.B. "Ethanol", "Benzol", "Ammoniak")
- **Vordefinierte Bibliothek:** Eine kuratierte Sammlung der curriculumsrelevanten Moleküle steht ohne Suche bereit
- **PubChem-Integration:** Für fortgeschrittene Nutzer: Zugriff auf die NIH-Datenbank mit über 100 Millionen Molekülstrukturen
- **Freie Manipulation:** Drehen, Vergrößern, Verkleinern – alles durch intuitive Handgesten

**Pädagogischer Mehrwert:**

Der Viewer ermöglicht entdeckendes Lernen: Ein Schüler kann selbst herausfinden, dass alle Alkane tetraedrische Kohlenstoffatome haben, oder vergleichen, wie sich die Geometrie ändert, wenn Wasserstoff durch Fluor ersetzt wird. Diese Selbsterkundung festigt das im Tutorial erworbene Wissen.

---

## 3.2 Hardwarewahl

Die Wahl der Hardware war eine kritische Entscheidung, die den gesamten Projekterfolg beeinflusste. Wir mussten abwägen zwischen technischen Möglichkeiten, Kosten, Praktikabilität im Schulalltag und Lerneffektivität.

### 3.2.1 Meta Quest 3

Nach Evaluierung verschiedener VR-Systeme (HTC Vive, PlayStation VR, Valve Index) entschieden wir uns für die Meta Quest 3. Die Gründe:

**Standalone-Betrieb:**

Die Quest 3 benötigt keinen externen Computer. Dies ist für den Schuleinsatz entscheidend:
- Kein IT-Administrator muss Gaming-PCs warten
- Keine Verkabelung, die Stolperfallen erzeugt
- Setup-Zeit unter 2 Minuten (Headset aufsetzen, fertig)
- Transport zwischen Räumen problemlos möglich

Technisch basiert die Quest 3 auf einem Qualcomm Snapdragon XR2 Gen 2 Prozessor – im Wesentlichen ein leistungsstarkes Smartphone-SoC, das VR-Rendering ohne externe Hardware ermöglicht.

**Mixed-Reality-Passthrough:**

Die Quest 3 verfügt über hochauflösende Farbkameras, die die physische Umgebung in Echtzeit ins Headset übertragen. Für unsere Anwendung nutzen wir dies, um Moleküle in den realen Klassenzimmer-Kontext einzubetten. Schüler sehen nicht eine komplett virtuelle Welt, sondern ihre gewohnte Umgebung mit "schwebenden" Molekülmodellen.

Vorteile des Passthrough-Ansatzes:
- Reduzierte Desorientierung und Motion Sickness
- Schüler können ihre Mitschüler und die Lehrkraft wahrnehmen
- Einfacherer Wechsel zwischen VR-Nutzung und normalem Unterricht

**Inside-Out-Tracking:**

Sechs integrierte Kameras erfassen die Umgebung und berechnen daraus die Position des Headsets im Raum (6 Freiheitsgrade: 3 für Position, 3 für Rotation). Gleichzeitig wird die Position der Hände des Nutzers erkannt.

Dies eliminiert die Notwendigkeit externer Tracking-Stationen, die bei anderen Systemen (z.B. HTC Vive) im Raum montiert werden müssen. Für einen flexiblen Schuleinsatz wäre eine solche Installation unpraktisch.

**Hand-Tracking:**

Die Quest 3 erkennt individuelle Fingerbewegungen ohne zusätzliche Controller. Schüler können Moleküle "greifen", indem sie Daumen und Zeigefinger zusammenführen – eine intuitive Geste, die keine Einarbeitung erfordert.

Wir unterstützen dennoch auch Controller-Eingabe als Alternative:
- Bei schwierigen Lichtverhältnissen (Hand-Tracking basiert auf Kamerabildern)
- Für Nutzer, die haptisches Feedback bevorzugen
- Bei motorischen Einschränkungen, die präzise Fingergesten erschweren

**Technische Spezifikationen:**

| Parameter | Wert |
|-----------|------|
| Display-Auflösung | 2064 × 2208 Pixel pro Auge |
| Bildwiederholrate | 72/90/120 Hz |
| Sichtfeld (FoV) | ~110° horizontal |
| Tracking | 6DoF (Inside-Out) |
| Akkulaufzeit | ca. 2 Stunden aktive Nutzung |
| Gewicht | 515 g |

### 3.2.2 iPad-Interface

Ein fundamentales Problem beim VR-Einsatz im Unterricht: Die Lehrkraft kann nicht sehen, was die Schüler sehen, und hat keine Kontrolle über den Ablauf. Unsere Lösung: Ein Tablet als "Fernbedienung".

**Das Operator-Konzept:**

Die Lehrkraft hält ein iPad in der Hand und kann darüber:
- Das Tutorial für alle Schüler synchron starten und pausieren
- Zwischen Lektionen navigieren (vor/zurück)
- Bestimmte Moleküle für alle laden
- Den Passthrough-Modus ein- und ausschalten
- Den Systemstatus überwachen

**Technische Realisierung:**

Das iPad verbindet sich über WLAN mit dem VR-Headset. Die Kommunikation erfolgt über das WebSocket-Protokoll:

1. Die VR-Anwendung startet einen lokalen Server auf Port 8080
2. Das iPad öffnet eine Web-App im Browser (keine Installation nötig)
3. Über WebSockets werden Befehle in Echtzeit übertragen (Latenz <50ms)
4. Die IP-Adresse wird als QR-Code im VR-Interface angezeigt – die Lehrkraft scannt ihn zum Verbinden

**Warum ein iPad?**

- An vielen Schulen bereits vorhanden (Dienstgeräte für Lehrkräfte)
- Keine Installation erforderlich – Web-App läuft im Browser
- Große Bildschirmfläche für übersichtliche Bedienung
- Andere Tablets (Android) funktionieren ebenfalls

**Pädagogischer Nutzen:**

Das Operator-Konzept ermöglicht verschiedene Unterrichtsszenarien:

1. **Geführter Unterricht:** Lehrkraft steuert, alle Schüler sehen dasselbe
2. **Halboffene Exploration:** Lehrkraft gibt Molekül vor, Schüler explorieren frei
3. **Vollständig offen:** Schüler arbeiten selbstständig, Lehrkraft beobachtet nur

Die Trennung von Steuerung (Lehrkraft) und Erleben (Schüler) entspricht klassischen Unterrichtsrollen und erleichtert die Integration in bestehende didaktische Konzepte.

---

## 3.3 Technische Umsetzung

Nach der Klärung von "Was" (Programmkonzept) und "Womit" (Hardware) folgt nun das "Wie": Die softwaretechnische Implementierung.

Die Anwendung wurde in **Unity** entwickelt, einer Game-Engine, die auch für VR-Anwendungen weit verbreitet ist. Unity bietet:
- Plattformübergreifende Entwicklung (Quest, PC, potenziell andere VR-Systeme)
- Umfangreiche XR-Bibliotheken (XR Interaction Toolkit)
- C# als Programmiersprache (robust, gut dokumentiert)
- Visual Scripting für schnelle Prototypen

Die Projektstruktur umfasst ca. 25 C#-Skripte, organisiert in Module: Chemistry (Moleküllogik), Tutorial (Lernsequenz), VR (Interaktion), UI (Bedienoberfläche).

### 3.3.1 Interaktivität

Die Interaktion zwischen Nutzer und virtuellen Objekten ist das Kernmerkmal einer VR-Anwendung. Wir haben zwei Interaktionsebenen implementiert: **direkte Manipulation** für 3D-Objekte und **Ray-Casting** für UI-Elemente.

**Direkte Manipulation (Moleküle):**

Molekülmodelle können durch "Greifen" manipuliert werden. Der technische Ablauf:

1. **Gestenerkennung:** Das XR Interaction Toolkit erkennt eine "Pinch"-Geste (Daumen + Zeigefinger < 2cm Abstand) oder einen gedrückten Grip-Button am Controller.

2. **Kollisionsprüfung:** Ein unsichtbarer Kollisions-Collider um die Hand prüft, ob sie ein Molekül berührt.

3. **Attachment:** Bei erkanntem Griff wird das Molekül an die Hand "geheftet" – es folgt der Handbewegung.

4. **Rotationsberechnung:** Die Differenz der Handorientierung zwischen Frames wird auf das Molekül übertragen. Ein Smoothing-Algorithmus (lineare Interpolation mit Faktor 0.15) verhindert ruckartige Bewegungen.

5. **Release:** Beim Loslassen wird das Molekül an seiner aktuellen Position fixiert.

**Implementierte Optimierungen:**

- **Dead Zone:** Rotationen unter 2° werden ignoriert, um Hand-Zittern herauszufiltern
- **Position Lock:** Während der Rotation bleibt die Position fixiert – kein versehentliches Verschieben
- **Zwei-Hand-Skalierung:** Greifen mit beiden Händen ermöglicht Vergrößern/Verkleinern durch Auseinanderziehen

**Ray-Casting (UI-Elemente):**

Für Buttons, Menüs und Texteingaben eignet sich direkte Berührung weniger – die Elemente sind oft weiter entfernt oder erfordern präzise Selektion. Hier projizieren wir einen virtuellen "Laserstrahl" von der Hand:

1. Von der Handfläche ausgehend wird ein Strahl in Blickrichtung berechnet
2. Der erste Schnittpunkt mit einem UI-Element wird markiert (visuelles Feedback: Punkt auf dem Element)
3. Eine Pinch-Geste an dieser Position löst einen "Klick" aus

Dieser Ansatz ist von TV-Fernbedienungen inspiriert und intuitiv verständlich.

**Controller-Fallback:**

Alle Interaktionen funktionieren auch mit Controllern:
- Trigger-Taste ersetzt Pinch-Geste
- Grip-Taste ersetzt Greif-Geste
- Thumbstick ermöglicht zusätzliche Navigation

Die automatische Erkennung wechselt nahtlos zwischen Hand-Tracking und Controller-Modus.

### 3.3.2 Tutorial-Element

Das Tutorial-System koordiniert die Abfolge von Videos, Molekülen und Interaktionen. Die Architektur ist modular aufgebaut:

**ScriptableObjects für Lektionen:**

Jede Lektion ist als "ScriptableObject" definiert – eine Unity-Datenstruktur, die im Editor bearbeitet werden kann, ohne Code zu schreiben. Eine Lektion enthält:

```
TutorialStep:
  - Titel: "Tetraedrischer Bau"
  - Beschreibung: "Methan (CH₄) als Beispiel..."
  - VideoClip: [Referenz auf MP4-Datei]
  - MolekülPrefabs: [CH4Tetraedrisch, CH4Highlight]
  - Winkelmarkierungen: [109.5° zwischen C-H-Bindungen]
  - AutoAdvance: false (Nutzer muss manuell weiterklicken)
```

Dieser Ansatz ermöglicht es, neue Lektionen hinzuzufügen oder bestehende zu modifizieren, ohne den Programmcode zu ändern.

**TutorialManager – der Dirigent:**

Eine zentrale Komponente (`TutorialManager.cs`) koordiniert den Ablauf:

1. **Schritt laden:** Liest die ScriptableObject-Daten, instantiiert das Molekül-Prefab, startet das Video
2. **Positionierung:** Moleküle erscheinen 35cm vor dem Nutzer (dynamisch berechnet basierend auf aktueller Kopfposition)
3. **Synchronisation:** Video und Molekül-Highlights können zeitlich abgestimmt werden
4. **Navigation:** Reagiert auf Nutzereingaben (Weiter/Zurück) oder Operator-Befehle
5. **Cleanup:** Beim Schrittwechsel werden alte Moleküle entfernt, Ressourcen freigegeben

**Video-Integration mit Chroma-Key:**

Die Erklärvideos wurden vor einem Greenscreen aufgenommen. Ein selbstentwickelter Shader entfernt die grüne Farbe in Echtzeit:

```
// Pseudocode des Chroma-Key-Algorithmus:
für jeden Pixel:
  wenn (Farbton zwischen 100° und 140°) UND (Sättigung > 0.3):
    setze Alpha = 0 (transparent)
  sonst:
    behalte Original
```

Das Ergebnis: Das Video "schwebt" frei im Raum, ohne störenden Hintergrund. Die Toleranzwerte wurden empirisch optimiert, um Randartefakte (grüne Ränder um Personen) zu minimieren.

**Winkel-Visualisierung:**

Bei Geometrie-Lektionen werden Bindungswinkel visuell hervorgehoben:

1. **Bogen-Mesh:** Ein prozedural generierter Kreisbogen zwischen zwei Bindungen
2. **Textlabel:** Die Gradzahl (z.B. "109,5°") schwebt neben dem Bogen
3. **Farbcodierung:** Unterschiedliche Farben für verschiedene Winkeltypen

Die Berechnung des Bogens erfolgt mathematisch: Gegeben zwei Vektoren (Bindungsrichtungen) vom Zentralatom wird der Winkel per Skalarprodukt bestimmt und ein Mesh aus Dreiecken aufgespannt.

**Dynamisches Spawning:**

Ein technisches Detail mit großer Auswirkung auf die User Experience: Wo erscheinen Moleküle?

- **Problem:** Bei fixen Weltkoordinaten wären Moleküle je nach Startposition des Nutzers unterschiedlich weit entfernt oder gar außerhalb des Sichtfelds
- **Lösung:** Moleküle erscheinen relativ zur aktuellen Kamera (Kopf) Position: 35cm vor dem Nutzer, leicht nach unten versetzt

Zusätzlich prüft ein Algorithmus, ob die Zielposition bereits belegt ist (z.B. durch ein vorheriges Molekül) und verschiebt das neue Objekt gegebenenfalls.

### 3.3.3 Molekülviewer

Der Molekülviewer ist technisch komplexer als das Tutorial, da er dynamisch Moleküle aus verschiedenen Quellen laden und darstellen muss.

**Datenarchitektur – vom Periodensystem zum Molekül:**

Die Moleküldarstellung basiert auf einer dreistufigen Hierarchie:

1. **ElementData:** Eigenschaften einzelner Elemente
   - Symbol (H, C, N, O, ...)
   - Atomradius (für die Kugelgröße im Modell)
   - CPK-Farbe (standardisierte Farbkonvention: H=weiß, C=grau, N=blau, O=rot, ...)

2. **MoleculeData:** Beschreibung eines konkreten Moleküls
   - Liste der Atome mit 3D-Positionen (x, y, z)
   - Bindungsmatrix (welche Atome sind verbunden, mit welcher Bindungsordnung)
   - Metadaten (Name, Summenformel, Geometrie-Klassifikation)

3. **MoleculeRenderer:** Visuelle Darstellung
   - Erzeugt Kugeln für Atome (Radius proportional zum Atomradius)
   - Erzeugt Zylinder für Bindungen (zwischen verbundenen Atomen)
   - Wendet Materialien mit CPK-Farben an

**Ball-and-Stick-Modell:**

Wir verwenden das "Ball-and-Stick"-Modell (Kugelstab-Modell), das in der Chemiedidaktik weit verbreitet ist:
- Atome als farbige Kugeln
- Bindungen als graue Zylinder
- Größenverhältnisse näherungsweise proportional zu realen Atomradien

Alternativen wie das Kalottenmodell (raumfüllend) wurden verworfen, da Bindungen dort nicht sichtbar sind – für das Verständnis von Molekülgeometrie aber essentiell.

**PubChem-Integration:**

Für fortgeschrittene Nutzer haben wir einen Zugang zur PubChem-Datenbank des NIH (National Institutes of Health) integriert:

1. **Suchanfrage:** Der Nutzer gibt einen Molekülnamen ein (z.B. "Aspirin")
2. **API-Aufruf:** Eine HTTP-Anfrage an `https://pubchem.ncbi.nlm.nih.gov/rest/pug/compound/name/{name}/SDF` ruft die Strukturdaten ab
3. **SDF-Parsing:** Das zurückgegebene Structure Data File (SDF) wird geparst:
   - Atomzeilen enthalten: x-Koordinate, y-Koordinate, z-Koordinate, Elementsymbol
   - Bindungszeilen enthalten: Atom1-Index, Atom2-Index, Bindungsordnung
4. **Koordinatentransformation:** PubChem-Koordinaten (Ångström, rechtshändiges System) werden in Unity-Koordinaten (Meter, linkshändiges System) umgerechnet
5. **Rendering:** Das `MoleculeRenderer`-Modul erzeugt die visuelle Darstellung

**Fehlerbehandlung:**

Netzwerkanfragen können fehlschlagen. Implementierte Absicherungen:
- Timeout nach 10 Sekunden
- Benutzerfreundliche Fehlermeldung bei ungültigem Molekülnamen
- Offline-Fallback auf lokale Bibliothek
- Retry-Mechanismus bei temporären Netzwerkproblemen

**Shader und Rendering:**

Für eine konsistente Darstellung haben wir einen eigenen Shader entwickelt (`MoleculeUnlit.shader`):

- **Unlit (nicht beleuchtet):** Die Farben sind unabhängig von Lichtquellen immer gleich – wichtig für korrekte Farbwahrnehmung
- **Mobile-optimiert:** Reduzierte Shader-Komplexität für flüssige Darstellung auf dem mobilen Quest-Chip
- **Fallback:** Bei älteren GPUs wird automatisch ein einfacherer Shader verwendet

**Performance-Optimierungen:**

VR erfordert konstant hohe Frameraten (mindestens 72 FPS), sonst entsteht Motion Sickness. Unsere Maßnahmen:

1. **Level of Detail (LOD):** Ab 2m Entfernung werden vereinfachte Molekülgeometrien verwendet
2. **Object Pooling:** Häufig verwendete Objekte (Atom-Kugeln, Bindungs-Zylinder) werden wiederverwendet statt ständig neu erzeugt
3. **Texture-Komprimierung:** Videotexturen werden GPU-effizient komprimiert (ASTC-Format)
4. **Draw Call Batching:** Ähnliche Objekte werden in einem einzigen Renderaufruf zusammengefasst

**Benutzeroberfläche:**

Die Suchoberfläche im Molekülviewer umfasst:
- Eine schwebende virtuelle Tastatur (QWERTZ-Layout für deutsche Nutzer)
- Autovervollständigung nach 3 eingegebenen Zeichen
- Ergebnisliste als scrollbare 3D-Karten mit Molekül-Vorschau
- Kategoriefilter (organisch, anorganisch, curriculumsrelevant)

---

## Zusammenfassung des roten Fadens

Die Systementwicklung folgte einer klaren Logik:

1. **Pädagogisches Ziel definieren:** Schüler sollen Molekülgeometrien räumlich verstehen
2. **Programmkonzept ableiten:** Tutorial für strukturiertes Lernen + Viewer für freie Exploration
3. **Hardware wählen:** Meta Quest 3 für Standalone-VR + iPad für Lehrkraft-Kontrolle
4. **Technisch umsetzen:** Unity-Engine mit modularer Architektur, intuitive Interaktion, robuste Moleküldarstellung

Jede Entscheidung wurde durch die vorherige begründet: Die Wahl des Hand-Trackings folgt aus dem Ziel intuitiver Bedienung, dieses wiederum aus dem Wunsch, Einarbeitungszeit zu minimieren, was für den Schuleinsatz essentiell ist.

Das Ergebnis ist eine Anwendung, die technische Komplexität vor dem Nutzer verbirgt und stattdessen eine nahtlose Lernerfahrung bietet – genau das, was gute Bildungstechnologie ausmacht.
