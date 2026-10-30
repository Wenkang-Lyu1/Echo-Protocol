using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelConfig))]
public sealed class LevelConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("关卡配置", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("levelId"), new GUIContent("关卡 ID"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("durationSeconds"), new GUIContent("关卡时长（秒）"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("echoLimit"), new GUIContent("整关可用回声总数"));
        EditorGUILayout.HelpBox("首波时间从正式战斗开始计时。后续波次在本巢穴上一波全部生成并被消灭后，再等待指定秒数。", MessageType.Info);

        var nests = serializedObject.FindProperty("nests");
        EditorGUILayout.LabelField("巢穴（" + nests.arraySize + "）", EditorStyles.boldLabel);
        for (int i = 0; i < nests.arraySize; i++)
        {
            var nest = nests.GetArrayElementAtIndex(i);
            var id = nest.FindPropertyRelative("nestId");
            var waves = nest.FindPropertyRelative("waves");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            nest.isExpanded = EditorGUILayout.Foldout(nest.isExpanded,
                (string.IsNullOrEmpty(id.stringValue) ? "未命名巢穴" : id.stringValue) + " · " + waves.arraySize + " 波", true);
            bool remove = GUILayout.Button("删除巢穴", GUILayout.Width(72));
            EditorGUILayout.EndHorizontal();
            if (remove)
            {
                nests.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndVertical();
                break;
            }
            if (nest.isExpanded)
            {
                EditorGUILayout.PropertyField(id, new GUIContent("巢穴 ID"));
                EditorGUILayout.PropertyField(nest.FindPropertyRelative("firstWaveTimeSeconds"), new GUIContent("首波开始（秒）"));
                DrawWaves(waves);
            }
            EditorGUILayout.EndVertical();
        }
        if (GUILayout.Button("＋ 添加巢穴"))
        {
            int index = nests.arraySize;
            nests.InsertArrayElementAtIndex(index);
            var nest = nests.GetArrayElementAtIndex(index);
            nest.FindPropertyRelative("nestId").stringValue = UniqueNestId(nests, index);
            nest.FindPropertyRelative("firstWaveTimeSeconds").floatValue = 0;
            nest.FindPropertyRelative("waves").ClearArray();
            nest.isExpanded = true;
        }
        serializedObject.ApplyModifiedProperties();

        try { ((LevelConfig)target).Validate(); }
        catch (InvalidOperationException error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
    }

    private static void DrawWaves(SerializedProperty waves)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("波次", GUILayout.Width(32));
        GUILayout.Label("怪物数量", GUILayout.MinWidth(55));
        GUILayout.Label(new GUIContent("清波后等待（秒）", "上一波全部生成且消灭后，到本波开始的间隔。"), GUILayout.MinWidth(90));
        GUILayout.Space(25);
        EditorGUILayout.EndHorizontal();
        for (int i = 0; i < waves.arraySize; i++)
        {
            var wave = waves.GetArrayElementAtIndex(i);
            var count = wave.FindPropertyRelative("enemyCount");
            var delay = wave.FindPropertyRelative("delayAfterPreviousClearSeconds");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label((i + 1).ToString(), GUILayout.Width(32));
            count.intValue = EditorGUILayout.IntField(count.intValue, GUILayout.MinWidth(55));
            if (i == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("由首波时间决定", GUILayout.MinWidth(90));
            }
            else delay.floatValue = EditorGUILayout.FloatField(delay.floatValue, GUILayout.MinWidth(90));
            bool remove = GUILayout.Button("−", GUILayout.Width(25));
            EditorGUILayout.EndHorizontal();
            if (remove)
            {
                waves.DeleteArrayElementAtIndex(i);
                if (i == 0 && waves.arraySize > 0)
                    waves.GetArrayElementAtIndex(0).FindPropertyRelative("delayAfterPreviousClearSeconds").floatValue = 0;
                break;
            }
        }
        if (GUILayout.Button("＋ 添加波次"))
        {
            int index = waves.arraySize;
            waves.InsertArrayElementAtIndex(index);
            var wave = waves.GetArrayElementAtIndex(index);
            wave.FindPropertyRelative("enemyCount").intValue = 1;
            wave.FindPropertyRelative("delayAfterPreviousClearSeconds").floatValue = 0;
        }
    }

    private static string UniqueNestId(SerializedProperty nests, int newIndex)
    {
        int suffix = newIndex + 1;
        while (true)
        {
            string candidate = "nest_" + suffix++;
            bool exists = false;
            for (int i = 0; i < newIndex; i++)
                if (nests.GetArrayElementAtIndex(i).FindPropertyRelative("nestId").stringValue == candidate)
                    exists = true;
            if (!exists) return candidate;
        }
    }
}
