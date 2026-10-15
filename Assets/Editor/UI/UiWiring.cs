using UnityEditor;
using UnityEngine;

// [SerializeField] alanlarini disaridan doldurmak icin.
//
// Alanlari public yapmak yerine SerializedObject kullaniyoruz: calisma anindaki
// API yuzeyi builder yuzunden genislemesin. Yazma "farkliysa yaz" bicimde --
// birebir PaintingLibraryBuilder.AssignToSceneManagers kalibi -- boylece komutu
// ikinci kez calistirmak hicbir sahneyi kirli isaretlemiyor.
public static class UiWiring
{
    public static bool Set(Object target, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = Find(serialized, target, field);

        if (property == null || property.objectReferenceValue == value)
        {
            return false;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    public static bool SetArray(Object target, string field, Object[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = Find(serialized, target, field);

        if (property == null)
        {
            return false;
        }

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    public static bool SetBool(Object target, string field, bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = Find(serialized, target, field);

        if (property == null || property.boolValue == value)
        {
            return false;
        }

        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    public static bool SetFloat(Object target, string field, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = Find(serialized, target, field);

        if (property == null || Mathf.Approximately(property.floatValue, value))
        {
            return false;
        }

        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    public static bool SetInt(Object target, string field, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = Find(serialized, target, field);

        if (property == null || property.intValue == value)
        {
            return false;
        }

        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    public static bool SetColor(Object target, string field, Color value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = Find(serialized, target, field);

        if (property == null || property.colorValue == value)
        {
            return false;
        }

        property.colorValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    public static bool SetVector2(Object target, string field, Vector2 value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = Find(serialized, target, field);

        if (property == null || property.vector2Value == value)
        {
            return false;
        }

        property.vector2Value = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    // Alan adi yanlis yazilirsa sessizce hicbir sey olmaz ve hata sahnede
    // "referans atanmamis" olarak cok sonra ortaya cikardi; bu yuzden bagirıyoruz.
    private static SerializedProperty Find(SerializedObject serialized, Object target, string field)
    {
        SerializedProperty property = serialized.FindProperty(field);

        if (property == null)
        {
            Debug.LogError("UiWiring: " + target.GetType().Name + " uzerinde '" + field + "' alani yok.", target);
        }

        return property;
    }
}
