using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SubclassSelectorUtils
{
    private static readonly Dictionary<Type, List<Type>> TypeCache = new Dictionary<Type, List<Type>>();

    public static List<Type> GetDerivedTypes(Type baseType)
    {
        if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(List<>))
        {
            baseType = baseType.GetGenericArguments()[0];
        }

        if (TypeCache.TryGetValue(baseType, out var cachedTypes))
        {
            return cachedTypes;
        }

        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(t => (t.IsSubclassOf(baseType) || baseType.IsAssignableFrom(t)) && !t.IsAbstract && t != baseType)
            .ToList();

        TypeCache[baseType] = types;
        return types;
    }

    public static void ShowTypePopup(Rect position, SerializedProperty property, Type fieldType)
    {
        string currentTypeName = property.managedReferenceFullTypename;
        GenericMenu menu = new GenericMenu();

        menu.AddItem(new GUIContent("Null"), string.IsNullOrEmpty(currentTypeName), () =>
        {
            property.managedReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
        });

        var types = GetDerivedTypes(fieldType);
        foreach (var type in types)
        {
            string cleanMenuName = type.Name.Replace("Assembly-CSharp", "").Trim();

            menu.AddItem(new GUIContent(cleanMenuName), currentTypeName.EndsWith(type.Name), () =>
            {
                property.managedReferenceValue = Activator.CreateInstance(type);
                property.serializedObject.ApplyModifiedProperties();
            });
        }

        menu.DropDown(position);
    }
}

[CustomPropertyDrawer(typeof(ProceduralAnimationSelectorAttribute))]
public class SubclassSelectorDrawer : PropertyDrawer
{
    private const float VerticalSpacing = 3f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        SerializedProperty enableProp = property.FindPropertyRelative("Enable");
        SerializedProperty nameProp = property.FindPropertyRelative("Name");

        string displayLabel = (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
            ? nameProp.stringValue
            : GetCleanTypeName(property.managedReferenceFullTypename, label.text);

        float singleLineHeight = EditorGUIUtility.singleLineHeight;
        Rect rowRect = new Rect(position.x, position.y, position.width, singleLineHeight);

        using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
        {
            float currentX = rowRect.x + 4f;

            if (enableProp != null)
            {
                float toggleWidth = 16f;
                Rect toggleRect = new Rect(currentX, rowRect.y, toggleWidth, singleLineHeight);

                EditorGUI.BeginChangeCheck();
                bool enabledValue = EditorGUI.Toggle(toggleRect, enableProp.boolValue);
                if (EditorGUI.EndChangeCheck())
                {
                    enableProp.boolValue = enabledValue;
                    property.serializedObject.ApplyModifiedProperties();
                }
                currentX += toggleWidth + 8f;
            }

            float popupWidth = 150f;
            float labelWidth = (rowRect.x + rowRect.width) - currentX - popupWidth - 4f;

            Rect labelRect = new Rect(currentX, rowRect.y, labelWidth, singleLineHeight);
            Rect popupRect = new Rect(currentX + labelWidth + 4f, rowRect.y, popupWidth, singleLineHeight);

            string arrow = property.isExpanded ? "▼ " : "▶ ";
            if (GUI.Button(labelRect, new GUIContent(arrow + displayLabel), EditorStyles.boldLabel))
            {
                property.isExpanded = !property.isExpanded;
            }

            string className = GetCleanTypeName(property.managedReferenceFullTypename, "Null (Select Type)");
            if (GUI.Button(popupRect, className, EditorStyles.popup))
            {
                SubclassSelectorUtils.ShowTypePopup(popupRect, property, fieldInfo.FieldType);
            }
        }

        if (property.isExpanded && property.managedReferenceValue != null)
        {
            DrawChildProperties(position, property, singleLineHeight);
        }
    }

    private void DrawChildProperties(Rect position, SerializedProperty property, float singleLineHeight)
    {
        float currentY = position.y + singleLineHeight + VerticalSpacing;
        Rect childrenRect = new Rect(position.x, currentY, position.width, 0f);

        EditorGUI.indentLevel++;
        SerializedProperty endProp = property.GetEndProperty();
        SerializedProperty childProp = property.Copy();

        if (childProp.NextVisible(true))
        {
            do
            {
                if (SerializedProperty.EqualContents(childProp, endProp)) break;
                if (childProp.name == "Enable") continue;

                float childHeight = EditorGUI.GetPropertyHeight(childProp, true);
                childrenRect.height = childHeight;
                childrenRect.y = currentY;

                EditorGUI.PropertyField(childrenRect, childProp, true);

                currentY += childHeight + VerticalSpacing;
            }
            while (childProp.NextVisible(false));
        }
        EditorGUI.indentLevel--;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        if (!property.isExpanded || property.managedReferenceValue == null)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        float height = EditorGUIUtility.singleLineHeight;

        SerializedProperty endProp = property.GetEndProperty(false);
        SerializedProperty childProp = property.Copy();

        if (childProp.NextVisible(true))
        {
            do
            {
                if (SerializedProperty.EqualContents(childProp, endProp)) break;
                if (childProp.name == "Enable") continue;

                height += VerticalSpacing + EditorGUI.GetPropertyHeight(childProp, true);
            }
            while (childProp.NextVisible(false));
        }

        height += VerticalSpacing;

        return height;
    }

    private string GetCleanTypeName(string fullTypeName, string fallback)
    {
        if (string.IsNullOrEmpty(fullTypeName)) return fallback;
        return fullTypeName.Split('.').Last().Replace("Assembly-CSharp", "").Trim();
    }
}
