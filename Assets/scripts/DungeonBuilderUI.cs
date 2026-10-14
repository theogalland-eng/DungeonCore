using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro

/// <summary>
/// Contrôleur UI pour le DungeonBuilder
/// Affiche : Mana, Mode, Barre de mining
/// </summary>
public class DungeonBuilderUI : MonoBehaviour
{
    [Header("UI Elements - TextMeshPro (recommandé)")]
    [Tooltip("Texte affichant la mana (TextMeshPro)")]
    public TextMeshProUGUI manaText;
    
    [Tooltip("Texte affichant le mode actuel (TextMeshPro)")]
    public TextMeshProUGUI modeText;
    
    [Tooltip("Texte d'aide/raccourcis (TextMeshPro)")]
    public TextMeshProUGUI helpText;

    [Header("OU UI Elements - Text standard")]
    [Tooltip("Si tu n'as pas TextMeshPro, utilise Text standard")]
    public Text manaTextStandard;
    public Text modeTextStandard;
    public Text helpTextStandard;

    [Header("Mining Progress")]
    [Tooltip("Barre de progression du mining (Image ou Slider)")]
    public Image miningProgressBar;
    
    [Tooltip("GameObject parent de la barre (à activer/désactiver)")]
    public GameObject miningProgressPanel;

    [Header("Mode Buttons (optionnel)")]
    [Tooltip("Bouton pour mode Mur")]
    public Button wallButton;
    
    [Tooltip("Bouton pour mode Piège")]
    public Button trapButton;
    
    [Tooltip("Bouton pour mode Porte")]
    public Button doorButton;

    private DungeonBuilder builder;

    private void Start()
    {
        // Trouver le DungeonBuilder
        builder = FindFirstObjectByType<DungeonBuilder>();
        
        if (builder == null)
        {
            Debug.LogError("[DungeonBuilderUI] DungeonBuilder non trouvé !");
            return;
        }

        // Assigner les boutons si présents
        if (wallButton != null)
            wallButton.onClick.AddListener(() => builder.SetMode(DungeonBuilder.BuildMode.PlacingWall));
        
        if (trapButton != null)
            trapButton.onClick.AddListener(() => builder.SetMode(DungeonBuilder.BuildMode.PlacingTrap));
        
        if (doorButton != null)
            doorButton.onClick.AddListener(() => builder.SetMode(DungeonBuilder.BuildMode.Summoning));

        // Masquer la barre de mining au démarrage
        if (miningProgressPanel != null)
            miningProgressPanel.SetActive(false);

        // Afficher l'aide
        UpdateHelpText();
    }

    /// <summary>
    /// Appelé par DungeonBuilder pour mettre à jour l'UI
    /// </summary>
    public void UpdateDisplay(int mana, DungeonBuilder.BuildMode mode, bool isMining, float miningProgress, float miningTime)
    {
        // Mana
        UpdateManaText(mana);

        // Mode
        UpdateModeText(mode);

        // Barre de mining
        UpdateMiningBar(isMining, miningProgress, miningTime);
    }

    private void UpdateManaText(int mana)
    {
        string text = $"Mana: {mana}";
        
        if (manaText != null)
            manaText.text = text;
        else if (manaTextStandard != null)
            manaTextStandard.text = text;
    }

    private void UpdateModeText(DungeonBuilder.BuildMode mode)
    {
        string text = $"Mode: {GetModeName(mode)}";
        
        if (modeText != null)
            modeText.text = text;
        else if (modeTextStandard != null)
            modeTextStandard.text = text;
    }

    private void UpdateMiningBar(bool isMining, float progress, float maxTime)
    {
        if (miningProgressPanel != null)
            miningProgressPanel.SetActive(isMining);

        if (miningProgressBar != null && isMining)
        {
            float fillAmount = progress / maxTime;
            miningProgressBar.fillAmount = fillAmount;
        }
    }

    private void UpdateHelpText()
    {
        string help = "[1] Creuser [2] Remblayer [3] Piège [4] Gobelin [5] Décor [6] Gomme\n[ESC] Sélection";
        
        if (helpText != null)
            helpText.text = help;
        else if (helpTextStandard != null)
            helpTextStandard.text = help;
    }

    private string GetModeName(DungeonBuilder.BuildMode mode) => DungeonBuilder.GetModeLabel(mode);
}