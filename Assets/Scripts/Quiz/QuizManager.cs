using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Verwaltet das Quiz-System: Fragen laden, State tracken, Antworten prüfen.
/// Singleton – wird einmal in der Szene platziert oder automatisch erzeugt.
/// Kommunikation mit Web-UI läuft über WebSocketServer.BroadcastMessage().
/// </summary>
public class QuizManager : MonoBehaviour
{
    public static QuizManager Instance { get; private set; }

    [Header("References")]
    public WebSocketServer webSocket;
    public MoleculeLibrary moleculeLibrary;
    public QuizDisplay quizDisplay;

    [Header("State (read-only)")]
    [SerializeField] private QuizState state = new QuizState();

    // Alle Fragen des aktuellen Quiz
    private List<QuizQuestion> questions = new List<QuizQuestion>();

    // ──────────────────────────── Lifecycle ────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // Auto-find references
        if (webSocket == null)
            webSocket = FindObjectOfType<WebSocketServer>();
        if (moleculeLibrary == null)
            moleculeLibrary = FindObjectOfType<MoleculeLibrary>();
        if (quizDisplay == null)
            quizDisplay = FindObjectOfType<QuizDisplay>();
    }

    // ──────────────────────────── Öffentliche API ────────────────────────────

    /// <summary>
    /// Startet ein neues Quiz mit den Standard-Fragen (alle Kategorien)
    /// </summary>
    public void StartQuiz()
    {
        StartQuiz(null);
    }

    /// <summary>
    /// Startet ein Quiz gefiltert nach Kategorie (null = alle)
    /// </summary>
    public void StartQuiz(QuizCategory? category)
    {
        var allQuestions = GetDefaultQuestions();

        if (category.HasValue)
        {
            questions = new List<QuizQuestion>();
            foreach (var q in allQuestions)
            {
                if (q.category == category.Value)
                    questions.Add(q);
            }
        }
        else
        {
            questions = allQuestions;
        }

        if (questions.Count == 0)
        {
            Debug.LogWarning($"[Quiz] Keine Fragen für Kategorie {category}");
            Broadcast("{\"type\":\"error\",\"message\":\"Keine Fragen für diese Kategorie\"}");
            return;
        }

        state = new QuizState
        {
            isActive = true,
            currentQuestionIndex = 0,
            score = 0,
            totalQuestions = questions.Count
        };

        // Auto-Rotation deaktivieren während des Quiz
        SetAutoRotation(false);

        // Finger-Collider für Hand Tracking erzeugen (falls nicht vorhanden)
        EnsureFingerTipPokers();

        // Debug.Log($"[Quiz] Quiz gestartet: {questions.Count} Fragen (Kategorie: {category?.ToString() ?? "Alle"})");
        BroadcastState();
        ShowCurrentQuestion();
    }

    /// <summary>
    /// Verarbeitet eine Antwort (Index der gewählten Antwort)
    /// </summary>
    public void SubmitAnswer(int answerIndex)
    {
        if (!state.isActive || state.IsFinished)
        {
            Debug.LogWarning("[Quiz] Quiz nicht aktiv oder bereits beendet");
            return;
        }

        var question = questions[state.currentQuestionIndex];
        bool correct = (answerIndex == question.correctAnswerIndex);

        if (correct)
            state.score++;

        // Debug.Log($"[Quiz] Frage {state.currentQuestionIndex + 1}: " +
        //           $"Antwort {answerIndex} → {(correct ? "RICHTIG" : "FALSCH")}");

        // Ergebnis im VR-Display anzeigen
        if (quizDisplay != null)
            quizDisplay.ShowAnswerResult(answerIndex, question.correctAnswerIndex, question.explanation);

        // Ergebnis an Web-UI senden
        string resultJson = $"{{\"type\":\"quiz_answer_result\"," +
                            $"\"correct\":{correct.ToString().ToLower()}," +
                            $"\"correctIndex\":{question.correctAnswerIndex}," +
                            $"\"selectedIndex\":{answerIndex}," +
                            $"\"explanation\":\"{EscapeJson(question.explanation)}\"," +
                            $"\"score\":{state.score}," +
                            $"\"questionIndex\":{state.currentQuestionIndex}}}";
        Broadcast(resultJson);
    }

    /// <summary>
    /// Geht zur nächsten Frage oder beendet das Quiz
    /// </summary>
    public void NextQuestion()
    {
        if (!state.isActive) return;

        state.currentQuestionIndex++;

        if (state.IsFinished)
        {
            EndQuiz();
            return;
        }

        BroadcastState();
        ShowCurrentQuestion();
    }

    /// <summary>
    /// Beendet das Quiz und sendet Endergebnis
    /// </summary>
    public void EndQuiz()
    {
        state.isActive = false;

        // Auto-Rotation wieder aktivieren
        SetAutoRotation(true);

        // Letztes Molekül entfernen
        if (moleculeLibrary != null)
            moleculeLibrary.ClearCurrentMolecule();

        // Endbildschirm in VR anzeigen
        if (quizDisplay != null)
            quizDisplay.ShowFinished(state.score, state.totalQuestions);

        // Debug.Log($"[Quiz] Quiz beendet! Ergebnis: {state.score}/{state.totalQuestions}");

        string json = $"{{\"type\":\"quiz_finished\"," +
                      $"\"score\":{state.score}," +
                      $"\"total\":{state.totalQuestions}}}";
        Broadcast(json);
    }

    /// <summary>
    /// Gibt den aktuellen State zurück (für externe Abfrage)
    /// </summary>
    public QuizState GetState() => state;

    /// <summary>
    /// Gibt die aktuelle Frage zurück (oder null)
    /// </summary>
    public QuizQuestion GetCurrentQuestion()
    {
        if (!state.isActive || state.IsFinished) return null;
        return questions[state.currentQuestionIndex];
    }

    // ──────────────────────────── Interne Helfer ────────────────────────────

    /// <summary>
    /// Aktiviert oder deaktiviert die Auto-Rotation aller MoleculePlaneAlignment-Objekte.
    /// Während des Quiz soll das Molekül still stehen.
    /// </summary>
    private void SetAutoRotation(bool enabled)
    {
        var alignments = FindObjectsOfType<MoleculePlaneAlignment>();
        foreach (var pa in alignments)
        {
            pa.enableAutoRotation = enabled;
            if (!enabled)
                pa.StopAutoRotation();
        }
        // Debug.Log($"[Quiz] Auto-Rotation {(enabled ? "aktiviert" : "deaktiviert")} ({alignments.Length} Objekte)");
    }

    /// <summary>
    /// Stellt sicher, dass für beide Hände ein FingerTipPoker existiert.
    /// Sucht die Hand-Referenzen aus dem HandRotationController.
    /// </summary>
    private void EnsureFingerTipPokers()
    {
        // Prüfe ob bereits vorhanden
        if (FindObjectOfType<FingerTipPoker>() != null) return;

        var handController = FindObjectOfType<HandRotationController>();
        if (handController == null)
        {
            Debug.LogWarning("[Quiz] Kein HandRotationController gefunden – FingerTipPoker nicht erstellt");
            return;
        }

        if (handController.rightHand != null)
        {
            var pokerR = new GameObject("FingerTipPoker_Right");
            var ftp = pokerR.AddComponent<FingerTipPoker>();
            ftp.hand = handController.rightHand;
            // Debug.Log("[Quiz] FingerTipPoker für rechte Hand erstellt");
        }

        if (handController.leftHand != null)
        {
            var pokerL = new GameObject("FingerTipPoker_Left");
            var ftp = pokerL.AddComponent<FingerTipPoker>();
            ftp.hand = handController.leftHand;
            // Debug.Log("[Quiz] FingerTipPoker für linke Hand erstellt");
        }
    }

    /// <summary>
    /// Sendet die aktuelle Frage als JSON an die Web-UI
    /// </summary>
    private void ShowCurrentQuestion()
    {
        var q = questions[state.currentQuestionIndex];

        // Molekül in VR laden (falls angegeben)
        if (!string.IsNullOrEmpty(q.moleculeName) && moleculeLibrary != null)
        {
            moleculeLibrary.LoadAndDisplayMolecule(q.moleculeName);
        }

        // VR-Display aktualisieren
        if (quizDisplay == null)
        {
            // Auto-create QuizDisplay
            quizDisplay = gameObject.AddComponent<QuizDisplay>();
            // Debug.Log("[Quiz] Auto-created QuizDisplay");
        }
        quizDisplay.ShowQuestion(q, state.currentQuestionIndex, state.totalQuestions, state.score);

        // Frage als JSON senden
        string answersJson = "[";
        for (int i = 0; i < q.answers.Length; i++)
        {
            if (i > 0) answersJson += ",";
            answersJson += $"\"{EscapeJson(q.answers[i])}\"";
        }
        answersJson += "]";

        string json = $"{{\"type\":\"quiz_question\"," +
                      $"\"questionIndex\":{state.currentQuestionIndex}," +
                      $"\"totalQuestions\":{state.totalQuestions}," +
                      $"\"questionText\":\"{EscapeJson(q.questionText)}\"," +
                      $"\"moleculeName\":\"{EscapeJson(q.moleculeName ?? "")}\"," +
                      $"\"category\":\"{q.category}\"," +
                      $"\"answers\":{answersJson}," +
                      $"\"score\":{state.score}}}";
        Broadcast(json);
    }

    /// <summary>
    /// Sendet den aktuellen Quiz-State an die Web-UI
    /// </summary>
    private void BroadcastState()
    {
        string json = $"{{\"type\":\"quiz_state\"," +
                      $"\"isActive\":{state.isActive.ToString().ToLower()}," +
                      $"\"currentQuestion\":{state.currentQuestionIndex}," +
                      $"\"totalQuestions\":{state.totalQuestions}," +
                      $"\"score\":{state.score}}}";
        Broadcast(json);
    }

    private void Broadcast(string json)
    {
        if (webSocket != null)
            webSocket.BroadcastMessage(json);
    }

    /// <summary>
    /// Einfaches JSON-Escaping für Strings
    /// </summary>
    private string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }

    // ──────────────────────────── Standard-Fragen ────────────────────────────

    /// <summary>
    /// Gibt eine Liste hart codierter Starter-Fragen zurück.
    /// Später ersetzbar durch JSON-Datei oder dynamische Generierung.
    /// </summary>
    private List<QuizQuestion> GetDefaultQuestions()
    {
        return new List<QuizQuestion>
        {
            // ═══════════ KATEGORIE 1: Keilstrichformel (10 Fragen, Klasse 9) ═══════════

            // Frage 1
            new QuizQuestion
            {
                questionText = "Was bedeutet ein massiv ausgefüllter, dicker Keil in einer Keilstrichformel?",
                moleculeName = "",
                imageName = "KeilstrichBild",
                answers = new[] {
                    "Das Atom liegt flach in der Zeichenebene.",
                    "Das Atom ragt räumlich nach vorne, auf den Betrachter zu.",
                    "Das Atom ragt räumlich in den Hintergrund.",
                    "Es handelt sich um eine Doppelbindung."
                },
                correctAnswerIndex = 1,
                category = QuizCategory.Keilstrich,
                explanation = "Ein massiv ausgefüllter Keil zeigt an, dass das Atom räumlich aus der Zeichenebene heraus auf den Betrachter zukommt."
            },
            // Frage 2
            new QuizQuestion
            {
                questionText = "Was bedeutet eine gestrichelte Linie (gestrichelter Keil) in einer Keilstrichformel?",
                moleculeName = "",
                imageName = "KeilstrichBild",
                answers = new[] {
                    "Das Atom liegt in der Zeichenebene.",
                    "Das Atom ist nur schwach gebunden.",
                    "Das Atom ragt räumlich in den Hintergrund.",
                    "Es handelt sich um eine Wasserstoffbrücke."
                },
                correctAnswerIndex = 2,
                category = QuizCategory.Keilstrich,
                explanation = "Eine gestrichelte Linie (gestrichelter Keil) bedeutet, dass das Atom hinter die Zeichenebene ragt, also vom Betrachter weg."
            },
            // Frage 3
            new QuizQuestion
            {
                questionText = "Welche Position haben Atome, die mit normalen durchgezogenen Linien gezeichnet sind?",
                moleculeName = "",
                imageName = "KeilstrichBild",
                answers = new[] {
                    "Sie ragen nach vorne.",
                    "Sie liegen flach in der Zeichenebene.",
                    "Sie ragen nach hinten.",
                    "Sie schweben frei im Raum."
                },
                correctAnswerIndex = 1,
                category = QuizCategory.Keilstrich,
                explanation = "Normale durchgezogene Linien stehen für Bindungen, die in der Zeichenebene liegen."
            },
            // Frage 4
            new QuizQuestion
            {
                questionText = "Warum wird Methan (CH4) als Tetraeder dargestellt und nicht flach?",
                moleculeName = "methane",
                answers = new[] {
                    "Weil Kohlenstoff immer Ringe bildet.",
                    "Weil sich die Elektronenpaare gegenseitig abstoßen und maximalen Abstand einnehmen.",
                    "Weil Wasserstoff magnetisch ist.",
                    "Weil die Bindungen alle verschieden lang sind."
                },
                correctAnswerIndex = 1,
                category = QuizCategory.Keilstrich,
                explanation = "Die vier Bindungselektronenpaare stoßen sich gegenseitig ab und nehmen den größtmöglichen Abstand ein – das ergibt eine Tetraederform mit 109,5°-Winkeln."
            },
            // Frage 5
            new QuizQuestion
            {
                questionText = "Ammoniak (NH3) hat ein freies Elektronenpaar am Stickstoff. Welche Auswirkung hat das auf die Molekülform?",
                moleculeName = "ammonia",
                answers = new[] {
                    "Das Molekül wird flach.",
                    "Es entsteht eine dreiseitige Pyramide (trigonale Pyramide).",
                    "Es bleibt ein perfekter Tetraeder.",
                    "Das Molekül wird linear."
                },
                correctAnswerIndex = 1,
                category = QuizCategory.Keilstrich,
                explanation = "Das freie Elektronenpaar beansprucht mehr Raum als eine Bindung und drückt die drei N-H-Bindungen nach unten – es entsteht eine trigonale Pyramide."
            },
            // Frage 6
            new QuizQuestion
            {
                questionText = "Warum ist das Wassermolekül (H2O) gewinkelt und nicht linear?",
                moleculeName = "water",
                answers = new[] {
                    "Weil am Sauerstoff zwei freie Elektronenpaare sitzen, die Platz brauchen.",
                    "Weil Wasserstoff zu leicht ist.",
                    "Weil die O-H-Bindung eine Doppelbindung ist.",
                    "Weil Sauerstoff nur eine Bindung eingehen kann."
                },
                correctAnswerIndex = 0,
                category = QuizCategory.Keilstrich,
                explanation = "Sauerstoff besitzt zwei freie Elektronenpaare, die zusammen mit den zwei Bindungen eine tetraedrische Anordnung bilden. Sichtbar sind nur die zwei O-H-Bindungen → gewinkelt (ca. 104,5°)."
            },
            // Frage 7
            new QuizQuestion
            {
                questionText = "Warum zeichnet man die Kohlenstoffkette von Butan in einer Zickzack-Linie statt gerade?",
                moleculeName = "",
                answers = new[] {
                    "Weil die C-Atome abwechselnd geladen sind.",
                    "Weil jedes C-Atom eine tetraedrische Umgebung hat (109,5°-Winkel).",
                    "Weil Butan ein Ring ist.",
                    "Weil alle Bindungen Doppelbindungen sind."
                },
                correctAnswerIndex = 1,
                category = QuizCategory.Keilstrich,
                explanation = "Jedes sp3-hybridisierte C-Atom hat Bindungswinkel von ca. 109,5°, was eine zickzackförmige Kette ergibt."
            },
            // Frage 8
            new QuizQuestion
            {
                questionText = "Was ist eine besondere Eigenschaft der C-C-Einfachbindung im Ethan?",
                moleculeName = "ethane",
                answers = new[] {
                    "Sie ist starr und nicht drehbar.",
                    "Sie ist kürzer als eine Doppelbindung.",
                    "Sie existiert nur in der Gasphase.",
                    "Sie ist frei drehbar."
                },
                correctAnswerIndex = 3,
                category = QuizCategory.Keilstrich,
                explanation = "Eine C-C-Einfachbindung (Sigma-Bindung) erlaubt freie Rotation der beiden Molekülhälften gegeneinander."
            },
            // Frage 9
            new QuizQuestion
            {
                questionText = "Was passiert bei einer Doppelbindung (wie in Ethen) mit der freien Drehbarkeit?",
                moleculeName = "ethene",
                answers = new[] {
                    "Die Drehbarkeit bleibt voll erhalten.",
                    "Die Bindung wird länger und flexibler.",
                    "Die Doppelbindung ist nicht frei drehbar – das Molekül ist planar.",
                    "Nur bei hoher Temperatur ist Drehung möglich."
                },
                correctAnswerIndex = 2,
                category = QuizCategory.Keilstrich,
                explanation = "Die Pi-Bindung der Doppelbindung blockiert die Rotation. Alle vier Atome von Ethen liegen daher in einer Ebene (planar)."
            },
            // Frage 10
            new QuizQuestion
            {
                questionText = "Warum ist der Sechsring von Cyclohexan nicht flach, obwohl man ihn oft so zeichnet?",
                moleculeName = "cyclohexane",
                answers = new[] {
                    "Weil Kohlenstoff nicht genug Bindungen hat.",
                    "Weil die Tetraederwinkel (109,5°) einen flachen Ring nicht erlauben – er nimmt die Sesselform ein.",
                    "Weil Cyclohexan in Wirklichkeit ein Fünfring ist.",
                    "Weil die Wasserstoffatome den Ring nach oben drücken."
                },
                correctAnswerIndex = 1,
                category = QuizCategory.Keilstrich,
                explanation = "Ein flacher Sechsring hätte 120°-Winkel, aber sp3-Kohlenstoff braucht 109,5°. Die spannungsfreie Sesselkonformation löst dieses Problem."
            },

            // ═══════════ KATEGORIE 2: Chiralität (10 Fragen, Klasse 11) ═══════════

            // Frage 1
            new QuizQuestion
            {
                questionText = "Ethanol (C2H5OH) und Dimethylether (CH3OCH3) haben die gleiche Summenformel C2H6O. Welche Art von Isomerie liegt vor?",
                moleculeName = "",
                answers = new[] {
                    "Konstitutionsisomerie (unterschiedliche Verknüpfung der Atome).",
                    "Enantiomerie (Spiegelbildisomerie).",
                    "cis-trans-Isomerie.",
                    "Konformationsisomerie."
                },
                correctAnswerIndex = 0,
                category = QuizCategory.Chirality,
                explanation = "Bei Konstitutionsisomeren ist die Reihenfolge der Atomverknüpfungen unterschiedlich, obwohl die Summenformel identisch ist."
            },
            // Frage 2
            new QuizQuestion
            {
                questionText = "Welche Voraussetzung muss ein Kohlenstoffatom erfüllen, damit es ein Chiralitätszentrum ist?",
                moleculeName = "",
                answers = new[] {
                    "Es muss an einen Ring gebunden sein.",
                    "Es muss vier verschiedene Substituenten tragen.",
                    "Es muss eine Doppelbindung besitzen.",
                    "Es muss an Sauerstoff gebunden sein."
                },
                correctAnswerIndex = 1,
                category = QuizCategory.Chirality,
                explanation = "Ein Chiralitätszentrum liegt vor, wenn ein Kohlenstoffatom vier unterschiedliche Substituenten trägt. Dann ist das Molekül nicht mit seinem Spiegelbild deckungsgleich."
            },
            // Frage 3
            new QuizQuestion
            {
                questionText = "Wie nennt man zwei Moleküle, die sich wie Bild und Spiegelbild verhalten und nicht zur Deckung gebracht werden können?",
                moleculeName = "",
                answers = new[] {
                    "Enantiomere.",
                    "Diastereomere.",
                    "Konstitutionsisomere.",
                    "Mesomere."
                },
                correctAnswerIndex = 0,
                category = QuizCategory.Chirality,
                explanation = "Enantiomere sind Stereoisomere, die sich wie Bild und Spiegelbild verhalten – vergleichbar mit linker und rechter Hand."
            },
            // Frage 4
            new QuizQuestion
            {
                questionText = "Wo befindet sich das Chiralitätszentrum bei 2-Butanol (CH3-CHOH-CH2-CH3)?",
                moleculeName = "2-butanol",
                answers = new[] {
                    "Am C1.",
                    "Am C3.",
                    "Es gibt kein Chiralitätszentrum.",
                    "Am C2, weil dort vier verschiedene Substituenten hängen (H, OH, CH3, C2H5)."
                },
                correctAnswerIndex = 3,
                category = QuizCategory.Chirality,
                explanation = "Das C2-Atom trägt vier verschiedene Gruppen: -H, -OH, -CH3 und -CH2CH3. Damit ist es ein Chiralitätszentrum."
            },
            // Frage 5
            new QuizQuestion
            {
                questionText = "Ist Propan-2-ol (Isopropanol) chiral?",
                moleculeName = "propan-2-ol",
                answers = new[] {
                    "Ja, wegen der OH-Gruppe.",
                    "Ja, weil es drei C-Atome hat.",
                    "Nein, weil am mittleren C zwei identische Methylgruppen (-CH3) hängen.",
                    "Nein, weil Alkohole nie chiral sind."
                },
                correctAnswerIndex = 2,
                category = QuizCategory.Chirality,
                explanation = "Das mittlere C-Atom trägt zweimal dieselbe Gruppe (-CH3). Damit hat es keine vier verschiedenen Substituenten und ist nicht chiral."
            },
            // Frage 6
            new QuizQuestion
            {
                questionText = "Wie kann man im Labor zwei Enantiomere voneinander unterscheiden?",
                moleculeName = "",
                answers = new[] {
                    "Mit einem Polarimeter (Drehung von polarisiertem Licht).",
                    "Durch ihren Schmelzpunkt.",
                    "Durch ihre Farbe.",
                    "Durch ihre Molmasse."
                },
                correctAnswerIndex = 0,
                category = QuizCategory.Chirality,
                explanation = "Enantiomere drehen die Ebene von linear polarisiertem Licht in entgegengesetzte Richtungen. Ein Polarimeter misst diese Drehung."
            },
            // Frage 7
            new QuizQuestion
            {
                questionText = "Was versteht man unter einer 50:50-Mischung aus Links- und Rechts-Enantiomer?",
                moleculeName = "",
                answers = new[] {
                    "Eine besonders reine Substanz.",
                    "Ein Racemat – es ist optisch inaktiv, da sich die Drehungen aufheben.",
                    "Ein Diastereomerengemisch.",
                    "Eine Meso-Verbindung."
                },
                correctAnswerIndex = 1,
                category = QuizCategory.Chirality,
                explanation = "In einem Racemat (racemischen Gemisch) heben sich die entgegengesetzten Drehwerte beider Enantiomere exakt auf – netto keine optische Aktivität."
            },
            // Frage 8
            new QuizQuestion
            {
                questionText = "Wie nennt man den Überbegriff für Stereoisomere, die sich NICHT wie Bild und Spiegelbild verhalten?",
                moleculeName = "",
                answers = new[] {
                    "Enantiomere.",
                    "Konstitutionsisomere.",
                    "Konformere.",
                    "Diastereomere."
                },
                correctAnswerIndex = 3,
                category = QuizCategory.Chirality,
                explanation = "Diastereomere sind Stereoisomere, die kein Spiegelbildpaar bilden. Beispiele sind cis/trans-Isomere oder Zucker mit mehreren Chiralitätszentren."
            },
            // Frage 9
            new QuizQuestion
            {
                questionText = "Meso-Weinsäure hat zwei Chiralitätszentren, ist aber trotzdem achiral. Warum?",
                moleculeName = "meso-tartaric acid",
                answers = new[] {
                    "Weil die Chiralitätszentren zu weit auseinander liegen.",
                    "Weil sie nur in Wasser vorkommt.",
                    "Weil eine interne Spiegelebene die Chiralität aufhebt.",
                    "Weil Weinsäure immer achiral ist."
                },
                correctAnswerIndex = 2,
                category = QuizCategory.Chirality,
                explanation = "In der Meso-Weinsäure spiegelt sich die obere Molekülhälfte in der unteren. Die beiden Chiralitätszentren kompensieren sich durch eine innere Spiegelebene."
            },
            // Frage 10
            new QuizQuestion
            {
                questionText = "Warum ist es in der Pharmazie so wichtig, das richtige Enantiomer eines Wirkstoffs einzusetzen?",
                moleculeName = "",
                answers = new[] {
                    "Weil Enantiomere unterschiedliche Molmassen haben.",
                    "Weil Enzyme und Rezeptoren im Körper chiral sind (Schlüssel-Schloss-Prinzip) und nur ein Enantiomer passt.",
                    "Weil das andere Enantiomer immer giftig ist.",
                    "Weil Enantiomere verschiedene Summenformeln haben."
                },
                correctAnswerIndex = 1,
                category = QuizCategory.Chirality,
                explanation = "Biologische Rezeptoren und Enzyme sind selbst chiral. Sie können zwischen Enantiomeren unterscheiden – nur eines passt optimal (Schlüssel-Schloss-Prinzip)."
            }
        };
    }
}
