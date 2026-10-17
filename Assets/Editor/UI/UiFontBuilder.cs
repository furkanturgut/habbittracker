using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Tasarimin fontu Fredoka (Google Fonts). TTF'ler Assets/Fonts altina elle
// konuyor; burasi her agirlik icin bir TMP Font Asset uretiyor.
//
// Atlas DYNAMIC: Turkce glifler (s, g, I, i, c, o, u) talep uzerine ekleniyor,
// yani elde tutulacak sabit bir karakter listesi yok. Statik atlas'ta unutulan
// tek bir karakter ekranda kare olarak gorunurdu.
//
// TTF yoksa is durmuyor: dort agirlik da projedeki LiberationSans SDF'ye dusuyor.
public static class UiFontBuilder
{
    public const string FontFolder = "Assets/Fonts";

    // Tasarimin agirliklari: 400 / 500 / 600 / 700.
    public static readonly string[] Weights = { "Regular", "Medium", "SemiBold", "Bold" };

    private const string FallbackPath =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    // Uretilen ya da bulunan dort font. Sirasi Weights ile ayni.
    public static TMP_FontAsset[] Build(out bool usedFallback)
    {
        EditorAssetUtil.EnsureFolder("Assets", "Fonts");

        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
        TMP_FontAsset[] result = new TMP_FontAsset[Weights.Length];
        usedFallback = false;

        for (int i = 0; i < Weights.Length; i++)
        {
            result[i] = BuildOne(Weights[i]);

            if (result[i] == null)
            {
                result[i] = fallback;
                usedFallback = true;
            }
            else if (fallback != null)
            {
                EnsureGlyphFallback(result[i], fallback);
            }
        }

        return result;
    }

    // Fredoka TTF'de bazi harfler yok: yumusak ge, noktali buyuk I, s-cedilla
    // (Turkce'ye ozgu) ve butun Kiril alfabesi (Rusca) hic yok -- TTF'nin cmap'i
    // dogrulandi. Boyle bir karakterle karsilasinca kare gostermek yerine
    // LiberationSans'a (ve onun kendi Dynamic fallback zincirine, zaten TMP
    // Essentials ile birlikte gelen "- Fallback" varligina) dusuyor. Dinamik
    // atlas ayni mantikla calisiyor: talep edilen karakter burada da yoksa
    // LiberationSans'in kendi fallback'i onu TTF'den isteyince ekliyor.
    private static void EnsureGlyphFallback(TMP_FontAsset font, TMP_FontAsset glyphFallback)
    {
        if (font.fallbackFontAssetTable.Contains(glyphFallback))
        {
            return;
        }

        font.fallbackFontAssetTable.Add(glyphFallback);
        EditorUtility.SetDirty(font);
    }

    private static TMP_FontAsset BuildOne(string weight)
    {
        string assetPath = FontFolder + "/Fredoka-" + weight + " SDF.asset";

        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null)
        {
            return existing;
        }

        string ttfPath = FontFolder + "/Fredoka-" + weight + ".ttf";
        Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            return null;
        }

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
            font,
            90,
            9,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            true);

        if (asset == null)
        {
            Debug.LogError("UiFontBuilder: " + ttfPath + " icin TMP font asset uretilemedi.");
            return null;
        }

        asset.name = "Fredoka-" + weight + " SDF";
        AssetDatabase.CreateAsset(asset, assetPath);

        // Atlas texture'i ve materyal font asset'in ICINDE yasamali; ayri dosya
        // olarak birakilirsa varlik tasinirken referanslar kopar.
        if (asset.atlasTextures != null && asset.atlasTextures.Length > 0)
        {
            asset.atlasTextures[0].name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        }

        if (asset.material != null)
        {
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }

        EditorUtility.SetDirty(asset);
        return asset;
    }
}
