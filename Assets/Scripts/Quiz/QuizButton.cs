using UnityEngine;
using System;

/// <summary>
/// Physischer 3D-Button für VR-Quiz.
/// Reagiert auf OnTriggerEnter mit Hand-Collidern (Oculus Hand Tracking).
/// Visuelles Feedback: Farbumschlag bei Berührung + Skalierungsanimation.
/// </summary>
[RequireComponent(typeof(Collider))]
public class QuizButton : MonoBehaviour
{
    [Header("Settings")]
    public int answerIndex;                 // Welche Antwort dieser Button repräsentiert
    public string answerText = "";          // Angezeigte Antwort

    [Header("Visual")]
    public Color normalColor = new Color(0.2f, 0.2f, 0.3f, 1f);
    public Color hoverColor = new Color(0.3f, 0.4f, 0.8f, 1f);
    public Color correctColor = new Color(0.1f, 0.8f, 0.2f, 1f);
    public Color wrongColor = new Color(0.8f, 0.1f, 0.1f, 1f);

    [Header("Cooldown")]
    public float cooldownTime = 1.0f;       // Verhindert Doppelklick

    /// <summary>
    /// Wird ausgelöst wenn der Button gedrückt wird. Parameter: answerIndex
    /// </summary>
    public event Action<int> OnPressed;

    // Interne Referenzen
    private Renderer buttonRenderer;
    private TextMesh label;
    private Material buttonMaterial;
    private Vector3 originalScale;
    private float lastPressTime = -10f;
    private bool isLocked = false;          // Gesperrt nach Antwort bis nächste Frage

    void Awake()
    {
        buttonRenderer = GetComponent<Renderer>();
        if (buttonRenderer == null)
            buttonRenderer = GetComponentInChildren<Renderer>();

        label = GetComponentInChildren<TextMesh>();
        originalScale = transform.localScale;

        // Eigenes Material erstellen (keine Instanz-Kollisionen)
        if (buttonRenderer != null)
        {
            buttonMaterial = new Material(Shader.Find("Standard"));
            buttonMaterial.color = normalColor;
            buttonMaterial.EnableKeyword("_EMISSION");
            buttonRenderer.material = buttonMaterial;
        }

        // Collider muss Trigger sein
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    /// <summary>
    /// Setzt den angezeigten Text und Index
    /// </summary>
    public void Setup(int index, string text)
    {
        answerIndex = index;
        answerText = text;

        if (label == null)
            label = GetComponentInChildren<TextMesh>();
        if (label != null)
            label.text = text;
    }

    /// <summary>
    /// Setzt Button auf Normalzustand zurück
    /// </summary>
    public void ResetButton()
    {
        isLocked = false;
        SetColor(normalColor);
        transform.localScale = originalScale;
    }

    /// <summary>
    /// Zeigt Ergebnis-Farbe an (grün/rot) und sperrt den Button
    /// </summary>
    public void ShowResult(bool isCorrectAnswer)
    {
        isLocked = true;
        SetColor(isCorrectAnswer ? correctColor : wrongColor);
    }

    // ──────────────────────────── Trigger-Logik ────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (isLocked) return;
        if (Time.time - lastPressTime < cooldownTime) return;

        // Nur auf Hand-Collider reagieren (OVR Hand Tracking erzeugt Collider mit "Hand" im Namen)
        // Akzeptiere auch alles mit Rigidbody oder spezifische Tags
        if (!IsHandCollider(other)) return;

        lastPressTime = Time.time;

        // Visuelles Feedback: kurze Skalierung
        StartCoroutine(PressAnimation());

        // Haptisches Feedback (Controller-Vibration falls verfügbar)
        TryHapticFeedback();

        // Debug.Log($"[QuizButton] Button {answerIndex} gedrückt: '{answerText}'");
        OnPressed?.Invoke(answerIndex);
    }

    void OnTriggerStay(Collider other)
    {
        if (isLocked) return;
        if (!IsHandCollider(other)) return;

        // Hover-Effekt
        SetColor(hoverColor);
    }

    void OnTriggerExit(Collider other)
    {
        if (isLocked) return;
        if (!IsHandCollider(other)) return;

        SetColor(normalColor);
    }

    // ──────────────────────────── Helfer ────────────────────────────

    private bool IsHandCollider(Collider other)
    {
        // Oculus Hand Tracking Collider enthalten oft "Hand" oder "finger"/"index" im Namen
        string name = other.gameObject.name.ToLower();
        if (name.Contains("hand") || name.Contains("finger") || name.Contains("index") ||
            name.Contains("thumb") || name.Contains("tip") || name.Contains("poke"))
            return true;

        // Auch Controller-Collider akzeptieren
        if (name.Contains("controller") || name.Contains("touch"))
            return true;

        // Fallback: Wenn Collider zum "Hands" oder "Controllers" Layer gehört
        string layerName = LayerMask.LayerToName(other.gameObject.layer).ToLower();
        if (layerName.Contains("hand") || layerName.Contains("interact"))
            return true;

        return false;
    }

    private void SetColor(Color color)
    {
        if (buttonMaterial != null)
        {
            buttonMaterial.color = color;
            buttonMaterial.SetColor("_EmissionColor", color * 0.3f);
        }
    }

    private void TryHapticFeedback()
    {
        try
        {
            // OVRInput Haptic Pulse - kurzer Vibrationsimpuls
            OVRInput.SetControllerVibration(0.5f, 0.3f, OVRInput.Controller.RTouch);
            OVRInput.SetControllerVibration(0.5f, 0.3f, OVRInput.Controller.LTouch);
        }
        catch
        {
            // Kein Controller verbunden - kein Problem bei Hand Tracking
        }
    }

    private System.Collections.IEnumerator PressAnimation()
    {
        // Eindrücken
        Vector3 pressedScale = originalScale * 0.85f;
        float duration = 0.1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, pressedScale, elapsed / duration);
            yield return null;
        }

        // Zurückfedern
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(pressedScale, originalScale, elapsed / duration);
            yield return null;
        }

        transform.localScale = originalScale;
    }

    void OnDestroy()
    {
        if (buttonMaterial != null)
            Destroy(buttonMaterial);
    }
}
