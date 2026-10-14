using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Narration : réincarnation en Noyau de Donjon, arrivée des expéditions, premiers morts, etc.
/// Chaque événement ne se déclenche qu'une fois. Les dialogues mettent le jeu en pause.
/// Les textes sont ici pour être faciles à réécrire.
/// </summary>
public class StoryManager : MonoBehaviour
{
    public static StoryManager Instance { get; private set; }

    [Tooltip("Jouer l'introduction au lancement")]
    public bool playIntro = true;

    public const string NarratorName = "";
    public const string SystemName = "Astuce";
    public const string SelfName = "Vous";

    public struct Line
    {
        public string speaker;
        public string text;
        public Line(string speaker, string text) { this.speaker = speaker; this.text = text; }
    }

    private readonly HashSet<string> flags = new HashSet<string>();
    private readonly Queue<Line> queue = new Queue<Line>();
    private bool showing;

    public bool IsShowing => showing;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Unsubscribe();
    }

    private void Start()
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnNewDay += OnNewDay;
            gm.OnNightChanged += OnNightChanged;
            gm.OnGameOver += OnGameOver;
        }
        if (ExpeditionManager.Instance != null)
        {
            ExpeditionManager.Instance.OnExpeditionArrived += OnExpeditionArrived;
            ExpeditionManager.Instance.OnExpeditionEnded += OnExpeditionEnded;
        }
        if (ReputationSystem.Instance != null)
        {
            ReputationSystem.Instance.OnDiscovered += OnDiscovered;
            ReputationSystem.Instance.OnTierChanged += OnTierChanged;
        }
        if (PlayerLevel.Instance != null) PlayerLevel.Instance.OnLevelUp += OnLevelUp;

        // L'écran d'accueil (ou le chargement d'une partie) décide quand l'aventure commence
        if (playIntro && !MainMenu.HoldIntro) StartIntro();
    }

    /// <summary>Nouvelle partie : cinématique de naissance de l'orbe puis premiers dialogues.</summary>
    public void StartIntro()
    {
        bool cine = CinematicDirector.Instance != null && CinematicDirector.Instance.playIntroCinematic && GameSettings.Cinematics;
        // le réveil du Noyau est joué DANS la cinématique ; il ne reste ensuite que l'astuce de jeu
        if (cine) CinematicDirector.Instance.PlayIntro(() => PlayOnce("intro", IntroTip));
        else PlayIntro();
    }

    public IEnumerable<string> Flags => flags;

    public void RestoreFlags(IEnumerable<string> saved)
    {
        flags.Clear();
        foreach (var f in saved) flags.Add(f);
    }

    public void SetFlag(string key) => flags.Add(key);

    private void Unsubscribe()
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnNewDay -= OnNewDay;
            gm.OnNightChanged -= OnNightChanged;
            gm.OnGameOver -= OnGameOver;
        }
        if (ExpeditionManager.Instance != null)
        {
            ExpeditionManager.Instance.OnExpeditionArrived -= OnExpeditionArrived;
            ExpeditionManager.Instance.OnExpeditionEnded -= OnExpeditionEnded;
        }
        if (ReputationSystem.Instance != null)
        {
            ReputationSystem.Instance.OnDiscovered -= OnDiscovered;
            ReputationSystem.Instance.OnTierChanged -= OnTierChanged;
        }
        if (PlayerLevel.Instance != null) PlayerLevel.Instance.OnLevelUp -= OnLevelUp;
    }

    // ─────────────────────────────────────────────
    // DIALOGUES
    // ─────────────────────────────────────────────

    /// <summary>Affiche une suite de répliques (une seule fois par clé).</summary>
    public bool PlayOnce(string key, params Line[] lines)
    {
        if (!flags.Add(key)) return false;
        Play(lines);
        return true;
    }

    public void Play(params Line[] lines)
    {
        foreach (var l in lines) queue.Enqueue(l);
        if (!showing) ShowNext();
    }

    /// <summary>Appelé par le HUD quand le joueur passe à la réplique suivante.</summary>
    public void Advance()
    {
        if (!showing) return;
        ShowNext();
    }

    private void ShowNext()
    {
        if (queue.Count == 0)
        {
            if (showing)
            {
                showing = false;
                GameHUD.Instance?.HideDialogue();
                GameManager.Instance?.PopPause();
            }
            return;
        }
        if (!showing)
        {
            showing = true;
            GameManager.Instance?.PushPause();
        }
        Line line = queue.Dequeue();
        GameHUD.Instance?.ShowDialogue(line.speaker, line.text, queue.Count == 0);
    }

    public bool HasFlag(string key) => flags.Contains(key);

    private static Line N(string t) => new Line(NarratorName, t);
    private static Line S(string t) => new Line(SystemName, t);
    private static Line V(string t) => new Line(SelfName, t);
    private static Line P(string who, string t) => new Line(who, t);

    // ─────────────────────────────────────────────
    // SCÉNARIO
    // La cinématique décrit l'île ; ici, c'est le Noyau qui parle (monologue intérieur).
    // Pas de « système » dans ce monde : les explications de jeu sont de simples astuces.
    // ─────────────────────────────────────────────

    private void PlayIntro()
    {
        PlayOnce("intro",
            V("Je me suis couché dans mon lit... et je me suis réveillé en cristal, à la dérive au large d'une île. Mort dans mon sommeil, sans doute."),
            V("Statut ? Menu ?... Rien. Pas de système. Mais si je me concentre, je sens ma solidité, et cette énergie que je respire : le mana."),
            V("Une mouette m'a porté jusqu'à cette fissure, dans le flanc du volcan. J'ai versé mon mana dans la roche... et la grotte est devenue mon corps."),
            V("Chaque chose que je façonne me vide. Mais plus je grandirai, plus je pourrai respirer."),
            IntroTip,
            V("Si je dois être un donjon... autant être un bon donjon."));
    }

    private static readonly Line IntroTip = S("Ouvrez le panneau de droite. CREUSER agrandit votre antre ; chaque action coûte du mana, qui revient seul — plus vite la nuit.");

    private void OnNewDay(int day)
    {
        if (day == 1) return;
        GameHUD.Toast($"Jour {day}", GameHUD.InfoColor);
        if (day == 2)
            PlayOnce("day2", V("Le jour se lève — je le devine à la lumière qui filtre par la fissure. Une nuit entière, et personne n'est venu. Pour l'instant."));
    }

    private void OnNightChanged(bool night)
    {
        if (!night) return;
        GameHUD.Toast("La nuit tombe : le mana afflue", GameHUD.ManaColor);
        PlayOnce("first_night", V("La nuit tombe. Dans le noir, la montagne entière semble tendre l'oreille... et moi avec elle."));
    }

    private void OnExpeditionArrived(AdventurerTier tier, int count)
    {
        var exp = ExpeditionManager.Instance;
        bool cineOk = GameSettings.Cinematics && CinematicDirector.Instance != null;

        // Équipe d'évaluation de la Guilde : cinématique puis briefing
        if (exp != null && exp.IsAssessment)
        {
            GameHUD.Toast($"L'équipe d'évaluation de la Guilde accoste ({count} aventuriers de rang {GuildSystem.RankName(exp.CurrentRank)})", GameHUD.DangerColor);
            var g = GuildSystem.Instance;
            if (cineOk && !HasFlag("guild_arrival_cine") && g != null)
            {
                flags.Add("guild_arrival_cine");
                CinematicDirector.Instance.PlayGuildArrival(g.reportedByWitness, g.reportSummary, exp.CurrentRank, AssessmentBriefing);
            }
            else AssessmentBriefing();
            return;
        }

        GameHUD.Toast(exp != null && exp.CurrentRank >= 0
            ? $"Un bateau accoste : {count} aventuriers de rang {GuildSystem.RankName(exp.CurrentRank)}"
            : $"Un bateau accoste : {count} × {tier.displayName}", GameHUD.DangerColor);

        // Premier bateau : la cinématique d'abord, les répliques ensuite
        if (tier.unaware && !HasFlag("first_boat_cine") && cineOk && !CinematicDirector.Instance.IsPlaying)
        {
            flags.Add("first_boat_cine");
            var boat = exp != null ? exp.CurrentBoat : null;
            CinematicDirector.Instance.PlayFirstBoat(boat != null ? boat.transform : null, () => OnExpeditionArrived(tier, count));
            return;
        }
        if (tier.unaware)
        {
            PlayOnce("first_fishermen",
                P("Pêcheur", "Par tous les saints... On est vivants. Quelqu'un connaît cette île ?"),
                P("Pêcheur", "Là-haut, une grotte dans la montagne. On pourra s'y abriter le temps que la mer se calme."),
                V("Des humains. Les premiers que je vois depuis... depuis que je suis mort."),
                V("Je sens leur mana d'ici. Chaud. Tentant. S'ils entrent, j'en boirai un peu... ou tout."),
                S("Un intrus dans le donjon est lentement drainé de son mana ; tué, il libère toute son essence. Un survivant, lui, rentrera raconter ce qu'il a vu."));
            return;
        }
        string rank = exp != null && exp.CurrentRank >= 0 ? GuildSystem.RankName(exp.CurrentRank) : "?";
        switch (ReputationSystem.Instance != null ? ReputationSystem.Instance.CurrentTier : 0)
        {
            case 1:
                PlayOnce("tier_villagers",
                    P("Villageois", "Trois jours qu'ils ne sont pas rentrés. S'il y a une bête là-dedans, on la trouvera."),
                    V("Des fourches, des torches et un chasseur avec un arc. Ils cherchent leurs disparus."));
                break;
            case 2:
                PlayOnce("tier_adventurers",
                    P("Aventurière", $"Un donjon de rang {rank}, presque vierge, et la Guilde paie la prime d'exploration ? C'est maintenant ou jamais."),
                    P("Aventurière", "On garde la formation. Bouclier devant, Lise derrière, et personne ne touche aux coffres avant que je le dise."),
                    V("Des professionnels. Un bouclier devant, une soigneuse derrière, un archer et un mage pour me grignoter à distance."),
                    S("Visez les soigneuses et les tireurs : ils sont fragiles, mais un groupe bien protégé peut tout emporter."));
                break;
            case 3:
                PlayOnce("tier_knights",
                    P("Chevalier", $"Rang {rank}. La Guilde a réclamé des lames d'élite, alors nous voici."),
                    P("Chevalier", "Prêtresse, bénissez nos armes. Archimage, brûlez tout ce qui bouge. Ce donjon ne verra pas la fin de la semaine."),
                    V("Plus de curieux ni de chasseurs de primes. Ceux-là viennent pour me tuer, et ils savent comment faire."));
                break;
            case 4:
                PlayOnce("tier_hero",
                    N("Une silhouette nimbée de lumière pose le pied sur la plage. Les colons s'agenouillent sur son passage."),
                    P("Héros", "Rang SSS, qu'ils disent. Un donjon qui mérite qu'on se déplace, enfin."),
                    P("Héros", "Je sens ta présence, Noyau. Montre-toi, et finissons-en."),
                    V("Le héros. C'était donc ça, la fin de l'histoire ?... Pas si c'est moi qui l'écris."));
                break;
        }
    }

    /// <summary>Briefing de l'équipe d'évaluation (après sa cinématique).</summary>
    private void AssessmentBriefing()
    {
        PlayOnce("guild_assessment",
            P("Cheffe d'équipe", "Procédure standard : on cartographie, on compte les monstres, on repère les pièges. Personne ne joue les héros."),
            P("Mage", "Et s'il y a vraiment un noyau, là-dedans ?"),
            P("Cheffe d'équipe", "On le note dans le rapport, et on n'y touche pas. On n'est pas payés pour mourir."),
            V("Des inspecteurs. On vient noter mon donjon comme on note une auberge."),
            S("L'équipe mesure, elle ne cherche pas à vous détruire. Si elle rentre, le rang dépendra de ce qu'elle a vu ; si des membres ne rentrent pas, la Guilde vous jugera bien plus dangereux."));
    }

    private void OnExpeditionEnded(int killed, int fled)
    {
        if (fled == 0 && killed > 0)
            GameHUD.Toast("Aucun survivant. Personne ne reviendra raconter...", GameHUD.ManaColor);
        else if (fled > 0 && killed == 0)
            GameHUD.Toast($"{fled} intrus ont quitté l'île", GameHUD.InfoColor);
        else
            GameHUD.Toast($"Expédition terminée : {killed} mort(s), {fled} survivant(s)", GameHUD.InfoColor);
    }

    private void OnDiscovered()
    {
        // Le signalement à la Guilde est raconté par OnGuildReported
    }

    // ─────────────────────────────────────────────
    // GUILDE DES AVENTURIERS
    // ─────────────────────────────────────────────

    public void OnGuildReported(bool fromWitness, string summary, int rank, int day)
    {
        GameHUD.Toast("Votre donjon a été signalé à la Guilde des Aventuriers", GameHUD.WarningColor);
        PlayOnce("guild_reported",
            fromWitness
                ? V("Ils rament comme des damnés vers le large. Dès demain, tout le port saura ce qu'ils ont vu ici.")
                : V("Personne n'est revenu. Et c'est justement ce qui va attirer les autres."),
            S("Dans ce monde, la Guilde des Aventuriers classe tout ce qui est dangereux : les aventuriers comme les donjons, du rang F (novices) au rang SSS (les héros, l'élite des nations, presque des demi-dieux)."),
            S("Tôt ou tard, une équipe d'évaluation viendra mesurer votre donjon. Monstres, pièges, blessés, morts, trésors : ce qu'elle rapportera décidera du rang des aventuriers qu'on vous enverra ensuite."),
            V("Un examen, donc. Autant le réussir... dans un sens ou dans l'autre."));
    }

    public void OnGuildVerdict(int rank, string outcome, int killed, int fled)
    {
        GameHUD.Toast($"La Guilde classe votre donjon : rang {GuildSystem.RankName(rank)}", GameHUD.GoldColor);
        void Talk()
        {
            string r = GuildSystem.RankName(rank);
            var first = fled == 0 && killed > 0
                ? V($"Personne n'est rentré, et c'est déjà une réponse. Rang {r}... Ils ont peur de moi.")
                : killed > 0
                    ? V($"Rang {r}. Ils m'ont classé plus haut parce que j'ai tué des leurs. La peur, voilà ma meilleure publicité.")
                    : V($"Rang {r}. Me voilà officiellement fiché, comme une auberge ou un marécage à éviter.");
            PlayOnce("guild_verdict", first,
                S("Désormais, les expéditions viennent au rang de votre donjon. La CONVOITISE (trésors, butin, décor) attire plus de monde ; l'HOSTILITÉ (morts, blessés) rend les aventuriers plus acharnés. Les deux jauges sont affichées en haut à gauche."),
                S("Chaque expédition qui rentre fait évoluer votre classement. Un donjon qui tue monte vite en rang, et on lui envoie alors des chevaliers, puis des héros."));
        }
        if (GameSettings.Cinematics && CinematicDirector.Instance != null)
            CinematicDirector.Instance.PlayGuildVerdict(rank, outcome, Talk);
        else Talk();
    }

    public void OnFirstPrisoner(AdventurerAI a)
    {
        PlayOnce("first_prisoner",
            V("Assommé, pas mort. Derrière les barreaux, il tremble... et son mana suinte doucement vers moi."),
            S("Un prisonnier nourrit le cristal chaque matin. Libérez-le pour apaiser la colonie, vendez-le contre rançon, ou absorbez-le d'un coup."));
    }

    public void OnGuardianNamed(MonsterMind m)
    {
        PlayOnce("first_guardian",
            V($"Je te nomme {m.GivenName}. Cet étage est à toi : que personne ne le traverse vivant."),
            P(m.GivenName, "Il en sera ainsi, Maître."),
            S("Un gardien par étage : plus fort, plus endurant, il se régénère vite. S'il tombe, vous pourrez en nommer un autre."));
    }

    public void OnNemesisReturns(NemesisRecord r)
    {
        GameHUD.Toast($"{r.name} « {r.epithet} » est de retour (rang {GuildSystem.RankName(r.rank)})", GameHUD.DangerColor);
        if (r.encounters <= 1)
            Play(P(r.name, "Tu te souviens de moi, Noyau ? Moi, je n'ai rien oublié. Ni tes couloirs, ni tes pièges."),
                 V($"Lui... Il est revenu. On l'appelle « {r.epithet} », maintenant. Et il a pris du galon."));
        else
        {
            string[] lines =
            {
                "Encore moi. Cette fois, je sais exactement où tu caches tes pièges.",
                "Chaque fois que je ressors, je deviens plus fort. Et toi ?",
                "Les autres ont peur de toi. Moi, je te connais.",
            };
            Play(P(r.name, lines[UnityEngine.Random.Range(0, lines.Length)]));
        }
    }

    public void OnGuildRankChanged(int oldRank, int newRank)
    {
        if (newRank <= oldRank) return;
        string r = GuildSystem.RankName(newRank);
        if (newRank >= 5 && newRank <= 6)
            PlayOnce("rank_elite", V($"Rang {r}. On ne m'envoie plus des aventuriers, mais leur élite. Je dois être à la hauteur."));
        else if (newRank >= 7)
            PlayOnce("rank_legend", V($"Rang {r}... Au-delà, il n'y a que les héros. Et je sens qu'ils ont commencé à entendre parler de moi."));
        else
            PlayOnce("rank_up", V($"La Guilde me reclasse au rang {r}. Les prochains seront plus forts... et plus nombreux à vouloir ma tête."));
    }

    private void OnTierChanged(int tier)
    {
        string[] names = { "Pêcheurs", "Villageois", "Aventuriers", "Chevaliers", "Héros" };
        if (tier >= 0 && tier < names.Length)
            GameHUD.Toast($"La menace grandit : on parle de vous jusque chez les {names[tier].ToLowerInvariant()}", GameHUD.DangerColor);
    }

    private void OnLevelUp(int level)
    {
        GameHUD.Toast($"Niveau {level} : plus de monstres, de pièges et de mana", GameHUD.GoldColor);
        Sfx.Play("levelup");
        var core = FindFirstObjectByType<DungeonCore>();
        if (core != null) Fx.Play("buff", core.transform.position, 2f, 14f, 16, null, false, null, new Color(1f, 0.9f, 0.5f));
        PlayOnce("first_levelup", V("Je me sens... plus vaste. Comme si la montagne entière respirait avec moi."));
    }

    private void OnGameOver()
    {
        // L'écran de fin est géré par le HUD
    }

    // ─────────────────────────────────────────────
    // ÉVÉNEMENTS DE JEU
    // ─────────────────────────────────────────────

    public void OnIntruderEntered(AdventurerAI adv)
    {
        GameHUD.Toast($"{adv.DisplayName} entre dans votre donjon", GameHUD.WarningColor);
        PlayOnce("first_intruder", V("Quelqu'un est entré. Je sens ses pas résonner dans mes couloirs... C'est la sensation la plus étrange de ma nouvelle vie."));
    }

    public void OnCoreSpotted(AdventurerAI adv)
    {
        GameHUD.Toast($"{adv.DisplayName} a vu votre cristal et s'enfuit !", GameHUD.DangerColor);
        PlayOnce("core_spotted", V("Il m'a vu ! S'il quitte l'île, il racontera tout."));
    }

    public void OnAdventurerKilled(AdventurerAI adv)
    {
        PlayOnce("first_kill",
            N("Une vague de chaleur vous traverse : l'essence d'un être humain se dissout dans le cristal."),
            V("C'est... bon. Trop bon. Une part de moi s'en réjouit. L'autre se souvient d'avoir été humaine."));
    }

    public void OnAdventurerFled(AdventurerAI adv)
    {
        GameHUD.Toast($"{adv.DisplayName} a quitté l'île vivant", GameHUD.InfoColor);
        PlayOnce("first_fled", V("Il s'en va vivant. Il racontera ce qu'il a vu... et d'autres voudront voir par eux-mêmes."));
    }

    public void OnMonsterSummoned()
    {
        PlayOnce("first_summon",
            V("Une créature née de mon mana. Elle m'obéit sans un mot... et si je me concentre, je vois à travers ses yeux."),
            S("Sélectionnez un monstre puis « Posséder » pour le contrôler directement."));
    }

    public void OnLayerUnlocked(int layer, LayerTheme theme)
    {
        PlayOnce("layer_" + layer,
            N($"La roche gémit. Sous vos fondations s'ouvre un nouvel espace : {theme.displayName}."),
            S($"{theme.description}"),
            S("Creusez un ESCALIER depuis l'étage du dessus (panneau Étages) pour relier ce niveau. Vous pourrez ensuite y abriter votre cœur."));
    }

    public void OnCoreMoved(bool free)
    {
        if (free)
            PlayOnce("first_core_move",
                N("Le cristal se fend de lumière, se dissout... puis se reforme plus loin dans la roche."),
                V("Tiens... ça ne m'a presque rien coûté. La roche est encore jeune, elle se laisse faire."),
                S("Le premier déplacement du cœur est gratuit. Les suivants coûteront 90 % de votre réserve maximale de mana : choisissez bien votre refuge."));
        else
            PlayOnce("first_core_move",
                N("Le cristal se fend de lumière, se dissout... puis se reforme plus loin dans la roche."),
                V("Épuisant. Presque tout mon mana y est passé. Mais ici, ils auront plus de mal à m'atteindre."));
    }

    public void OnSettlementStage(int stage, string name)
    {
        bool cine = stage != 3 && GameSettings.Cinematics && CinematicDirector.Instance != null && !HasFlag("colony_cine_" + stage);
        if (cine)
        {
            flags.Add("colony_cine_" + stage);
            CinematicDirector.Instance.PlayColonyStage(stage, () => ColonyDialogue(stage));
        }
        else ColonyDialogue(stage);
    }

    private void ColonyDialogue(int stage)
    {
        switch (stage)
        {
            case 1:
                PlayOnce("colony_1",
                    V("Ils ne repartent pas. Ils s'installent... pour moi. Je suis devenu une destination."),
                    S("La COLONIE grandit tant que le donjon reste rentable : les survivants qui rentrent riches la font prospérer, les morts la terrifient. Elle rend les expéditions plus fréquentes et mieux équipées."));
                break;
            case 2:
                PlayOnce("colony_2", V("Un vrai port. Les navires viendront plus gros, plus souvent... et plus chargés d'aventuriers."));
                break;
            case 3:
                PlayOnce("colony_3", N("Des maisons en dur remplacent peu à peu les tentes : le camp devient un hameau."),
                    V("Ils ne comptent vraiment pas repartir."));
                break;
            case 4:
                PlayOnce("colony_4", V("Tout un village qui vit de moi... et qui rêve de me voir tomber."));
                break;
            case 5:
                PlayOnce("colony_5", V("Ils forgent ici même les héros qui viendront m'abattre. Soit. Qu'ils viennent."));
                break;
        }
    }

    public void OnUpkeepFailed()
    {
        PlayOnce("upkeep_fail", V("Je n'ai pas assez de mana pour nourrir tout ce petit monde. Mes monstres ont faim, mes pièges se sont figés."),
            S("L'entretien est prélevé chaque matin. Réduisez vos troupes ou gardez une réserve de mana."));
    }

    public void OnDecorPlaced()
    {
        PlayOnce("first_decor", V("Un peu de confort... Les aventuriers adorent les donjons bien tenus. Plus mon domaine impressionne, plus on en parle."));
    }

    public void OnCellDug()
    {
        PlayOnce("first_dig", V("La roche s'efface sous ma volonté. Ce donjon n'est pas ma maison : c'est mon corps."));
    }

    public void OnFirstPossession()
    {
        PlayOnce("first_possession", N("Votre conscience glisse hors du cristal et se loge dans le corps de la créature."),
            S("Déplacez-vous (ZQSD / joystick) et attaquez (Espace / bouton). Un monstre possédé frappe plus fort."));
    }
}
