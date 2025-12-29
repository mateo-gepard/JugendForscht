# 3. Systementwicklung

## Einleitung: Der rote Faden dieses Kapitels

Bevor wir in technische Details eintauchen, ist es wichtig zu verstehen, wie dieses Kapitel aufgebaut ist – und warum.

Wir entwickeln keine gewöhnliche Anwendung. Unser Ziel ist es, ein fundamentales Problem des Chemieunterrichts zu lösen: Schüler lernen dreidimensionale Molekülstrukturen aus zweidimensionalen Abbildungen in Schulbüchern. Das ist, als würde man versuchen, Architektur aus Grundrissen zu verstehen, ohne je ein Gebäude betreten zu haben.

Die Fragen, die wir beantworten müssen, sind daher eng miteinander verknüpft:

1. **Was genau sollen Schüler lernen?** → Das bestimmt, welche Funktionen unsere Software braucht
2. **Wie lernen Menschen am besten räumliche Zusammenhänge?** → Das bestimmt, wie wir diese Funktionen gestalten
3. **Auf welcher Hardware realisieren wir das?** → Das bestimmt technische Randbedingungen
4. **Wie verbinden wir 2D-Strukturformeln mit 3D-Modellen?** → Das ist der technische Kern, der alles zusammenhält

Der letzte Punkt ist besonders wichtig – und oft unterschätzt. Wenn ein Schüler in einem Chemiebuch eine Strukturformel mit Keilstrich- und gestrichelten Bindungen sieht, weiß er: Der Keil zeigt "nach vorne" (zum Betrachter), die gestrichelte Linie "nach hinten". Aber was bedeutet das in VR, wo der Schüler das Molekül von allen Seiten betrachten und drehen kann? Die Antwort auf diese Frage erfordert nicht-triviale Mathematik – und hat direkte Auswirkungen auf das Lernerlebnis.

Dieses Kapitel ist daher keine Auflistung isolierter Komponenten, sondern eine zusammenhängende Geschichte: Vom pädagogischen Problem über die Lösungsidee bis zur mathematischen und softwaretechnischen Umsetzung.

---

## 3.1 Das Programm: Was wir erreichen wollen

### 3.1.1 Zwei Modi für zwei Lernphasen

Unsere Anwendung besteht aus zwei Kernmodulen: dem **Tutorial** und dem **Molekülviewer**. Diese Zweiteilung ist kein Zufall, sondern folgt dem pädagogischen Prinzip "erst verstehen, dann anwenden".

Das **Tutorial** führt strukturiert durch die Konzepte der Molekülgeometrie. Es ist wie ein Lehrbuchkapitel – nur dreidimensional und interaktiv. Der Schüler wird an die Hand genommen, sieht Erklärvideos, die im Raum schweben, und kann die besprochenen Moleküle gleichzeitig von allen Seiten betrachten.

Der **Molekülviewer** ist das Gegenstück: freies Explorieren. Hier kann der Schüler jedes beliebige Molekül laden und untersuchen. Das im Tutorial erworbene Wissen wird angewendet und gefestigt.

Doch bevor wir technisch werden, müssen wir verstehen: Was genau soll ein Schüler am Ende einer Tutorial-Session können?

### 3.1.2 Die Lernziele – und warum sie die Software bestimmen

| Schritt | Thema | Was der Schüler verstehen soll |
|---------|-------|--------------------------------|
| 1 | Bindungstypen | Einfach-, Doppel- und Dreifachbindungen unterscheiden sich nicht nur in der Stärke, sondern auch in der Geometrie |
| 2 | Keilstrich-Schreibweise | 2D-Formeln codieren 3D-Information: Keil = nach vorne, gestrichelt = nach hinten |
| 3 | Molekülgeometrie-Konzept | Moleküle haben räumliche Strukturen, die durch Abstoßung der Elektronenpaare bestimmt werden |
| 4 | Linearer Bau | CO₂ mit 180° – warum liegen alle Atome auf einer Geraden? |
| 5 | Gewinkelter Bau | H₂O mit 104,5° – freie Elektronenpaare "drücken" die Bindungen zusammen |
| 6 | Trigonal-planar | BF₃ mit 120° – perfekte Symmetrie in der Ebene |
| 7 | Trigonal-pyramidal | NH₃ – ein freies Elektronenpaar macht aus einem Tetraeder eine Pyramide |
| 8 | Tetraedrisch | CH₄ mit 109,5° – die perfekte 3D-Symmetrie |
| 9 | Zusammenfassung | Alle Konzepte zusammen: Von der Formel zur 3D-Struktur |

Aus diesen Lernzielen ergeben sich direkte technische Anforderungen:

- **Schritt 2 erfordert eine korrekte Keilstrich-Visualisierung.** Das klingt trivial, ist aber technisch anspruchsvoll: Wie entscheidet die Software, welche Bindung ein Keil und welche gestrichelt dargestellt wird, wenn der Nutzer das Molekül dreht?

- **Schritte 5 und 7 erfordern die Visualisierung freier Elektronenpaare.** Diese sind keine Atome, keine Bindungen – sie sind "nichts" im chemischen Sinne, aber sie beeinflussen die Geometrie massiv.

- **Alle Schritte erfordern intuitive 3D-Interaktion.** Der Schüler muss das Molekül drehen können, ohne einen Gedanken an "Steuerung" zu verschwenden.

Im Folgenden werden wir sehen, wie jede dieser Anforderungen gelöst wurde – und warum die Lösungen so aussehen, wie sie aussehen.

---

## 3.2 Die Hardware: Warum Meta Quest 3 + iPad

### 3.2.1 Die Wahl des VR-Headsets

Nach Evaluierung verschiedener Systeme (HTC Vive, PlayStation VR, Valve Index, Pico) entschieden wir uns für die **Meta Quest 3**. Die Entscheidung basierte auf drei Kriterien, die für den Schuleinsatz nicht verhandelbar waren:

**1. Standalone-Betrieb (kein PC erforderlich)**

In einem Klassenzimmer mit 30 Schülern wäre es undenkbar, 30 Gaming-PCs aufzustellen. Die Quest 3 ist ein eigenständiges Gerät – aufsetzen, starten, fertig. Der interne Qualcomm Snapdragon XR2 Gen 2 Chip ist ein leistungsstarkes mobiles SoC, das VR-Rendering ohne externe Hardware ermöglicht.

Das hat direkte Konsequenzen für unsere Software: Wir müssen mit begrenzter GPU-Leistung haushalten. Komplexe Shader, die auf einem PC-VR-System problemlos laufen, würden hier Ruckeln verursachen. Jede Rendering-Entscheidung musste auf mobile Hardware optimiert werden.

**2. Mixed-Reality-Passthrough**

Die Quest 3 hat Farbkameras, die die Umgebung durchscheinen lassen. Für unsere Anwendung ist das essentiell: Schüler sehen ihr echtes Klassenzimmer mit virtuellen Molekülen darin. Sie sehen ihre Mitschüler, die Lehrkraft, die Tafel. Das reduziert Desorientierung und Motion Sickness erheblich.

Technisch bedeutet das: Wir können keine geschlossene VR-Umgebung mit aufwändiger Beleuchtung verwenden. Unsere Moleküle müssen in der echten Welt "funktionieren" – mit wechselnden Lichtverhältnissen, unbekannten Hintergründen, variablen Entfernungen.

**3. Hand-Tracking ohne Controller**

Die Quest 3 erkennt Hände und einzelne Finger direkt. Keine Controller bedeutet: keine Geräte, die verloren gehen oder herunterfallen können. Keine Batterien, die leer werden. Keine Erklärung, welcher Knopf was tut.

Aber Hand-Tracking hat Grenzen: Bei schlechtem Licht funktioniert es schlechter. Schnelle Bewegungen werden unscharf. Präzise Fingerstellungen (wie das Zeigen auf kleine Buttons) sind schwieriger als mit einem Controller-Laserpointer.

Deshalb unterstützen wir beides: Hand-Tracking als Standard, Controller als Fallback. Die Software erkennt automatisch, was verwendet wird.

### 3.2.2 Das iPad als Operator-Interface

Ein fundamentales Problem beim VR-Unterricht: Die Lehrkraft ist blind. Sie sieht nicht, was die Schüler sehen, und hat keine Kontrolle.

Unsere Lösung: Ein **Tablet als Fernbedienung**. Die Lehrkraft kann:

- Das Tutorial für alle Schüler synchron starten/pausieren
- Zwischen Lektionen navigieren
- Bestimmte Moleküle für alle laden
- Den Passthrough-Modus umschalten

Die technische Realisierung ist elegant: Die VR-Anwendung startet einen kleinen **WebSocket-Server** auf Port 8080. Das iPad öffnet eine Web-App im Browser (keine Installation nötig) und verbindet sich. Befehle werden in Echtzeit übertragen.

Aber wie findet das iPad das Headset im WLAN? Die IP-Adresse ist dynamisch und ändert sich. Die Lösung: Die VR-App zeigt einen **QR-Code** mit der aktuellen URL. Die Lehrkraft scannt ihn mit der iPad-Kamera – fertig.

---

## 3.3 Die Brücke zwischen 2D und 3D: Das technische Herzstück

Jetzt kommen wir zum Kern: Wie wird aus einer zweidimensionalen Strukturformel mit Keilstrichen ein dreidimensionales, interaktives Modell, das korrekt auf Rotation reagiert?

Diese Frage ist komplexer als sie scheint. Lassen Sie uns sie Schritt für Schritt durchgehen.

### 3.3.1 Das Problem der Keilstrich-Darstellung

In einem Schulbuch ist die Konvention klar:
- **Durchgezogener Keil** (▲): Die Bindung zeigt "zum Betrachter" (aus der Zeichenebene heraus)
- **Gestrichelte Linie** (- - -): Die Bindung zeigt "vom Betrachter weg" (in die Zeichenebene hinein)
- **Normale Linie** (—): Die Bindung liegt in der Zeichenebene

Aber was passiert in VR, wenn der Schüler das Molekül um 180° dreht? Plötzlich ist "vorne" zu "hinten" geworden. Müssen jetzt alle Keile zu Strichen werden und umgekehrt?

**Ja, genau das.**

Und das ist nicht-trivial zu implementieren. Denn die Software muss in Echtzeit – bei jeder Kopf- oder Handbewegung – entscheiden, welche Bindung wie dargestellt wird.

### 3.3.2 Die Lösung: Eine fixierte Referenzebene

Unser Ansatz: Wir definieren eine **Referenzebene** (wie das Papier eines Schulbuchs), die im Raum fixiert bleibt, während das Molekül rotiert. Die Klassifikation jeder Bindung (Keil, Strich, normal) basiert auf ihrer Position relativ zu dieser Ebene.

**Schritt 1: Die Ebene finden – Hauptkomponentenanalyse (PCA)**

Für die meisten Moleküle ist intuitiv klar, wo "die Ebene" liegt. Bei Benzol (C₆H₆) ist es der Ring. Bei Ethen die Doppelbindung. Aber wie findet ein Algorithmus das automatisch?

Wir verwenden **PCA (Principal Component Analysis)** auf die Atompositionen – aber nicht auf alle Atome gleich. Der Trick: Wir verwenden primär die **Kohlenstoffatome** für die Ebenenberechnung, da diese typischerweise das "Gerüst" des Moleküls bilden. Bei Molekülen ohne Kohlenstoff werden alle Atome verwendet.

Der PCA-Algorithmus findet die Richtung der größten Streuung (erste Hauptkomponente) und die zweitgrößte (senkrecht dazu). Die Ebene wird durch diese beiden Richtungen aufgespannt. Die dritte Hauptkomponente (geringste Streuung) gibt die Normale – die Richtung senkrecht zur Ebene.

**Schritt 2: Die Ebene fixieren – Ankerpunkt und Normale**

Einmal berechnet, wird die Ebene im Weltraum **fixiert**:

```
planePoint = Position des dem Schwerpunkt nächsten Atoms (Anker)
fixedNormal = PCA-Normale bei Initialisierung
```

Diese Ebene bewegt sich nicht, auch wenn das Molekül rotiert. Sie ist das "Papier", relativ zu dem Bindungen klassifiziert werden.

**Schritt 3: Bindungen klassifizieren – Vorzeichenabstände**

Für jede Bindung berechnen wir den **vorzeichenbehafteten Abstand** beider Atom-Endpunkte zur Ebene:

```
dA = Skalarprodukt(posA - planePoint, fixedNormal)
dB = Skalarprodukt(posB - planePoint, fixedNormal)
```

Positiver Abstand = hinter der Ebene (Strich)
Negativer Abstand = vor der Ebene (Keil)

Nun gibt es vier Fälle:

| dA | dB | Klassifikation | Grund |
|----|----|----------------|-------|
| < 0 | < 0 | Keil (▲) | Beide Atome vor der Ebene |
| > 0 | > 0 | Gestrichelt (- - -) | Beide Atome hinter der Ebene |
| < 0 | > 0 | Normal (—) | Bindung kreuzt die Ebene |
| > 0 | < 0 | Normal (—) | Bindung kreuzt die Ebene |

**Schritt 4: Kantenfälle – Die 50%-Regel**

Was wenn eine Bindung die Ebene *fast* kreuzt? Wenn dA und dB gegensätzliche Vorzeichen haben, aber ein Endpunkt sehr nah an der Ebene liegt?

Wir haben eine **50%-Regel** implementiert: Die Bindung wird nur als "normal" klassifiziert, wenn der Schnittpunkt mit der Ebene im **mittleren 50%** der Bindungslänge liegt (parametrisch zwischen t=0.25 und t=0.75). Liegt der Schnittpunkt näher an einem der Atome, "gewinnt" das andere Atom – die Bindung wird als Keil oder Strich basierend auf der Position des dominierenden Atoms klassifiziert.

```csharp
// Vereinfachter Code aus MoleculePlaneAlignment.cs:
if (bondCrossesPlane) {
    float t = Abs(dA) / (Abs(dA) + Abs(dB));  // Schnittpunkt parametrisch
    if (t >= 0.25 && t <= 0.75) {
        return BondStereo.None;  // Normal
    } else if (t < 0.25) {
        return (dB < 0) ? BondStereo.Up : BondStereo.Down;  // B dominiert
    } else {
        return (dA < 0) ? BondStereo.Up : BondStereo.Down;  // A dominiert
    }
}
```

**Schritt 5: Dynamische Neuklassifikation bei Rotation**

Wenn der Nutzer das Molekül dreht, ändern sich die Weltpositionen der Atome – aber die Ebene bleibt fixiert. Daher müssen Bindungen **kontinuierlich neu klassifiziert** werden.

Aus Performance-Gründen passiert das nicht in jedem Frame, sondern alle 100ms (10x pro Sekunde). Das ist schnell genug, um flüssig auszusehen, aber sparsam genug für mobile Hardware.

### 3.3.3 Automatische Rotation: Das Molekül zeigt sich

Beim Laden eines Moleküls passiert etwas Cleveres: Das Molekül rotiert automatisch langsam um die Y-Achse. Bei 180° und 360° pausiert es kurz.

**Warum?** Weil der Schüler so sieht, wie sich die Keil/Strich-Darstellung ändert, während das Molekül rotiert. Ein Keil wird zum Strich, ein Strich zum Keil – und der Schüler versteht: "Ah, die Darstellung hängt davon ab, von wo ich schaue!"

Diese Auto-Rotation stoppt sofort, wenn der Nutzer das Molekül greift. Von da an hat er die volle Kontrolle.

### 3.3.4 Freie Elektronenpaare: Das Unsichtbare sichtbar machen

Bei Molekülen wie H₂O oder NH₃ beeinflussen **freie Elektronenpaare** die Geometrie massiv. Wasser ist nicht linear wie CO₂, obwohl es auch nur drei Atome hat – weil zwei freie Elektronenpaare am Sauerstoff "Platz brauchen".

Aber Elektronenpaare sind keine Atome. Man kann sie nicht wie Kugeln darstellen. Trotzdem müssen Schüler verstehen, dass sie da sind und Raum einnehmen.

Unsere Lösung: **Halbtransparente Orbitale**. Freie Elektronenpaare werden als kleine, halbtransparente bläuliche Kugeln dargestellt. Bei der Tutorial-Lektion zu H₂O werden sie **rot hervorgehoben**, um die Aufmerksamkeit zu lenken.

Die Positionierung erfolgt mathematisch korrekt: Bei NH₃ sitzt das freie Elektronenpaar genau dort, wo ein vierter Wasserstoff wäre, wenn Stickstoff vier Bindungen eingehen würde. Die Geometrie ist tetraedrisch – wir sehen nur drei Ecken, aber die vierte ist "unsichtbar besetzt".

```csharp
// Aus TutorialPrefabCreator.cs - NH₃ mit Elektronenpaar
CreateAtom(molecule, "N", Vector3.zero, 0.11f, blueColor);
// ... drei Wasserstoffe ...
// Freies Elektronenpaar oben (wo ein 4. H wäre bei Tetraeder)
CreateAtom(molecule, "LP", new Vector3(0, bondLength * 0.6f, 0), 0.04f, 
    new Color(0.5f, 0.6f, 1f, 0.5f));  // Halbtransparent
```

---

## 3.4 Die Rendering-Pipeline: Vom Datensatz zum sichtbaren Molekül

Wir haben jetzt verstanden, wie Bindungen klassifiziert werden. Aber wie wird ein Molekül überhaupt gerendert?

### 3.4.1 Die Datenquellen

Moleküldaten kommen aus zwei Quellen:

**1. Vorgefertigte Prefabs (Tutorial-Moleküle)**

Für die Tutorial-Lektionen sind die Moleküle als Unity-Prefabs gespeichert. Jedes Atom, jede Bindung, jeder Winkelmarker ist vorab definiert. Das garantiert perfekte Darstellung und ermöglicht spezielle Effekte wie Hervorhebungen.

**2. PubChem-API (Molekülviewer)**

Für den freien Viewer laden wir Moleküle dynamisch aus der **PubChem-Datenbank** des NIH. Der Ablauf:

1. Nutzer gibt Molekülname ein (z.B. "Aspirin")
2. HTTP-Request an `https://pubchem.ncbi.nlm.nih.gov/rest/pug/compound/name/aspirin/SDF?record_type=3d`
3. Antwort: SDF-Datei (Structure Data File) mit Atomkoordinaten und Bindungen
4. Parsing: Zeile für Zeile werden Atome und Bindungen extrahiert
5. Koordinatentransformation: Ångström → Meter, rechtshändiges → linkshändiges System
6. Rendering: MoleculeRenderer erzeugt die 3D-Darstellung

**Rate Limiting:** PubChem erlaubt nur 5 Requests pro Sekunde. Wir erzwingen 200ms Pause zwischen Anfragen.

### 3.4.2 Der MoleculeRenderer: Vom Datensatz zum 3D-Modell

Die Klasse `MoleculeRenderer` ist das Arbeitspferd der Visualisierung:

**Atome rendern:**

1. Für jedes Atom wird das Element aus der Datenbank geholt (H, C, N, O, ...)
2. Eine Kugel wird instantiiert
3. Position = Atomkoordinaten × Umrechnungsfaktor × Skalierung
4. Radius = Van-der-Waals-Radius × Skalierungsfaktor
5. Farbe = CPK-Konvention (H=weiß, C=grau, N=blau, O=rot, ...)

**Bindungen rendern (normal):**

1. Start- und Endatom werden ermittelt
2. Beide Atompositionen werden geholt
3. Ein Zylinder wird zwischen den Kugeloberflächen gespannt (nicht zwischen den Mittelpunkten!)
4. Rotation = Ausrichtung des Zylinders entlang der Bindungsachse

**Bindungen rendern (Keil):**

Der Keil ist komplexer. Ein einzelner Zylinder kann keine Keilform darstellen. Stattdessen verwenden wir **5 gestaffelte Zylinder** mit zunehmendem Radius:

```csharp
// Aus MoleculeRenderer.cs - RenderWedgeBond
int segments = 5;
for (int i = 0; i < segments; i++) {
    float t = (float)i / (segments - 1);
    Vector3 pos = Vector3.Lerp(start, end, t);
    float radius = Mathf.Lerp(bondRadius * 0.5f, bondRadius * 2f, t);
    // Erzeuge Zylinder an pos mit radius
}
```

Das Ergebnis: Ein Konus, der vom Startatom schmal beginnt und zum Endatom hin breiter wird – genau wie ein gezeichneter Keilstrich.

**Bindungen rendern (gestrichelt):**

Gestrichelte Bindungen sind mehrere kurze Zylinder mit Lücken dazwischen. Wir verwenden ein spezielles Material mit vorberechneter Textur, das die Striche simuliert.

### 3.4.3 Dynamische Skalierung: Große Moleküle passen auch

Ein Methan-Molekül (CH₄) hat 5 Atome. Aspirin hat 21. Cholesterin hat 74. Wie passen sie alle in den gleichen Betrachtungsraum?

Die Lösung ist **dynamische Skalierung**:

```csharp
// Aus MoleculeRenderer.cs
private float CalculateMoleculeScale(int atomCount) {
    // Basis: 5 Atome = 100% Größe
    // Jede weiteren 5 Atome: -25%
    // Minimum: 15%
    
    float scale = 1.0f - ((atomCount - 5) / 5f * 0.25f);
    return Mathf.Max(scale, 0.15f);
}
```

Kleine Moleküle werden groß dargestellt (einfache Strukturen sollen gut sichtbar sein). Große Moleküle werden verkleinert (damit sie ins Sichtfeld passen). Die Beziehung ist linear mit einem Minimum von 15%, damit auch sehr große Moleküle noch erkennbar sind.

---

## 3.5 Das Tutorial-System: Multimedia in VR

### 3.5.1 Videos im Raum: Chroma-Keying in Echtzeit

Die Erklärvideos wurden vor einem **Greenscreen** aufgenommen. In der VR-Umgebung soll der Sprecher frei im Raum schweben, ohne störenden grünen Hintergrund.

Die Lösung: Ein selbstentwickelter **Chroma-Key-Shader**, der in Echtzeit auf der GPU läuft:

```hlsl
// Aus ChromaKeyShader.shader
fixed4 frag (v2f i) : SV_Target {
    fixed4 col = tex2D(_MainTex, i.uv);
    
    // Wie "grün" ist dieser Pixel?
    float greenness = col.g - max(col.r, col.b);
    
    // Grüne Pixel werden transparent
    float alpha = 1.0 - smoothstep(_Threshold, _Threshold + _Smoothness, greenness);
    col.a = alpha;
    
    return col;
}
```

Der Algorithmus ist simpel aber effektiv:
1. Berechne "Grünheit" = Grün-Kanal minus Maximum von Rot und Blau
2. Wenn Grünheit über einem Schwellwert liegt: Pixel transparent machen
3. Der Smoothness-Parameter verhindert harte Kanten

Die Parameter wurden empirisch optimiert: _Threshold=0.4 und _Smoothness=0.1 ergeben saubere Ränder ohne grüne "Halos" um Personen.

### 3.5.2 ScriptableObjects: Lektionen ohne Code ändern

Jede Tutorial-Lektion ist als **ScriptableObject** definiert – eine Unity-Datenstruktur, die im Editor bearbeitet werden kann:

```
TutorialStep:
  - Titel: "Tetraedrischer Bau"
  - VideoClip: [Referenz auf MP4]
  - MolekülPrefab: CH4Tetraedrisch
  - SpawnPosition: 0.35m vor Kamera
  - AutoAdvance: false
```

Neue Lektionen können hinzugefügt werden, ohne eine Zeile Code zu schreiben. Das macht Iteration schnell – wichtig für didaktische Optimierung nach Nutzertests.

### 3.5.3 Der TutorialManager: Koordination aller Elemente

Eine zentrale Komponente (`TutorialManager.cs`) orchestriert den Ablauf:

1. **Schritt laden:** ScriptableObject-Daten lesen, Video vorbereiten, Prefab instantiieren
2. **Positionierung:** Molekül 35cm vor aktuellem Kopf spawnen, Video daneben
3. **Synchronisation:** Video startet, Molekül rotiert langsam zur Einführung
4. **Navigation:** Auf Nutzereingabe (Controller-Button, Handgeste) oder WebSocket-Befehl reagieren
5. **Cleanup:** Alte Objekte entfernen, Ressourcen freigeben

Die Spawning-Position wird dynamisch berechnet, nicht hart kodiert. Egal wo der Schüler steht oder in welche Richtung er schaut – das Molekül erscheint immer 35cm vor ihm, leicht nach unten versetzt.

---

## 3.6 Interaktion: Greifen, Drehen, Loslassen

### 3.6.1 Direkte Manipulation mit Händen

Das XR Interaction Toolkit von Unity erkennt Handgesten. Für Molekül-Interaktion nutzen wir den **Pinch-Grip**:

1. Daumen und Zeigefinger nähern sich (< 2cm Abstand)
2. Ein Kollisions-Check prüft, ob die "Pinch-Position" ein Molekül berührt
3. Das Molekül wird an die Hand "angeheftet" und folgt ihrer Bewegung
4. Beim Loslassen wird das Molekül an seiner aktuellen Position fixiert

**Optimierungen für natürliches Gefühl:**

- **Dead Zone:** Rotationen unter 2° werden ignoriert (Hand-Zittern)
- **Smoothing:** Rotation wird über mehrere Frames geglättet (Faktor 0.15)
- **Position Lock:** Während der Rotation bleibt die Position fixiert

### 3.6.2 Ray-Casting für UI-Elemente

Buttons und Menüs erfordern präzisere Interaktion als das Greifen großer Objekte. Hier verwenden wir **Ray-Casting**:

Ein unsichtbarer Strahl geht von der Hand aus. Wo er ein UI-Element trifft, erscheint ein Punkt. Eine Pinch-Geste an dieser Position löst einen "Klick" aus.

Das ist intuitiv – wie eine Fernbedienung auf einen Fernseher richten.

---

## 3.7 WebSocket: Kommunikation zwischen VR und iPad

### 3.7.1 Der Server im Headset

Die VR-App startet beim Start einen minimalen **WebSocket-Server**:

```csharp
// Aus WebSocketServer.cs
tcpListener = new TcpListener(IPAddress.Any, port);
tcpListener.Start();
// Wartet auf Verbindungen in separatem Thread
```

Der Server läuft auf Port 8080 und wartet auf Verbindungen. Er kann:
- HTTP-Anfragen für die Web-App beantworten (HTML/JS wird ausgeliefert)
- WebSocket-Verbindungen für Echtzeit-Kommunikation aufbauen
- Befehle empfangen (StartTutorial, NextStep, LoadMolecule, ...)
- Status-Updates senden (aktueller Schritt, geladenes Molekül, ...)

### 3.7.2 Das iPad als Client

Die Lehrkraft öffnet im Safari-Browser die URL `http://[Quest-IP]:8080`. Eine vollständige Web-App wird geladen – keine Installation nötig.

Die App zeigt:
- Aktuellen Tutorial-Status
- Buttons für Navigation (Vor, Zurück, Start, Stop)
- Molekül-Schnellauswahl für curriculumsrelevante Moleküle
- Passthrough-Toggle

Bei Klick auf einen Button wird ein JSON-Befehl per WebSocket gesendet:
```json
{"command": "nextStep"}
```

Die VR-App empfängt dies und führt aus. Die Latenz liegt unter 50ms – praktisch instantan.

---

## 3.8 Performance: 72 FPS auf mobiler Hardware

VR erfordert konstant hohe Frameraten. Unter 72 FPS entsteht Motion Sickness. Auf der Quest 3 – mit mobilem Chip – ist das eine Herausforderung.

**Unsere Maßnahmen:**

1. **Unlit Shader:** Moleküle werden ohne Beleuchtungsberechnung gerendert. Die Farben sind fest, unabhängig von Lichtquellen. Das spart GPU-Zeit und garantiert konsistente Farben.

2. **Object Pooling:** Atom-Kugeln und Bindungs-Zylinder werden wiederverwendet. Statt bei jedem Molekülwechsel 100 Objekte zu löschen und 100 neue zu erstellen, werden die alten recycelt.

3. **Material-Caching:** Materialien (Shader + Parameter) werden einmal erstellt und wiederverwendet. Das verhindert "graue Blitze" beim Erstellen neuer Objekte.

4. **Reduzierte Bond-Klassifikation:** Bindungen werden nur 10x pro Sekunde neu klassifiziert, nicht in jedem Frame.

5. **LOD für große Moleküle:** Ab einer gewissen Entfernung werden vereinfachte Geometrien verwendet.

---

## 3.9 Zusammenfassung: Der rote Faden

Die Systementwicklung folgte einer klaren Logik:

1. **Pädagogisches Ziel:** Schüler sollen verstehen, wie 2D-Formeln 3D-Strukturen codieren
2. **Daraus folgt:** Wir brauchen eine korrekte Keilstrich-Visualisierung, die auf Rotation reagiert
3. **Technische Lösung:** Fixierte Referenzebene + Vorzeichenabstände + dynamische Neuklassifikation
4. **Hardware-Wahl:** Quest 3 für Standalone-VR + iPad für Lehrkraft-Kontrolle
5. **Umsetzung:** Unity mit modularer Architektur, optimiert für mobile Performance

Jede Entscheidung begründet die nächste. Die Keilstrich-Visualisierung erfordert Echtzeit-Berechnung, die wiederum Performance-Optimierung erfordert, die wiederum die Shader-Wahl beeinflusst.

Das Ergebnis ist eine Anwendung, die technische Komplexität vor dem Nutzer verbirgt. Der Schüler sieht einfach ein Molekül, das er drehen kann – und dabei "richtig" reagiert. Er denkt nicht über PCA, Vorzeichenabstände oder WebSocket-Protokolle nach.

Genau das ist gute Bildungstechnologie: Die Technik dient dem Lernen, nicht umgekehrt.
