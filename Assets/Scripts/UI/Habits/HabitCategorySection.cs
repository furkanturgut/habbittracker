using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Listede bir kategori blogu: baslik + o kategorinin kartlari.
//
// Kartlar blok icinde HAVUZLANIYOR; liste her tazelendiginde GameObject yaratip
// yok etmek yerine var olanlar yeniden baglaniyor.
[DisallowMultipleComponent]
public class HabitCategorySection : MonoBehaviour
{
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private HabitCard cardPrefab;
    [SerializeField] private Transform cardRoot;

    private readonly List<HabitCard> cards = new List<HabitCard>(8);

    public event Action<string> CardClicked;

    public void Show(string category, List<HabitData> habits, PaintingLibrary library)
    {
        // ToUpperInvariant "i" -> "I" ceviriyor (Turkce "I"/"i" harfleri
        // kulturden bagimsiz esleme disinda); "Diger" -> "DIGER" yerine
        // "DİĞER" gibi dogru Turkce buyuk harfi elle duzeltiyoruz --
        // kultur bagimliligi getirmeden (ToUpper(tr-TR) kullanmiyoruz).
        // BU DUZELTME TURKCE'YE OZGU: DisplayName baska bir dilde ceviri
        // donduruyorsa dotless/dotted-I sorunu yok, duzeltmeyi uygulamak o
        // metni bozardi (orn. Ingilizce "Finance" -> "FİNANCE").
        string display = HabitCategories.DisplayName(category);
        nameLabel.text = AppSettings.Language == AppLanguage.Turkish
            ? display.Replace('i', 'İ').ToUpperInvariant()
            : display.ToUpperInvariant();
        countLabel.text = habits.Count.ToString();

        for (int i = 0; i < habits.Count; i++)
        {
            HabitCard card = CardAt(i);
            card.gameObject.SetActive(true);
            card.Bind(habits[i], library);
        }

        for (int i = habits.Count; i < cards.Count; i++)
        {
            cards[i].gameObject.SetActive(false);
        }
    }

    private HabitCard CardAt(int index)
    {
        while (cards.Count <= index)
        {
            HabitCard created = Instantiate(cardPrefab, cardRoot);
            created.Clicked += HandleCardClicked;
            cards.Add(created);
        }

        return cards[index];
    }

    private void HandleCardClicked(string habitId)
    {
        CardClicked?.Invoke(habitId);
    }
}
