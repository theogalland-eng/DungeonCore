using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Panneau de construction repliable sur la droite de l'écran.
/// Onglets : Bâtir, Monstres, Pièges, Décor, Étages. Contenu tiré du DungeonCatalog.
/// </summary>
public class SidePanel : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.11f, 0.92f);
    private static readonly Color ButtonColor = new Color(0.17f, 0.15f, 0.26f, 0.96f);
    private static readonly Color ActiveColor = new Color(0.47f, 0.32f, 0.8f, 1f);
    private static readonly Color LockedColor = new Color(0.12f, 0.11f, 0.15f, 0.9f);
    private static readonly Color TextColor = new Color(0.94f, 0.93f, 0.98f);
    private static readonly Color SubText = new Color(0.7f, 0.68f, 0.8f);

    private const float Width = 450f;

    private RectTransform container;
    private RectTransform content;
    private ScrollRect scroll;
    private TextMeshProUGUI handleLabel;
    private readonly List<Image> tabImages = new List<Image>();
    private CatalogCategory currentTab = CatalogCategory.Build;
    private bool expanded = true;
    private string contentSignature;
    private float refreshTimer;
    private int themePickerLayer = -1;

    private class ItemButton
    {
        public CatalogItem item;
        public DungeonBuilder.BuildMode tool;
        public Image bg;
        public GameObject lockOverlay;
        public TextMeshProUGUI lockText;
    }
    private readonly List<ItemButton> itemButtons = new List<ItemButton>();

    public bool Expanded => expanded;

    public static SidePanel Create(RectTransform parent)
    {
        var go = new GameObject("SidePanel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 0.5f);
        rt.offsetMin = new Vector2(-Width - 16, 16);
        rt.offsetMax = new Vector2(-16, -182);
        var sp = go.AddComponent<SidePanel>();
        sp.Build(rt);
        return sp;
    }

    private void Build(RectTransform root)
    {
        container = root;
        var bg = root.gameObject.AddComponent<Image>();
        bg.sprite = SpriteUtil.RoundedUI;
        bg.type = Image.Type.Sliced;
        bg.color = PanelColor;

        // Poignée pour replier / déplier
        var handle = Btn(root.parent, "PanelHandle", "", () => SetExpanded(!expanded));
        var hrt = (RectTransform)handle.transform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(1, 0.5f);
        hrt.pivot = new Vector2(1, 0.5f);
        hrt.sizeDelta = new Vector2(56, 150);
        handleLabel = Txt(hrt, "", 34, TextColor, TextAlignmentOptions.Center);
        Stretch(handleLabel.rectTransform, 0, 0, 0, 0);

        // Onglets
        string[] names = { "Bâtir", "Monstres", "Pièges", "Décor", "Étages", "Trésor" };
        float tw = (Width - 20f - 5 * 6f) / 6f;
        for (int i = 0; i < names.Length; i++)
        {
            var cat = (CatalogCategory)i;
            var b = Btn(root, "Tab" + i, names[i], () => SetTab(cat), 15);
            var tl = b.GetComponentInChildren<TextMeshProUGUI>();
            tl.enableAutoSizing = true;
            tl.fontSizeMin = 11;
            tl.fontSizeMax = 15;
            tl.overflowMode = TextOverflowModes.Overflow;
            tl.rectTransform.offsetMin = new Vector2(1, 0);
            tl.rectTransform.offsetMax = new Vector2(-1, 0);
            var brt = (RectTransform)b.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0, 1);
            brt.anchoredPosition = new Vector2(10 + i * (tw + 6f), -10);
            brt.sizeDelta = new Vector2(tw, 54);
            tabImages.Add(b.GetComponent<Image>());
        }

        // Zone défilante (glisser au doigt / molette) : la liste peut dépasser la hauteur de l'écran
        var viewport = NewRect("Viewport", root);
        viewport.anchorMin = new Vector2(0, 0);
        viewport.anchorMax = new Vector2(1, 1);
        viewport.offsetMin = new Vector2(10, 10);
        viewport.offsetMax = new Vector2(-18, -74);
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0, 0, 0, 0);   // capte le glisser entre les boutons
        viewport.gameObject.AddComponent<RectMask2D>();

        content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, 100);

        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.08f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;
        scroll.scrollSensitivity = 45f;

        // Barre de défilement fine (masquée si tout tient à l'écran)
        var bar = NewRect("Scrollbar", root);
        bar.anchorMin = new Vector2(1, 0);
        bar.anchorMax = new Vector2(1, 1);
        bar.pivot = new Vector2(1, 0.5f);
        bar.offsetMin = new Vector2(-12, 14);
        bar.offsetMax = new Vector2(-5, -78);
        var barImg = bar.gameObject.AddComponent<Image>();
        barImg.sprite = SpriteUtil.RoundedUI;
        barImg.type = Image.Type.Sliced;
        barImg.color = new Color(1f, 1f, 1f, 0.06f);
        var area = NewRect("Area", bar);
        Stretch(area, 0, 0, 0, 0);
        var handleRt = NewRect("Handle", area);
        Stretch(handleRt, 0, 0, 0, 0);
        var hImg = handleRt.gameObject.AddComponent<Image>();
        hImg.sprite = SpriteUtil.RoundedUI;
        hImg.type = Image.Type.Sliced;
        hImg.color = new Color(0.62f, 0.5f, 0.95f, 0.75f);
        var sb = bar.gameObject.AddComponent<Scrollbar>();
        sb.handleRect = handleRt;
        sb.targetGraphic = hImg;
        sb.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = sb;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        SetExpanded(false);   // replié au lancement : la carte d'abord
        SetTab(CatalogCategory.Build);
    }

    public void SetExpanded(bool e)
    {
        expanded = e;
        container.gameObject.SetActive(e);
        var handle = (RectTransform)handleLabel.transform.parent;
        handle.anchoredPosition = new Vector2(e ? -Width - 20f : -16f, 0f);
        handleLabel.text = e ? ">" : "<";
    }

    public void SetVisible(bool v)
    {
        gameObject.SetActive(v && expanded);
        handleLabel.transform.parent.gameObject.SetActive(v);
    }

    private void SetTab(CatalogCategory c)
    {
        currentTab = c;
        themePickerLayer = -1;
        for (int i = 0; i < tabImages.Count; i++) tabImages[i].color = (int)c == i ? ActiveColor : ButtonColor;
        contentSignature = null;
        Rebuild();
        ScrollToTop();
    }

    private void ScrollToTop()
    {
        if (scroll == null) return;
        scroll.StopMovement();
        content.anchoredPosition = Vector2.zero;
    }

    /// <summary>Ajuste la hauteur défilable au contenu réellement construit.</summary>
    private void FitContent()
    {
        float bottom = 0f;
        for (int i = 0; i < content.childCount; i++)
        {
            var rt = (RectTransform)content.GetChild(i);
            if (!rt.gameObject.activeSelf) continue;
            bottom = Mathf.Max(bottom, -rt.anchoredPosition.y + rt.rect.height * rt.pivot.y);
        }
        content.sizeDelta = new Vector2(0, bottom + 12f);
    }

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = 0.25f;

        string sig = Signature();
        if (sig != contentSignature) Rebuild();
        RefreshButtons();
        UpdateAttackBanner();
    }

    // ── Attaque en cours : bandeau rouge, la construction est verrouillée ──
    private RectTransform attackBanner;

    private void UpdateAttackBanner()
    {
        bool attack = DungeonBuilder.UnderAttack;
        if (attackBanner == null)
        {
            if (!attack || container == null) return;
            attackBanner = UIKit.Rect("Attaque", container);
            attackBanner.anchorMin = new Vector2(0f, 0f); attackBanner.anchorMax = new Vector2(1f, 1f);
            attackBanner.offsetMin = new Vector2(8f, 8f); attackBanner.offsetMax = new Vector2(-8f, -70f);
            UIKit.Image(attackBanner, new Color(0.25f, 0.03f, 0.05f, 0.82f), true, true);   // bloque aussi les clics
            var t = UIKit.Label(attackBanner, "<b>LE DONJON EST ATTAQUÉ</b>\n\n<size=80%>Impossible de bâtir, poser ou invoquer tant qu'il reste des intrus.\nVos monstres, eux, peuvent toujours recevoir des ordres.</size>", 26, new Color(1f, 0.82f, 0.78f));
            UIKit.Stretch(t.rectTransform, 24, 24, 0, 0);
        }
        attackBanner.gameObject.SetActive(attack);
        if (attack) attackBanner.SetAsLastSibling();
    }

    private string Signature()
    {
        int lvl = PlayerLevel.Instance != null ? PlayerLevel.Instance.currentLevel : 1;
        int maxMana = ManaSystem.Instance != null ? ManaSystem.Instance.Max : 0;
        string s = $"{currentTab}|{lvl}|{themePickerLayer}|{maxMana}";
        if (currentTab == CatalogCategory.Treasure && LootSystem.Instance != null)
        {
            foreach (var d in LootSystem.Instance.Owned()) s += $"|{d.id}{LootSystem.Instance.CountOf(d.id)}";
            s += $"|{PlacedObject.Count(ItemKind.Decor)}";
        }
        if (currentTab == CatalogCategory.Layer && DungeonLayers.Instance != null)
        {
            var core = FindFirstObjectByType<DungeonCore>();
            int coreLayer = core != null ? DungeonLayers.Instance.IndexAt(core.transform.position) : 0;
            s += $"|{coreLayer}|{DungeonLayers.Instance.CurrentView}";
            foreach (var l in DungeonLayers.Instance.layers) s += $"|{l.unlocked}{l.connected}{l.themeIndex}";
        }
        return s;
    }

    // ─────────────────────────────────────────────
    // CONTENU
    // ─────────────────────────────────────────────

    public static int RebuildCount;

    private void Rebuild()
    {
        RebuildCount++;
        contentSignature = Signature();
        itemButtons.Clear();
        // Destroy est différé : on détache d'abord pour que FitContent ne compte pas les anciens éléments
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var old = content.GetChild(i).gameObject;
            old.transform.SetParent(null, false);
            Destroy(old);
        }
        Vector2 keep = content.anchoredPosition;   // un rafraîchissement ne ramène pas la liste en haut

        if (currentTab == CatalogCategory.Layer)
        {
            if (themePickerLayer >= 0) BuildThemePicker(themePickerLayer);
            else BuildLayers();
            FitContent();
            content.anchoredPosition = keep;
            return;
        }

        if (currentTab == CatalogCategory.Treasure)
        {
            BuildTreasure();
            FitContent();
            content.anchoredPosition = keep;
            return;
        }

        var items = new List<CatalogItem>();
        if (currentTab == CatalogCategory.Build)
            items.Add(new CatalogItem { id = "select", displayName = "Sélection", description = "Commander les monstres", kind = ItemKind.Tool, tool = DungeonBuilder.BuildMode.None });
        items.AddRange(DungeonCatalog.Get().InCategory(currentTab));

        const float bw = 202f, bh = 118f, gap = 8f;
        for (int i = 0; i < items.Count; i++)
        {
            int col = i % 2, row = i / 2;
            var item = items[i];
            var b = Btn(content, "Item_" + item.id, "", () => OnItemClicked(item));
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(col * (bw + gap), -row * (bh + gap));
            rt.sizeDelta = new Vector2(bw, bh);

            if (item.icon != null)
            {
                var icon = NewRect("Icon", rt);
                icon.anchorMin = icon.anchorMax = new Vector2(0, 0.5f);
                icon.pivot = new Vector2(0, 0.5f);
                icon.anchoredPosition = new Vector2(10, 4);
                icon.sizeDelta = new Vector2(64, 64);
                var img = icon.gameObject.AddComponent<Image>();
                img.sprite = item.icon;
                img.color = item.iconTint;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            float tx = item.icon != null ? 80f : 10f;
            var name = Txt(rt, item.displayName, 21, TextColor, TextAlignmentOptions.TopLeft);
            Place(name.rectTransform, new Vector2(tx, -12), new Vector2(bw - tx - 6, 56));
            name.textWrappingMode = TextWrappingModes.Normal;
            int shownCost = item.kind == ItemKind.Tool && item.tool == DungeonBuilder.BuildMode.MovingCore && DungeonBuilder.Instance != null
                ? DungeonBuilder.Instance.MoveCoreCost : item.cost;
            bool freeMove = item.kind == ItemKind.Tool && item.tool == DungeonBuilder.BuildMode.MovingCore && DungeonBuilder.Instance != null && DungeonBuilder.Instance.FirstCoreMoveFree;
            if (freeMove)
            {
                var cost = Txt(rt, "Gratuit (1re fois)", 18, new Color(0.6f, 0.95f, 0.65f), TextAlignmentOptions.BottomLeft);
                Place(cost.rectTransform, new Vector2(tx, -bh + 34), new Vector2(bw - tx - 6, 28));
            }
            else if (shownCost > 0)
            {
                var cost = Txt(rt, $"{shownCost} mana", 18, GameHUD.ManaColor, TextAlignmentOptions.BottomLeft);
                Place(cost.rectTransform, new Vector2(tx, -bh + 34), new Vector2(bw - tx - 6, 28));
            }

            var lockGo = NewRect("Lock", rt);
            Stretch(lockGo, 0, 0, 0, 0);
            var lockImg = lockGo.gameObject.AddComponent<Image>();
            lockImg.sprite = SpriteUtil.RoundedUI;
            lockImg.type = Image.Type.Sliced;
            lockImg.color = new Color(0.03f, 0.02f, 0.06f, 0.55f);
            lockImg.raycastTarget = false;
            // verrou discret sur l'icône, niveau requis dessous (ne cache plus le nom de l'objet)
            UIKit.Icon(lockGo, "lock", new Vector2(6, -6), 24, new Vector2(0, 1));
            var lockText = Txt(lockGo, "", 17, GameHUD.WarningColor, TextAlignmentOptions.Bottom);
            Stretch(lockText.rectTransform, 2, bw - 78, 8, 8);

            itemButtons.Add(new ItemButton { item = item, tool = item.tool, bg = b.GetComponent<Image>(), lockOverlay = lockGo.gameObject, lockText = lockText });
        }
        FitContent();
        content.anchoredPosition = keep;
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        var builder = DungeonBuilder.Instance;
        int lvl = PlayerLevel.Instance != null ? PlayerLevel.Instance.currentLevel : 1;
        foreach (var b in itemButtons)
        {
            bool locked = lvl < b.item.unlockLevel;
            b.lockOverlay.SetActive(locked);
            if (locked) b.lockText.text = $"Niv. {b.item.unlockLevel}";
            bool selected = builder != null && (b.item.kind == ItemKind.Tool
                ? builder.GetCurrentMode() == b.tool && builder.SelectedItem == null
                : builder.SelectedItem != null && builder.SelectedItem.id == b.item.id);
            b.bg.color = selected ? ActiveColor : (locked ? LockedColor : ButtonColor);
        }
    }

    private void OnItemClicked(CatalogItem item)
    {
        int lvl = PlayerLevel.Instance != null ? PlayerLevel.Instance.currentLevel : 1;
        if (lvl < item.unlockLevel)
        {
            GameHUD.Toast($"{item.displayName} : niveau {item.unlockLevel} requis", GameHUD.WarningColor);
            return;
        }
        var builder = DungeonBuilder.Instance;
        if (builder == null) return;
        // Recliquer sur l'élément déjà choisi l'annule (retour à la sélection)
        bool alreadySelected = item.kind == ItemKind.Tool
            ? builder.GetCurrentMode() == item.tool && item.tool != DungeonBuilder.BuildMode.None
            : builder.SelectedItem != null && builder.SelectedItem.id == item.id;
        if (alreadySelected)
        {
            builder.SetMode(DungeonBuilder.BuildMode.None);
            return;
        }
        builder.SelectItem(item);
        // Sur petit écran, on replie le panneau pour voir la carte
        if (PointerUtil.IsTouch && Screen.width < 1600 && item.kind != ItemKind.Tool) SetExpanded(false);
    }

    // ─────────────────────────────────────────────
    // TRÉSOR (butin des intrus)
    // ─────────────────────────────────────────────

    private void BuildTreasure()
    {
        var loot = LootSystem.Instance;
        if (loot == null) return;
        float prestige = PlacedObject.TotalPrestige();
        float regen = ManaSystem.Instance != null ? ManaSystem.Instance.PrestigeRegen : 0f;
        var head = Txt(content, $"<color=#FFD966>Prestige {prestige:0}</color>  <size=80%>(+{regen:0.00} mana/s, renommée chaque jour)</size>\n" +
                                $"<size=78%><color=#9A96B0>Le butin des intrus tués atterrit ici. Exposez-le en trophée (table dans le donjon) ou absorbez-le.</color></size>",
                       19, TextColor, TextAlignmentOptions.TopLeft);
        head.textWrappingMode = TextWrappingModes.Normal;
        Place(head.rectTransform, new Vector2(4, 0), new Vector2(Width - 40, 74));

        var owned = loot.Owned();
        if (owned.Count == 0)
        {
            var empty = Txt(content, "<color=#6F6A80>Le trésor est vide. Tuez des intrus : ils laissent parfois tomber leur équipement.</color>", 18, SubText, TextAlignmentOptions.TopLeft);
            empty.textWrappingMode = TextWrappingModes.Normal;
            Place(empty.rectTransform, new Vector2(4, -84), new Vector2(Width - 40, 60));
            return;
        }
        float y = 84f;
        foreach (var d in owned)
        {
            var def = d;
            var card = NewRect("Loot_" + d.id, content);
            card.anchorMin = new Vector2(0, 1);
            card.anchorMax = new Vector2(1, 1);
            card.pivot = new Vector2(0.5f, 1);
            card.anchoredPosition = new Vector2(0, -y);
            card.sizeDelta = new Vector2(0, 112);
            var img = card.gameObject.AddComponent<Image>();
            img.sprite = SpriteUtil.RoundedUI;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.12f, 0.1f, 0.18f, 0.96f);

            var icon = NewRect("Icon", card);
            Place(icon, new Vector2(10, -14), new Vector2(64, 64));
            var ii = icon.gameObject.AddComponent<Image>();
            ii.sprite = d.icon;
            ii.preserveAspect = true;
            ii.raycastTarget = false;
            var rc = LootSystem.RarityColors[d.rarity];
            int count = loot.CountOf(d.id);
            var n = Txt(card, $"{d.name}{(count > 1 ? $" <color=#FFFFFF>×{count}</color>" : "")}", 20, rc, TextAlignmentOptions.TopLeft);
            Place(n.rectTransform, new Vector2(84, -8), new Vector2(Width - 120, 28));
            var info = Txt(card, $"<size=85%>{LootSystem.RarityNames[d.rarity]} · prestige {d.Prestige:0} · {d.Mana} mana</size>", 16, SubText, TextAlignmentOptions.TopLeft);
            Place(info.rectTransform, new Vector2(84, -36), new Vector2(Width - 120, 24));

            var expo = Btn(card, "Expose", "Exposer", () =>
            {
                var item = LootSystem.Instance.TrophyItem(def.id);
                DungeonBuilder.Instance?.SelectItem(item);
                if (PointerUtil.IsTouch && Screen.width < 1600) SetExpanded(false);
            }, 17);
            var ert = (RectTransform)expo.transform;
            ert.anchorMin = ert.anchorMax = new Vector2(0, 0);
            ert.pivot = new Vector2(0, 0);
            ert.anchoredPosition = new Vector2(84, 8);
            ert.sizeDelta = new Vector2(150, 40);
            expo.GetComponent<Image>().color = new Color(0.55f, 0.42f, 0.12f, 1f);
            var abs = Btn(card, "Absorb", "Absorber", () => { LootSystem.Instance.Absorb(def.id); Rebuild(); }, 17);
            var art = (RectTransform)abs.transform;
            art.anchorMin = art.anchorMax = new Vector2(0, 0);
            art.pivot = new Vector2(0, 0);
            art.anchoredPosition = new Vector2(244, 8);
            art.sizeDelta = new Vector2(150, 40);
            abs.GetComponent<Image>().color = new Color(0.3f, 0.22f, 0.55f, 1f);
            y += 120f;
        }
    }

    // ─────────────────────────────────────────────
    // ÉTAGES
    // ─────────────────────────────────────────────

    private void BuildLayers()
    {
        var layers = DungeonLayers.Instance;
        if (layers == null) return;
        var core = FindFirstObjectByType<DungeonCore>();
        int coreLayer = core != null ? layers.IndexAt(core.transform.position) : 0;
        int pending = layers.PendingConnectionLayer();
        float y = 0f;

        for (int i = 0; i < layers.layers.Count; i++)
        {
            int idx = i;
            var l = layers.layers[i];
            var theme = layers.ThemeOf(i);
            var card = NewRect("Layer" + i, content);
            card.anchorMin = new Vector2(0, 1);
            card.anchorMax = new Vector2(1, 1);
            card.pivot = new Vector2(0.5f, 1);
            card.anchoredPosition = new Vector2(0, -y);
            card.sizeDelta = new Vector2(0, 176);
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.sprite = SpriteUtil.RoundedUI;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = layers.CurrentView == i ? new Color(0.25f, 0.18f, 0.4f, 0.95f) : new Color(0.12f, 0.1f, 0.18f, 0.95f);

            string title = $"{l.name}" + (coreLayer == i ? "  <color=#FFD966>(cœur)</color>" : "");
            var t = Txt(card, title, 23, TextColor, TextAlignmentOptions.TopLeft);
            Place(t.rectTransform, new Vector2(14, -10), new Vector2(Width - 60, 32));

            string status;
            if (!l.unlocked) status = $"<color=#FF9A8A>Verrouillé</color> · Niveau {l.unlockLevel} · {l.unlockCost} mana";
            else status = $"{theme?.displayName}" + (i > 0 && !l.connected ? "  <color=#FFC266>· à relier par un escalier</color>" : "");
            var st = Txt(card, status, 18, SubText, TextAlignmentOptions.TopLeft);
            Place(st.rectTransform, new Vector2(14, -44), new Vector2(Width - 60, 50));
            st.textWrappingMode = TextWrappingModes.Normal;

            float bx = 12f;
            if (l.unlocked)
            {
                SmallBtn(card, "Voir", ref bx, () => layers.SetView(idx));
                if (i + 1 < layers.layers.Count && pending == i + 1)
                    SmallBtn(card, "Escalier", ref bx, () => DungeonBuilder.Instance?.SetMode(DungeonBuilder.BuildMode.PlacingStairs), 130f);
                if (l.connected && DungeonBuilder.Instance != null)
                    SmallBtn(card, coreLayer == i ? "Déplacer cœur" : "Cœur ici…", ref bx, () =>
                    {
                        layers.SetView(idx);
                        DungeonBuilder.Instance.SetMode(DungeonBuilder.BuildMode.MovingCore);
                    }, 170f);
            }
            else if (i > 0 && layers.layers[i - 1].unlocked)
            {
                SmallBtn(card, "Débloquer…", ref bx, () =>
                {
                    if (layers.CanUnlock(idx, out string reason)) { themePickerLayer = idx; Rebuild(); ScrollToTop(); }
                    else GameHUD.Toast(reason, GameHUD.WarningColor);
                }, 170f);
            }
            y += 184f;
        }
    }

    private void BuildThemePicker(int layerIndex)
    {
        var layers = DungeonLayers.Instance;
        var cat = DungeonCatalog.Get();
        var title = Txt(content, $"Thème de {layers.layers[layerIndex].name}", 24, GameHUD.GoldColor, TextAlignmentOptions.TopLeft);
        Place(title.rectTransform, new Vector2(4, 0), new Vector2(Width - 30, 34));
        float y = 42f;
        for (int i = 1; i < cat.themes.Count; i++)   // le thème 0 est la roche de surface
        {
            int ti = i;
            var th = cat.themes[i];
            var b = Btn(content, "Theme" + i, "", () =>
            {
                if (layers.Unlock(layerIndex, ti)) { themePickerLayer = -1; layers.SetView(layerIndex); Rebuild(); }
            });
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -y);
            rt.sizeDelta = new Vector2(0, 82);
            var swatch = NewRect("Swatch", rt);
            swatch.anchorMin = swatch.anchorMax = new Vector2(0, 0.5f);
            swatch.pivot = new Vector2(0, 0.5f);
            swatch.anchoredPosition = new Vector2(8, 0);
            swatch.sizeDelta = new Vector2(58, 58);
            var sw = swatch.gameObject.AddComponent<Image>();
            // aperçu : un bloc du mur du thème (dessus + face)
            var wallSprite = th.wallTile is AutoWallTile aw ? aw.SpriteFor(1 | 2 | 8) : null;
            if (wallSprite != null) { sw.sprite = wallSprite; sw.color = Color.white; }
            else sw.color = th.stoneTint;
            sw.raycastTarget = false;
            var n = Txt(rt, th.displayName, 21, TextColor, TextAlignmentOptions.TopLeft);
            Place(n.rectTransform, new Vector2(82, -6), new Vector2(Width - 110, 28));
            var d = Txt(rt, th.description, 14, SubText, TextAlignmentOptions.TopLeft);
            Place(d.rectTransform, new Vector2(82, -32), new Vector2(Width - 110, 50));
            d.textWrappingMode = TextWrappingModes.Normal;
            y += 86f;
        }
        var back = Btn(content, "Back", "Annuler", () => { themePickerLayer = -1; Rebuild(); }, 20);
        var brt = (RectTransform)back.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0, 1);
        brt.pivot = new Vector2(0, 1);
        brt.anchoredPosition = new Vector2(0, -y - 4);
        brt.sizeDelta = new Vector2(160, 50);
    }

    private void SmallBtn(RectTransform parent, string label, ref float x, UnityAction a, float w = 110f)
    {
        var b = Btn(parent, "B_" + label, label, a, 18);
        var rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(x, 12);
        rt.sizeDelta = new Vector2(w, 48);
        x += w + 8f;
    }

    // ─────────────────────────────────────────────
    // FABRIQUE
    // ─────────────────────────────────────────────

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt, float l, float r, float t, float b)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static TextMeshProUGUI Txt(Transform parent, string text, float size, Color c, TextAlignmentOptions align)
    {
        var t = NewRect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = c;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    private static Button Btn(Transform parent, string name, string label, UnityAction onClick, float fontSize = 22)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = SpriteUtil.RoundedUI;
        img.type = Image.Type.Sliced;
        img.color = ButtonColor;
        var b = rt.gameObject.AddComponent<Button>();
        var cols = b.colors;
        cols.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cols.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        b.colors = cols;
        b.onClick.AddListener(() => Sfx.Play("ui_click", null, 0.6f, 0.04f));
        b.onClick.AddListener(onClick);
        if (!string.IsNullOrEmpty(label))
        {
            var t = Txt(rt, label, fontSize, TextColor, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, 4, 4, 0, 0);
        }
        return b;
    }
}
