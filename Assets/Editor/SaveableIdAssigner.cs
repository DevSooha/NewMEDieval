using UnityEditor;
using UnityEngine;

/// <summary>
/// SaveableBehaviour 서브클래스의 커스텀 인스펙터.
/// _saveId가 비어있으면 경고를 표시하고 GUID 자동 생성 버튼을 제공한다.
/// </summary>
[CustomEditor(typeof(SaveableBehaviour), editorForChildClasses: true)]
public class SaveableBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty saveIdProp = serializedObject.FindProperty("_saveId");

        if (saveIdProp != null && string.IsNullOrEmpty(saveIdProp.stringValue))
        {
            EditorGUILayout.HelpBox(
                "SaveId가 비어 있습니다. 오브젝트 이동·리네임 시 저장 데이터가 깨지므로 반드시 고유 ID를 부여하세요.",
                MessageType.Warning
            );
            if (GUILayout.Button("GUID 자동 생성"))
            {
                saveIdProp.stringValue = System.Guid.NewGuid().ToString();
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
        }

        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
    }
}

/// <summary>
/// SavePoint(및 Bonfire 등 서브클래스)의 커스텀 인스펙터.
/// savePointId가 비어있으면 경고를 표시하고 GUID 자동 생성 버튼을 제공한다.
/// </summary>
[CustomEditor(typeof(SavePoint), editorForChildClasses: true)]
public class SavePointEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty idProp = serializedObject.FindProperty("savePointId");

        if (idProp != null && string.IsNullOrEmpty(idProp.stringValue))
        {
            EditorGUILayout.HelpBox(
                "savePointId가 비어 있습니다. 저장 시 이 포인트를 식별할 수 없습니다.",
                MessageType.Warning
            );
            if (GUILayout.Button("GUID 자동 생성"))
            {
                idProp.stringValue = System.Guid.NewGuid().ToString();
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
        }

        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
    }
}
