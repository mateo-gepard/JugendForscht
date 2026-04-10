using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// VR Quiz-Display: Frage-Panel oben, Antwort-Buttons unten in einer Reihe.
/// Die Mitte bleibt frei für Moleküle / 2D-Einblendungen.
/// Alle Elemente sind dynamisch — Anzahl der Buttons variabel.
/// </summary>
public class QuizDisplay : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Abstand vom Spieler (Meter)")]
    public float displayDistance = 0.8f;

    [Tooltip("Höhe des Frage-Panels über Augenhöhe")]
    public float questionHeight = 0.35f;

    [Tooltip("Höhe der Buttons unter Augenhöhe")]
    public float buttonRowHeight = -0.3f;

    [Tooltip("Abstand der Buttons vom Spieler (näher als Frage-Panel)")]
    public float buttonDistance = 0.55f;

    [Tooltip("Mindestbreite eines Antwort-Buttons")]
    public float minButtonWidth = 0.12f;

    [Tooltip("Breite pro Zeichen (für dynamische Button-Breite)")]
    public float charWidth = 0.005f;

    [Tooltip("Höhe eines Antwort-Buttons")]
    public float buttonHeight = 0.04f;

    [Tooltip("Abstand zwischen Buttons")]
    public float buttonSpacing = 0.015f;

    [Tooltip("Maximale Zeichenbreite pro Zeile auf Buttons")]
    public int maxCharsPerLine = 40;

    [Header("Colors")]
    public Color panelColor = new Color(0.05f, 0.05f, 0.1f, 0.85f);
    public Color questionTextColor = Color.white;
    public Color scoreTextColor = new Color(0.5f, 0.8f, 1f, 1f);

    // Erzeugte Objekte
    private GameObject displayRoot;
    private GameObject questionPanel;
    private GameObject questionBg;
    private GameObject buttonRow;
    private TextMesh questionLabel;
    private TextMesh scoreLabel;
    private TextMesh categoryLabel;
    private List<QuizButton> activeButtons = new List<QuizButton>();
    private GameObject nextButton;
    private GameObject imageQuad;       // 2D-Bild-Einblendung
    private bool answerLocked = false;

    // ──────────────────────────── Öffentliche API ────────────────────────────

    /// <summary>
    /// Zeigt eine Frage an. Buttons werden dynamisch erzeugt (variable Anzahl).
    /// Mitte bleibt frei für Molekül-Einblendungen.
    /// </summary>
    public void ShowQuestion(QuizQuestion question, int questionIndex, int totalQuestions, int currentScore)
    {
        ClearButtons();

        if (displayRoot == null)
            CreateDisplay();

        PositionDisplay();

        // Frage-Text (mit Zeilenumbruch)
        string wrappedQuestion = WrapText(question.questionText, 55);
        questionLabel.text = $"Frage {questionIndex + 1}/{totalQuestions}\n{wrappedQuestion}";
        scoreLabel.text = $"Punkte: {currentScore}";
        categoryLabel.text = question.category == QuizCategory.Keilstrich
            ? "Keilstrichformel" : "Chiralität";
        // Frage-Panel Hintergrund dynamisch an Textl\u00e4nge anpassen
        AdjustQuestionPanelSize(question.questionText);

        // 2D-Bild anzeigen (falls vorhanden)
        Hide2DImage();
        if (!string.IsNullOrEmpty(question.imageName))
            Show2DImage(question.imageName);

        // Buttons vertikal untereinander erzeugen — Höhe dynamisch je nach Text
        int count = question.answers.Length;

        // Breite und Höhe pro Button berechnen
        float maxBtnWidth = 0f;
        float[] heights = new float[count];
        string[] wrappedTexts = new string[count];
        for (int i = 0; i < count; i++)
        {
            wrappedTexts[i] = WrapText(question.answers[i], maxCharsPerLine);
            int lines = 1;
            foreach (char c in wrappedTexts[i]) { if (c == '\n') lines++; }
            int maxLen = 0; int cur = 0;
            foreach (char c in wrappedTexts[i]) { if (c == '\n') { cur = 0; } else { cur++; if (cur > maxLen) maxLen = cur; } }
            float w = Mathf.Max(minButtonWidth, maxLen * charWidth + 0.02f);
            if (w > maxBtnWidth) maxBtnWidth = w;
            heights[i] = Mathf.Max(buttonHeight, lines * 0.018f + 0.015f);
        }

        // Alle Buttons gleich breit (die breiteste bestimmt)
        float totalHeight = 0f;
        for (int i = 0; i < count; i++) totalHeight += heights[i];
        totalHeight += (count - 1) * buttonSpacing;

        float yCursor = totalHeight / 2f;

        for (int i = 0; i < count; i++)
        {
            var button = CreateAnswerButton(i, wrappedTexts[i], maxBtnWidth, heights[i]);
            float yPos = yCursor - heights[i] / 2f;
            button.transform.localPosition = new Vector3(0f, yPos, 0f);
            yCursor -= heights[i] + buttonSpacing;
            activeButtons.Add(button);
        }

        answerLocked = false;

        HideNextButton();
        displayRoot.SetActive(true);
    }

    /// <summary>
    /// Zeigt Ergebnis-Farbe auf Buttons (grün = richtig, rot = falsch)
    /// </summary>
    public void ShowAnswerResult(int selectedIndex, int correctIndex, string explanation)
    {
        // Alle Buttons sperren nach Antwort
        for (int i = 0; i < activeButtons.Count; i++)
        {
            if (i == correctIndex)
                activeButtons[i].ShowResult(true);
            else if (i == selectedIndex && selectedIndex != correctIndex)
                activeButtons[i].ShowResult(false);
            else
                activeButtons[i].ShowResult(false); // Auch restliche Buttons sperren
        }

        if (questionLabel != null && !string.IsNullOrEmpty(explanation))
        {
            questionLabel.text += $"\n\n{explanation}";
            AdjustQuestionPanelSize(questionLabel.text);
        }

        // "Weiter"-Button einblenden
        ShowNextButton();
    }

    /// <summary>
    /// Zeigt Endbildschirm mit Score
    /// </summary>
    public void ShowFinished(int score, int total)
    {
        ClearButtons();

        if (displayRoot == null)
            CreateDisplay();

        PositionDisplay();

        float percent = (float)score / total * 100f;
        string rating;
        if (percent >= 80) rating = "Ausgezeichnet!";
        else if (percent >= 60) rating = "Gut gemacht!";
        else if (percent >= 40) rating = "Nicht schlecht.";
        else rating = "Weiter üben!";

        questionLabel.text = $"Quiz beendet!\n\n{score} von {total} richtig ({percent:F0}%)\n{rating}";
        scoreLabel.text = "";
        categoryLabel.text = "Ergebnis";

        AdjustQuestionPanelSize(questionLabel.text);

        displayRoot.SetActive(true);

        // Nach 3.5 Sekunden automatisch ausblenden
        StartCoroutine(HideAfterDelay(3.5f));
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Hide();
    }

    /// <summary>
    /// Versteckt das gesamte Quiz-Display
    /// </summary>
    public void Hide()
    {
        ClearButtons();
        HideNextButton();
        Hide2DImage();
        if (displayRoot != null)
            displayRoot.SetActive(false);
    }

    // ──────────────────────────── Display erzeugen ────────────────────────────

    private void CreateDisplay()
    {
        displayRoot = new GameObject("QuizDisplay");
        displayRoot.transform.SetParent(transform);

        // === FRAGE-PANEL (oben, über dem Molekül) ===
        questionPanel = new GameObject("QuestionPanel");
        questionPanel.transform.SetParent(displayRoot.transform, false);

        // Hintergrund
        questionBg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        questionBg.name = "QuestionBG";
        questionBg.transform.SetParent(questionPanel.transform, false);
        questionBg.transform.localScale = new Vector3(0.7f, 0.22f, 1f);
        var bgCol = questionBg.GetComponent<Collider>();
        if (bgCol != null) Destroy(bgCol);

        var bgMat = CreateTransparentMaterial(panelColor);
        questionBg.GetComponent<Renderer>().material = bgMat;

        // Fragetext
        var qObj = new GameObject("QuestionText");
        qObj.transform.SetParent(questionPanel.transform, false);
        qObj.transform.localPosition = new Vector3(0f, 0.02f, -0.01f);
        questionLabel = qObj.AddComponent<TextMesh>();
        questionLabel.fontSize = 24;
        questionLabel.characterSize = 0.008f;
        questionLabel.anchor = TextAnchor.MiddleCenter;
        questionLabel.alignment = TextAlignment.Center;
        questionLabel.color = questionTextColor;

        // Score oben rechts
        var sObj = new GameObject("ScoreText");
        sObj.transform.SetParent(questionPanel.transform, false);
        sObj.transform.localPosition = new Vector3(0.28f, 0.08f, -0.01f);
        scoreLabel = sObj.AddComponent<TextMesh>();
        scoreLabel.fontSize = 22;
        scoreLabel.characterSize = 0.009f;
        scoreLabel.anchor = TextAnchor.UpperRight;
        scoreLabel.alignment = TextAlignment.Right;
        scoreLabel.color = scoreTextColor;

        // Kategorie oben links
        var cObj = new GameObject("CategoryText");
        cObj.transform.SetParent(questionPanel.transform, false);
        cObj.transform.localPosition = new Vector3(-0.28f, 0.08f, -0.01f);
        categoryLabel = cObj.AddComponent<TextMesh>();
        categoryLabel.fontSize = 22;
        categoryLabel.characterSize = 0.009f;
        categoryLabel.anchor = TextAnchor.UpperLeft;
        categoryLabel.alignment = TextAlignment.Left;
        categoryLabel.color = new Color(1f, 0.85f, 0.3f, 1f);

        // === BUTTON-ROW (unten, unter dem Molekül) ===
        buttonRow = new GameObject("ButtonRow");
        buttonRow.transform.SetParent(displayRoot.transform, false);
    }

    private void PositionDisplay()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 basePos = cam.transform.position + forward * displayDistance;

        // Frage-Panel oben
        questionPanel.transform.position = basePos + Vector3.up * questionHeight;
        questionPanel.transform.rotation = Quaternion.LookRotation(forward);

        // Button-Reihe unten — näher am Spieler
        Vector3 buttonPos = cam.transform.position + forward * buttonDistance;
        buttonRow.transform.position = buttonPos + Vector3.up * buttonRowHeight;
        buttonRow.transform.rotation = Quaternion.LookRotation(forward);
    }

    /// <summary>
    /// Passt die Größe des Frage-Panel-Hintergrunds an die Textlänge an.
    /// </summary>
    private void AdjustQuestionPanelSize(string questionText)
    {
        if (questionBg == null) return;

        // Breite: basierend auf Zeichenlänge, mindestens 0.5m, maximal 1.2m
        int maxLineLen = 0;
        int lineCount = 1;
        int currentLineLen = 0;
        // "Frage X/Y\n" prefix adds ~12 chars + the question text
        string fullText = questionLabel.text;
        foreach (char c in fullText)
        {
            if (c == '\n') { lineCount++; currentLineLen = 0; }
            else { currentLineLen++; if (currentLineLen > maxLineLen) maxLineLen = currentLineLen; }
        }
        // +1 for the "Frage X/Y" line
        lineCount = Mathf.Max(lineCount, 1);

        float width = Mathf.Clamp(maxLineLen * 0.011f + 0.1f, 0.5f, 1.2f);
        float height = Mathf.Clamp(lineCount * 0.04f + 0.06f, 0.12f, 0.5f);

        questionBg.transform.localScale = new Vector3(width, height, 1f);

        // Score und Kategorie-Label Positionen anpassen
        if (scoreLabel != null)
            scoreLabel.transform.localPosition = new Vector3(width / 2f - 0.02f, height / 2f - 0.02f, -0.01f);
        if (categoryLabel != null)
            categoryLabel.transform.localPosition = new Vector3(-width / 2f + 0.02f, height / 2f - 0.02f, -0.01f);
    }

    // ──────────────────────────── Buttons erzeugen ────────────────────────────

    private QuizButton CreateAnswerButton(int index, string wrappedText, float width, float height)
    {
        var buttonObj = new GameObject($"AnswerButton_{index}");
        buttonObj.transform.SetParent(buttonRow.transform, false);

        // Visueller Cube — Breite und Höhe dynamisch
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "ButtonVisual";
        visual.transform.SetParent(buttonObj.transform, false);
        visual.transform.localScale = new Vector3(width, height, 0.02f);

        var cubeCollider = visual.GetComponent<Collider>();
        if (cubeCollider != null) Destroy(cubeCollider);

        // Trigger-Collider (etwas größer als Button)
        var boxCol = buttonObj.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(width + 0.015f, height + 0.015f, 0.05f);
        boxCol.isTrigger = true;

        // Text-Label
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(buttonObj.transform, false);
        labelObj.transform.localPosition = new Vector3(0f, 0f, -0.015f);
        var textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = wrappedText;
        textMesh.fontSize = 18;
        textMesh.characterSize = 0.005f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;

        // QuizButton-Komponente
        var quizButton = buttonObj.AddComponent<QuizButton>();
        quizButton.Setup(index, wrappedText);
        quizButton.OnPressed += OnAnswerButtonPressed;

        return quizButton;
    }

    private void OnAnswerButtonPressed(int answerIndex)
    {
        // Doppelklick verhindern
        if (answerLocked) return;
        answerLocked = true;

        var quiz = QuizManager.Instance;
        if (quiz == null) return;

        quiz.SubmitAnswer(answerIndex);
        // ShowAnswerResult wird bereits von SubmitAnswer aufgerufen
    }

    // ──────────────────────────── Weiter-Button ────────────────────────────

    private void ShowNextButton()
    {
        if (nextButton != null) Destroy(nextButton);

        // Weltposition: mittig zwischen Frage-Panel und Buttons
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 centerPos = cam.transform.position + forward * buttonDistance;
        // Auf Augenhöhe (y=0 relativ zur Kamera)

        nextButton = new GameObject("NextButton");
        nextButton.transform.position = centerPos;
        nextButton.transform.rotation = Quaternion.LookRotation(forward);

        float nw = 0.25f;
        float nh = 0.08f;

        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "NextVisual";
        visual.transform.SetParent(nextButton.transform, false);
        visual.transform.localScale = new Vector3(nw, nh, 0.02f);
        var vc = visual.GetComponent<Collider>();
        if (vc != null) Destroy(vc);

        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.2f, 0.7f, 0.3f, 1f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.2f, 0.7f, 0.3f) * 0.3f);
        visual.GetComponent<Renderer>().material = mat;

        var boxCol = nextButton.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(nw + 0.02f, nh + 0.02f, 0.06f);
        boxCol.isTrigger = true;

        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(nextButton.transform, false);
        labelObj.transform.localPosition = new Vector3(0f, 0f, -0.015f);
        var tm = labelObj.AddComponent<TextMesh>();
        tm.text = "Weiter ›";
        tm.fontSize = 30;
        tm.characterSize = 0.008f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        tm.fontStyle = FontStyle.Bold;

        var qb = nextButton.AddComponent<QuizButton>();
        qb.normalColor = new Color(0.2f, 0.7f, 0.3f, 1f);
        qb.hoverColor = new Color(0.3f, 0.85f, 0.4f, 1f);
        qb.Setup(-1, "Weiter");
        qb.OnPressed += OnNextButtonPressed;
    }

    private void HideNextButton()
    {
        if (nextButton != null)
        {
            var qb = nextButton.GetComponent<QuizButton>();
            if (qb != null) qb.OnPressed -= OnNextButtonPressed;
            Destroy(nextButton);
            nextButton = null;
        }
    }

    private void OnNextButtonPressed(int _)
    {
        HideNextButton();
        Hide2DImage();
        var quiz = QuizManager.Instance;
        if (quiz != null)
            quiz.NextQuestion();
    }

    // ──────────────────────────── 2D-Bild ────────────────────────────

    private void Show2DImage(string imageName)
    {
        Hide2DImage();

        Texture2D tex = Resources.Load<Texture2D>(imageName);
        if (tex == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0;
        forward.Normalize();

        imageQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        imageQuad.name = "QuizImage2D";

        // Collider entfernen
        var col = imageQuad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Größe: Seitenverhältnis beibehalten, max 0.3m hoch
        float maxHeight = 0.3f;
        float aspect = (float)tex.width / tex.height;
        float h = maxHeight;
        float w = h * aspect;
        imageQuad.transform.localScale = new Vector3(w, h, 1f);

        // Position: mittig auf Augenhöhe, gleiche Entfernung wie Frage-Panel
        Vector3 pos = cam.transform.position + forward * displayDistance + Vector3.up * 0.02f;
        imageQuad.transform.position = pos;
        imageQuad.transform.rotation = Quaternion.LookRotation(forward);

        // Material: Unlit mit Textur
        var mat = new Material(Shader.Find("Unlit/Transparent"));
        if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            mat = new Material(Shader.Find("Unlit/Texture"));
        mat.mainTexture = tex;
        imageQuad.GetComponent<Renderer>().material = mat;
    }

    private void Hide2DImage()
    {
        if (imageQuad != null)
        {
            Destroy(imageQuad);
            imageQuad = null;
        }
    }

    // ──────────────────────────── Helfer ────────────────────────────

    /// <summary>
    /// Bricht Text in Zeilen um, wenn er länger als maxChars ist.
    /// </summary>
    private string WrapText(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;

        var sb = new System.Text.StringBuilder();
        int lineLen = 0;
        string[] words = text.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (i > 0 && lineLen + words[i].Length + 1 > maxChars)
            {
                sb.Append('\n');
                lineLen = 0;
            }
            else if (i > 0)
            {
                sb.Append(' ');
                lineLen++;
            }
            sb.Append(words[i]);
            lineLen += words[i].Length;
        }
        return sb.ToString();
    }

    private Material CreateTransparentMaterial(Color color)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        return mat;
    }

    private void ClearButtons()
    {
        foreach (var btn in activeButtons)
        {
            if (btn != null)
            {
                btn.OnPressed -= OnAnswerButtonPressed;
                Destroy(btn.gameObject);
            }
        }
        activeButtons.Clear();
    }

    void OnDestroy()
    {
        ClearButtons();
        if (displayRoot != null)
            Destroy(displayRoot);
    }
}
