using System;

/// <summary>
/// Kategorien für Quiz-Fragen
/// </summary>
public enum QuizCategory
{
    Keilstrich,     // Keilstrichformel-Fragen (Wedge/Dash)
    Chirality       // R/S-Konfiguration, Stereozentren
}

/// <summary>
/// Eine einzelne Quiz-Frage mit Multiple-Choice Antworten
/// </summary>
[Serializable]
public class QuizQuestion
{
    public string questionText;         // "Welche R/S-Konfiguration hat das markierte Zentrum?"
    public string moleculeName;         // Molekül das in VR geladen/angezeigt wird
    public string imageName;            // 2D-Bild aus Resources (z.B. "KeilstrichBild") — leer = kein Bild
    public string[] answers;            // z.B. ["R", "S", "achiral", "meso"]
    public int correctAnswerIndex;      // Index der richtigen Antwort (0-basiert)
    public QuizCategory category;       // Keilstrich oder Chiralität
    public string explanation;          // Erklärung nach Beantwortung
}

/// <summary>
/// Wrapper-Klasse für JsonUtility-Deserialisierung einer Fragen-Liste
/// </summary>
[Serializable]
public class QuizQuestionList
{
    public QuizQuestion[] questions;
}

/// <summary>
/// Aktueller Zustand des Quiz (wird an Web-UI gesendet)
/// </summary>
[Serializable]
public class QuizState
{
    public bool isActive;
    public int currentQuestionIndex;
    public int score;
    public int totalQuestions;

    public bool IsFinished => isActive && currentQuestionIndex >= totalQuestions;
}
