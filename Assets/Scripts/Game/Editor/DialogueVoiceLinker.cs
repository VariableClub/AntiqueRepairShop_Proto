using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dokkaebi.EditorTools
{
    // 대사 음성 연결 (실험): 메뉴 「도깨비 > 대사 음성 연결」
    //  Assets/Audio/Voice/ 안의 파일 이름이 대사 ID(D01.mp3, H01.mp3 …)와 같으면
    //  프로젝트의 모든 대사 표에서 같은 ID의 줄에 음성으로 넣는다. 파일이 없는 줄은 건드리지 않는다.
    //  음성 파일은 tools/tts/generate_dialogue.py 로 만든다.
    public static class DialogueVoiceLinker
    {
        const string VoiceDir = "Assets/Audio/Voice";
        static readonly string[] Exts = { ".mp3", ".wav", ".ogg" };

        [MenuItem("도깨비/대사 음성 연결")]
        public static void Link()
        {
            if (!Directory.Exists(VoiceDir))
            {
                Debug.LogWarning($"[대사 음성] {VoiceDir} 폴더가 없음. tools/tts/generate_dialogue.py 로 먼저 만든다");
                return;
            }
            AssetDatabase.Refresh();
            int linked = 0, missing = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:DialogueTable"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var table = AssetDatabase.LoadAssetAtPath<DialogueTable>(path);
                if (table == null) continue;
                bool changed = false;
                foreach (var line in table.lines)
                {
                    AudioClip clip = null;
                    foreach (var ext in Exts)
                    {
                        clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{VoiceDir}/{line.id}{ext}");
                        if (clip != null) break;
                    }
                    if (clip == null) { if (line.speaker == Speaker.Dokkaebi) missing++; continue; }
                    if (line.voice != clip) { line.voice = clip; changed = true; }
                    linked++;
                    Debug.Log($"[대사 음성] {line.id} ← {clip.name} ({clip.length:0.0}초)");
                }
                if (changed) { EditorUtility.SetDirty(table); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[대사 음성] 연결 {linked}줄, 도깨비 대사 중 음성 없는 줄 {missing}");
        }
    }
}
