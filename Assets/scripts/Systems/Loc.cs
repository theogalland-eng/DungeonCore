using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

/// <summary>
/// TRADUCTION (français → anglais) sans toucher aux textes du code : chaque TextMeshPro reçoit un pré-processeur
/// qui traduit son texte juste avant l'affichage. Le texte est découpé par balises de texte riche et par lignes ;
/// chaque segment est cherché tel quel dans Resources/Localization/en.txt (« français TAB anglais »), sinon comparé
/// aux modèles à trous ({0}, {1}... : noms, nombres), dont le contenu est traduit à son tour.
/// Tools~/extract_strings.py liste les segments du code à traduire.
/// </summary>
public static class Loc
{
    public static bool English { get; private set; }

    private static readonly Dictionary<string, string> exact = new Dictionary<string, string>();
    private static readonly List<(Regex re, string en)> patterns = new List<(Regex, string)>();
    private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
    private static readonly Regex Tag = new Regex("<[^<>]*>", RegexOptions.Compiled);
    private static readonly Regex Hole = new Regex(@"\{(\d+)\}", RegexOptions.Compiled);
    private static bool loaded, hooked;
    private static readonly Preprocessor pre = new Preprocessor();
    private static readonly List<TMP_Text> pending = new List<TMP_Text>();

    private class Preprocessor : ITextPreprocessor
    {
        public string PreprocessText(string text) => T(text);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot() => Apply(GameSettings.Language == "en");

    /// <summary>Change de langue et retraduit tout ce qui est affiché.</summary>
    public static void Apply(bool english)
    {
        English = english;
        cache.Clear();
        if (english) { Load(); Hook(); }
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            t.textPreprocessor = english ? pre : null;
            t.ForceMeshUpdate(true, true);
        }
    }

    private static void Hook()
    {
        if (hooked) return;
        hooked = true;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        var go = new GameObject("Loc");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<Runner>();
    }

    private static void OnTextChanged(Object o)
    {
        if (!English || !(o is TMP_Text t) || t.textPreprocessor != null) return;
        t.textPreprocessor = pre;
        pending.Add(t);   // retraité à la fin de l'image (pas pendant sa propre génération)
    }

    private class Runner : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (pending.Count == 0) return;
            var list = new List<TMP_Text>(pending);
            pending.Clear();
            foreach (var t in list) if (t != null) t.ForceMeshUpdate(true, true);
        }
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        var asset = Resources.Load<TextAsset>("Localization/en");
        if (asset == null) return;
        var pats = new List<(string fr, string en)>();
        foreach (var raw in asset.text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#') continue;
            int tab = line.IndexOf('\t');
            if (tab <= 0) continue;
            string fr = line.Substring(0, tab).Replace("\\n", "\n"), en = line.Substring(tab + 1).Replace("\\n", "\n");
            if (Hole.IsMatch(fr)) pats.Add((fr, en));
            else exact[fr] = en;
        }
        // les modèles les plus longs (les plus précis) d'abord
        pats.Sort((a, b) => b.fr.Length.CompareTo(a.fr.Length));
        foreach (var (fr, en) in pats)
        {
            string rx = "^" + Hole.Replace(Regex.Escape(fr).Replace(@"\{", "{"), m => $"(?<h{m.Groups[1].Value}>.*?)") + "$";
            try { patterns.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.Singleline), en)); }
            catch (System.ArgumentException) { /* modèle invalide (même trou deux fois) : ignoré */ }
        }
    }

    /// <summary>Traduit un texte complet (balises conservées). Sans effet en français.</summary>
    public static string T(string text)
    {
        if (!English || string.IsNullOrEmpty(text)) return text;
        if (cache.TryGetValue(text, out var hit)) return hit;
        var sb = new StringBuilder(text.Length + 16);
        int last = 0;
        foreach (Match m in Tag.Matches(text))
        {
            Chunk(text.Substring(last, m.Index - last), sb);
            sb.Append(m.Value);
            last = m.Index + m.Length;
        }
        Chunk(text.Substring(last), sb);
        string res = sb.ToString();
        if (cache.Count > 6000) cache.Clear();
        cache[text] = res;
        return res;
    }

    private static void Chunk(string s, StringBuilder sb)
    {
        var lines = s.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(Segment(lines[i], 0));
        }
    }

    private static string Segment(string s, int depth)
    {
        if (s.Length == 0 || depth > 3) return s;
        int a = 0, b = s.Length;
        while (a < b && char.IsWhiteSpace(s[a])) a++;
        while (b > a && char.IsWhiteSpace(s[b - 1])) b--;
        if (a >= b) return s;
        string core = s.Substring(a, b - a);
        string tr = Translate(core, depth);
        return tr == core ? s : s.Substring(0, a) + tr + s.Substring(b);
    }

    private static string Translate(string core, int depth)
    {
        if (exact.TryGetValue(core, out var en)) return en;
        foreach (var (re, pen) in patterns)
        {
            var m = re.Match(core);
            if (!m.Success) continue;
            return Hole.Replace(pen, h =>
            {
                var g = m.Groups["h" + h.Groups[1].Value];
                return g.Success ? Segment(g.Value, depth + 1) : "";
            });
        }
        // morceaux assemblés par le code : « Aube  ·  Surface », « Monstres 3 | Pièges 1 »
        foreach (char sep in Separators)
        {
            if (core.IndexOf(sep) < 0) continue;
            var parts = core.Split(sep);
            bool any = false;
            for (int i = 0; i < parts.Length; i++)
            {
                string t = Segment(parts[i], depth + 1);
                if (t != parts[i]) { parts[i] = t; any = true; }
            }
            if (any) return string.Join(sep.ToString(), parts);
        }
        // court texte assemblé par virgules (« Isaure, pêcheuse »)
        if (core.Length < 48 && core.Contains(", "))
        {
            var parts = core.Split(new[] { ", " }, System.StringSplitOptions.None);
            bool any = false;
            for (int i = 0; i < parts.Length; i++)
            {
                string t = Segment(parts[i], depth + 1);
                if (t != parts[i]) { parts[i] = t; any = true; }
            }
            if (any) return string.Join(", ", parts);
        }
        // ponctuation finale ajoutée par le code (« Encore 4 habitant(s) sur place. »)
        int z = core.Length;
        while (z > 1 && ".!:; 0123456789".IndexOf(core[z - 1]) >= 0) z--;
        if (z < core.Length)
        {
            string inner = Translate(core.Substring(0, z), depth + 1);
            if (inner != core.Substring(0, z)) return inner + core.Substring(z);
        }
        // puce ou guillemet devant (« » Conseil », « • Ligne »)
        int a = 0;
        while (a < core.Length && Prefix.IndexOf(core[a]) >= 0) a++;
        if (a > 0 && a < core.Length)
        {
            string inner = Segment(core.Substring(a), depth + 1);
            if (inner != core.Substring(a)) return core.Substring(0, a) + inner;
        }
        return core;
    }

    private static readonly char[] Separators = { '·', '|', '→' };
    private const string Prefix = "»•-—–«( ";
}
