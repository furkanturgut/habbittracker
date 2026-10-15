using System.Collections.Generic;
using NUnit.Framework;

// HabitCategories'in EditMode testleri: sahne/MonoBehaviour gerektirmeyen
// saf bir sinif (bkz. HabitCategories.cs basi). HabitUiLogicTests.cs zaten
// 492 satir butcesine yakin oldugu icin ayri dosya.
public class HabitCategoryTests
{
    [Test]
    public void All_HasTwelveUniqueEntriesAndNoLegacyCategories()
    {
        HashSet<string> seen = new HashSet<string>(System.StringComparer.Ordinal);

        Assert.AreEqual(12, HabitCategories.All.Length);

        for (int i = 0; i < HabitCategories.All.Length; i++)
        {
            Assert.IsTrue(seen.Add(HabitCategories.All[i]), "yinelenen kategori: " + HabitCategories.All[i]);
        }

        // Eski kategoriler (Zihin/Uretkenlik) listeden cikti ama const olarak
        // hala erisilebilir -- eski kayitlar kendi basligiyla gorunmeye devam
        // etmeli (bkz. HabitCategories.cs sinif yorumu).
        Assert.IsFalse(seen.Contains(HabitCategories.Mind));
        Assert.IsFalse(seen.Contains(HabitCategories.Productivity));
        Assert.AreEqual(-1, HabitCategories.IndexOf(HabitCategories.Mind));
        Assert.AreEqual(-1, HabitCategories.IndexOf(HabitCategories.Productivity));
    }

    [Test]
    public void NormalizeCustom_TrimsAndClampsLength()
    {
        string result = HabitCategories.NormalizeCustom("  Bahçem  ", null);
        Assert.AreEqual("Bahçem", result);

        string tooLong = new string('a', HabitCategories.MaxNameLength + 5);
        string clamped = HabitCategories.NormalizeCustom(tooLong, null);
        Assert.AreEqual(HabitCategories.MaxNameLength, clamped.Length);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void NormalizeCustom_BlankInputReturnsEmpty(string raw)
    {
        Assert.AreEqual(string.Empty, HabitCategories.NormalizeCustom(raw, null));
    }

    [Test]
    public void NormalizeCustom_CollidesWithBuiltIn_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, HabitCategories.NormalizeCustom(HabitCategories.Fitness, null));
        Assert.AreEqual(string.Empty, HabitCategories.NormalizeCustom("fitness", null));
    }

    [Test]
    public void NormalizeCustom_CollidesWithExistingCustom_ReturnsEmpty()
    {
        List<string> existing = new List<string> { "Deneme" };

        Assert.AreEqual(string.Empty, HabitCategories.NormalizeCustom("Deneme", existing));
        Assert.AreEqual(string.Empty, HabitCategories.NormalizeCustom("DENEME", existing));
    }

    [Test]
    public void Collect_BuiltInsFirstThenCustomSkippingBlankAndDuplicate()
    {
        List<string> custom = new List<string> { "Bahçem", "", "Fitness", "Bahçem" };
        List<string> buffer = new List<string>();

        HabitCategories.Collect(custom, buffer);

        Assert.AreEqual(HabitCategories.All.Length + 1, buffer.Count,
            "bos/yinelenen/yerlesikle-cakisan girdiler dusmeli");

        for (int i = 0; i < HabitCategories.All.Length; i++)
        {
            Assert.AreEqual(HabitCategories.All[i], buffer[i]);
        }

        Assert.AreEqual("Bahçem", buffer[HabitCategories.All.Length]);
    }

    [Test]
    public void Collect_NullCustomYieldsOnlyBuiltIns()
    {
        List<string> buffer = new List<string>();
        HabitCategories.Collect(null, buffer);

        CollectionAssert.AreEqual(HabitCategories.All, buffer);
    }

    [Test]
    public void IndexOfWithCustom_ResolvesAfterBuiltIns()
    {
        List<string> custom = new List<string> { "Bahçem" };

        Assert.AreEqual(HabitCategories.All.Length, HabitCategories.IndexOf("Bahçem", custom));
        Assert.AreEqual(-1, HabitCategories.IndexOf("Bilinmeyen", custom));
        Assert.AreEqual(0, HabitCategories.IndexOf(HabitCategories.All[0], custom));
    }
}
