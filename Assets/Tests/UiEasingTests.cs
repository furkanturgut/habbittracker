using NUnit.Framework;

// Easing.OutCubic saf bir matematik fonksiyonu -- sahne ya da MonoBehaviour
// gerekmiyor. Buton basma geri bildirimi (ButtonPressFeedback) ve ileride
// diger animasyonlar buraya yaslaniyor; egri bozulursa buradan yakalanir.
public class UiEasingTests
{
    [Test]
    public void OutCubic_EndpointsAreExact()
    {
        Assert.AreEqual(0f, Easing.OutCubic(0f), 0.0001f);
        Assert.AreEqual(1f, Easing.OutCubic(1f), 0.0001f);
    }

    [Test]
    public void OutCubic_IsMonotonicAndStaysInRange()
    {
        float previous = Easing.OutCubic(0f);

        for (int i = 1; i <= 20; i++)
        {
            float k = i / 20f;
            float value = Easing.OutCubic(k);

            Assert.GreaterOrEqual(value, previous);
            Assert.GreaterOrEqual(value, 0f);
            Assert.LessOrEqual(value, 1f);

            previous = value;
        }
    }

    [Test]
    public void OutCubic_FrontLoadsTheMotion()
    {
        // "Ease-out": hareketin cogu basta olmali, yani orta noktada lineer
        // k=0.5'ten daha ileride olmali.
        Assert.Greater(Easing.OutCubic(0.5f), 0.5f);
    }

    [Test]
    public void Breathe_EndpointsAreZero()
    {
        // 0 -> 1 -> 0: tur basinda ve tam yarisinda (donusun tepe noktasi
        // disinda) deger sifira yakin olmali.
        Assert.AreEqual(0f, Easing.Breathe(0f, 2f), 0.0001f);
        Assert.AreEqual(0f, Easing.Breathe(2f, 2f), 0.0001f, "Bir tam tur sonra basa donmeli.");
    }

    [Test]
    public void Breathe_PeakIsAtHalfPeriod()
    {
        Assert.AreEqual(1f, Easing.Breathe(1f, 2f), 0.0001f);
    }

    [Test]
    public void Breathe_StaysInZeroToOneRange()
    {
        for (int i = 0; i <= 40; i++)
        {
            float elapsed = i / 10f;
            float value = Easing.Breathe(elapsed, 2f);

            Assert.GreaterOrEqual(value, 0f);
            Assert.LessOrEqual(value, 1f);
        }
    }

    [Test]
    public void Breathe_IsSymmetricAroundThePeak()
    {
        // Tepe noktasindan esit uzaklikta iki nokta ayni degeri vermeli --
        // "nefes alma" ileri-geri bir egri, testere disi degil.
        float period = 2f;
        float beforePeak = Easing.Breathe(0.5f, period);
        float afterPeak = Easing.Breathe(1.5f, period);

        Assert.AreEqual(beforePeak, afterPeak, 0.0001f);
    }

    [Test]
    public void OutEmphasized_EndpointsAreExact()
    {
        Assert.AreEqual(0f, Easing.OutEmphasized(0f), 0.0001f);
        Assert.AreEqual(1f, Easing.OutEmphasized(1f), 0.0001f);
    }

    [Test]
    public void OutEmphasized_IsMonotonicAndStaysInRange()
    {
        float previous = Easing.OutEmphasized(0f);

        for (int i = 1; i <= 20; i++)
        {
            float k = i / 20f;
            float value = Easing.OutEmphasized(k);

            Assert.GreaterOrEqual(value, previous - 0.0001f);
            Assert.GreaterOrEqual(value, 0f);
            Assert.LessOrEqual(value, 1f);

            previous = value;
        }
    }

    [Test]
    public void OutEmphasized_FrontLoadsTheMotion()
    {
        // cubic-bezier(.2,.8,.2,1): OutCubic'ten de hizli acilir, orta nokta
        // lineer k=0.5'in cok ilerisinde olmali.
        Assert.Greater(Easing.OutEmphasized(0.5f), 0.9f);
    }
}
