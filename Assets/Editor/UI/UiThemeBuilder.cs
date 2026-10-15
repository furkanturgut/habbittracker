using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

// UiTheme.asset'i olusturur ve uretilen/bulunan varliklari ona baglar.
//
// KURAL: renklere asla dokunulmuyor. Varlik bir kez olustuktan sonra builder
// yalnizca sprite ve font SLOTLARINI tazeliyor -- tema, arayuzun elle
// duzenlenebilen tek yuzeyi ve her calistirmada ezilmesi onu ise yaramaz kilardi.
//
// moodIcons bu kuralin bir adim otesi: orada builder yuva ACIYOR ama iceriye
// hicbir sey YAZMIYOR. Mod gorselleri kullanicinin kendi varliklari; yuvayi
// hazirlamak ile icerigi ezmek farkli seyler.
public static class UiThemeBuilder
{
    public const string Folder = "Assets/UI";
    public const string AssetPath = Folder + "/UiTheme.asset";

    public static UiTheme Build()
    {
        EditorAssetUtil.EnsureFolder("Assets", "UI");

        UiTheme theme = AssetDatabase.LoadAssetAtPath<UiTheme>(AssetPath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UiTheme>();
            AssetDatabase.CreateAsset(theme, AssetPath);
        }

        theme.roundedSprites = LoadRoundedSprites();
        theme.circle = Load(UiSpriteGenerator.PathFor(UiSpriteGenerator.CircleName));
        theme.white = Load(UiSpriteGenerator.PathFor(UiSpriteGenerator.WhiteName));
        theme.checkmark = Load(UiSpriteGenerator.PathFor(UiSpriteGenerator.CheckName));
        theme.shadowEllipse = Load(UiSpriteGenerator.PathFor(UiSpriteGenerator.ShadowName));
        theme.shadowBox = Load(UiSpriteGenerator.PathFor(UiSpriteGenerator.SoftShadowName));
        theme.hatch = Load(UiSpriteGenerator.PathFor(UiSpriteGenerator.HatchName));
        theme.radialGlow = Load(UiSpriteGenerator.PathFor(UiSpriteGenerator.RadialGlowName));
        theme.rayRing = Load(UiSpriteGenerator.PathFor(UiSpriteGenerator.RayRingName));
        theme.studioBackground = Load(StudioSpriteImporter.BackgroundPath);
        theme.easelPainting = Load(StudioSpriteImporter.EaselPath);
        theme.moodIcons = EnsureMoodIconSlots(theme.moodIcons);

        theme.flameTiers = EnsureFlameTiers(theme.flameTiers);

        theme.navTabAtolye = Load(UiIconImporter.TabAtolyePath);
        theme.navTabHabits = Load(UiIconImporter.TabHabitsPath);
        theme.navTabGallery = Load(UiIconImporter.TabGalleryPath);
        theme.settingsGear = Load(UiIconImporter.SettingsGearPath);

        theme.rotusFrames = LoadRotusFrames();
        theme.rotusStatic = Load(RotusSpriteImporter.StaticPath);
        theme.rotusEmpty = Load(RotusSpriteImporter.EmptyPath);

        TMP_FontAsset[] fonts = UiFontBuilder.Build(out bool usedFallback);
        theme.regular = fonts[0];
        theme.medium = fonts[1];
        theme.semiBold = fonts[2];
        theme.bold = fonts[3];

        if (usedFallback)
        {
            Debug.Log(
                "UiThemeBuilder: Fredoka bulunamadi, LiberationSans SDF kullaniliyor. " +
                "Tasarimin fontu icin Fredoka-Regular/Medium/SemiBold/Bold.ttf dosyalarini " +
                UiFontBuilder.FontFolder + " altina koyup komutu tekrar calistirin.");
        }

        EditorUtility.SetDirty(theme);
        return theme;
    }

    private static UiTheme.RoundedSprite[] LoadRoundedSprites()
    {
        List<UiTheme.RoundedSprite> result = new List<UiTheme.RoundedSprite>(UiSpriteGenerator.Radii.Length);

        for (int i = 0; i < UiSpriteGenerator.Radii.Length; i++)
        {
            int radius = UiSpriteGenerator.Radii[i];
            Sprite sprite = Load(UiSpriteGenerator.PathFor(UiSpriteGenerator.RoundedName(radius)));

            if (sprite != null)
            {
                result.Add(new UiTheme.RoundedSprite { radius = radius, sprite = sprite });
            }
        }

        return result.ToArray();
    }

    // Her mod icin bir satir acar, ATANMIS sprite'i moda gore eslestirip KORUR.
    //
    // Sira MoodScale.Order'dan geliyor, yani Inspector'daki satirlar ekrandaki
    // dairelerle ayni sirada duruyor: kullanici "soldan ucuncu daire" ile
    // "ucuncu satir"i karistirmiyor. Sprite'lar asla yazilmiyor -- yalnizca
    // mevcut atama tasiniyor (PaintingLibraryBuilder'in elle duzenlenmis
    // alanlari koruma kuralinin aynisi).
    private static UiTheme.MoodIcon[] EnsureMoodIconSlots(UiTheme.MoodIcon[] current)
    {
        PainterManager.PainterMoods[] order = MoodScale.Order;
        UiTheme.MoodIcon[] result = new UiTheme.MoodIcon[order.Length];

        for (int i = 0; i < order.Length; i++)
        {
            result[i].mood = order[i];
            result[i].sprite = FindAssignedIcon(current, order[i]);
        }

        return result;
    }

    private static Sprite FindAssignedIcon(UiTheme.MoodIcon[] current, PainterManager.PainterMoods mood)
    {
        if (current == null)
        {
            return null;
        }

        for (int i = 0; i < current.Length; i++)
        {
            if (current[i].mood == mood)
            {
                return current[i].sprite;
            }
        }

        return null;
    }

    // Dokuz alev basamagi. moodIcons'un TERSI bir durum: alev gorselleri teslim
    // klasorunden gelen HAZIR varliklar (kullanicinin kendi cizimi degil), bu
    // yuzden sprite yuvalarini builder DOLDURUYOR. Ama iki RENK alani teslim
    // tablosundan geliyor ve temanin elle ayarlanabilir yuzeyinde kaliyor --
    // mevcut satirdan tasiniyor, hicbir zaman yazilmiyor (dosya basindaki
    // "renklere asla dokunulmuyor" kurali).
    private static UiTheme.FlameTier[] EnsureFlameTiers(UiTheme.FlameTier[] current)
    {
        UiTheme.FlameTier[] result = new UiTheme.FlameTier[StreakFlame.TierCount];

        for (int tier = 0; tier < result.Length; tier++)
        {
            result[tier] = current != null && tier < current.Length
                ? current[tier]
                : new UiTheme.FlameTier();

            result[tier].tier = tier;
            result[tier].frames = LoadFrames(FlameSpriteImporter.IconName(tier, false));
            result[tier].large = LoadFrames(FlameSpriteImporter.LargeName(tier, false));

            // T0'in gri varyanti yok: seri sifirsa alev zaten sonuk.
            result[tier].pending = tier > 0
                ? LoadFrames(FlameSpriteImporter.IconName(tier, true))
                : System.Array.Empty<Sprite>();
        }

        return result;
    }

    // Bir sayfanin 4 karesi, SOLDAN SAGA sirada. AssetDatabase alt-varliklari
    // sirali vermiyor, bu yuzden kareler ada gore tek tek araniyor -- yanlis
    // sirada bir dizi, alevi geriye dogru oynatirdi ve bunu hicbir derleyici
    // yakalayamazdi.
    private static Sprite[] LoadFrames(string spriteName)
    {
        string path = FlameSpriteImporter.PathFor(spriteName);
        Object[] all = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);

        if (all == null || all.Length == 0)
        {
            return System.Array.Empty<Sprite>();
        }

        Sprite[] frames = new Sprite[FlameSpriteImporter.FrameCount];

        for (int i = 0; i < frames.Length; i++)
        {
            string wanted = FlameSpriteImporter.FrameName(spriteName, i);

            for (int j = 0; j < all.Length; j++)
            {
                if (all[j] is Sprite sprite && sprite.name == wanted)
                {
                    frames[i] = sprite;
                    break;
                }
            }

            if (frames[i] == null)
            {
                // Eksik kare = eksik dizi. Yarim bir dizi dondurmek alevi
                // oynatirken bos kare gosterirdi; FlameSpriteImporter.Validate
                // zaten bu durumda hatayi yazdiriyor.
                return System.Array.Empty<Sprite>();
            }
        }

        return frames;
    }

    // Rotus.png'nin 4 karesi, SOLDAN SAGA sirada. LoadFrames'in (alev) ayni
    // kalibi, RotusSpriteImporter'in kendi yol/ad semasiyla.
    private static Sprite[] LoadRotusFrames()
    {
        Object[] all = AssetDatabase.LoadAllAssetRepresentationsAtPath(RotusSpriteImporter.SheetPath);
        if (all == null || all.Length == 0)
        {
            return System.Array.Empty<Sprite>();
        }

        Sprite[] frames = new Sprite[RotusSpriteImporter.FrameCount];

        for (int i = 0; i < frames.Length; i++)
        {
            string wanted = RotusSpriteImporter.FrameName(i);

            for (int j = 0; j < all.Length; j++)
            {
                if (all[j] is Sprite sprite && sprite.name == wanted)
                {
                    frames[i] = sprite;
                    break;
                }
            }

            if (frames[i] == null)
            {
                return System.Array.Empty<Sprite>();
            }
        }

        return frames;
    }

    private static Sprite Load(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
