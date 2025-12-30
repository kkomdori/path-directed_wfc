#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CubeData))]
public class CubeDataEditor : Editor
{
    private Editor prefabEditor;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // 기존 필드 출력

        CubeData data = (CubeData)target; // 현재 인스턴스가 그리고 있는 오브젝트를 가리키는 기본변수 target

        if (data.cubePrefab != null)
        {
            GUILayout.Space(10);
            GUILayout.Label("Prefab Preview", EditorStyles.boldLabel);


            // 미리보기 렌더링 준비
            if (prefabEditor == null)
                prefabEditor = Editor.CreateEditor(data.cubePrefab);

            // 프리뷰 박스 렌더링
            if (prefabEditor != null)
            {
                Rect previewRect = GUILayoutUtility.GetRect(128, 256); // 미리보기 박스 크기 설정
                prefabEditor.OnPreviewGUI(previewRect, EditorStyles.helpBox);
            }
        }
    }

    private void OnDisable()
    {
        if (prefabEditor != null)
        {
            DestroyImmediate(prefabEditor);
            prefabEditor = null;
        }
    }
}
#endif