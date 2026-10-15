using System;
using System.Collections.Generic;

// Kucuk listelerde "bu deger var mi" aramasi.
//
// List<T>.Contains yerine bu var cunku: string tarafinda varsayilan
// karsilastirici kulturden etkilenebilir, biz her yerde Ordinal istiyoruz; int
// tarafinda ise EqualityComparer<int> uzerinden gitmek gereksiz bir dolayli
// cagri. Ikisi de tik atma yolunda calisiyor, ikisi de tahsis uretmemeli.
public static class ListLookup
{
    public static bool ContainsOrdinal(List<string> values, string value)
    {
        if (values == null)
        {
            return false;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool Contains(List<int> values, int value)
    {
        if (values == null)
        {
            return false;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == value)
            {
                return true;
            }
        }

        return false;
    }
}
