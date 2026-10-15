// Ressamin modunun kullaniciya gorunen adi.
//
// PainterManager'in icinde degil: orasi puan ve animator isiyle ilgileniyor,
// metin sunum katmaninin isi. Enum buyudugunde derleyici burayi da uyarsin diye
// switch varsayilani en notr moda dusuyor.
public static class MoodPresentation
{
    public static string Label(PainterManager.PainterMoods mood)
    {
        switch (mood)
        {
            case PainterManager.PainterMoods.UltraRich: return Loc.Tr("mood.ultra_rich");
            case PainterManager.PainterMoods.Rich: return Loc.Tr("mood.rich");
            case PainterManager.PainterMoods.Happy: return Loc.Tr("mood.happy");
            case PainterManager.PainterMoods.Sad: return Loc.Tr("mood.sad");
            case PainterManager.PainterMoods.Starve: return Loc.Tr("mood.starve");
            default: return Loc.Tr("mood.normal");
        }
    }
}
