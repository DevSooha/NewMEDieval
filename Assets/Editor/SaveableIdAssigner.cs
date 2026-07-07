using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SaveableBehaviour), editorForChildClasses: true)]
public class SaveableBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SaveableIdAssignerUtil.DrawSaveIdField(serializedObject, "_saveId",
            "SaveId가 비어 있습니다. 오브젝트 이동·리네임 시 저장 데이터가 깨지므로 반드시 고유 ID를 부여하세요.",
            target);
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(SavePoint), editorForChildClasses: true)]
public class SavePointEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SaveableIdAssignerUtil.DrawSaveIdField(serializedObject, "savePointId",
            "savePointId가 비어 있습니다. 저장 시 이 포인트를 식별할 수 없습니다.",
            target);
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
    }
}

internal static class SaveableIdAssignerUtil
{
    internal static void DrawSaveIdField(SerializedObject so, string propName, string warningText, Object target)
    {
        so.Update();
        SerializedProperty prop = so.FindProperty(propName);
        if (prop != null && string.IsNullOrEmpty(prop.stringValue))
        {
            EditorGUILayout.HelpBox(warningText, MessageType.Warning);
            if (GUILayout.Button("GUID 자동 생성"))
            {
                prop.stringValue = System.Guid.NewGuid().ToString();
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
        }
    }
}
