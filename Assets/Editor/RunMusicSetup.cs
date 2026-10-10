using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RunMusicSetup
{
    const string Request = "Temp/PulseRun_SetupMusic.request";
    const string MusicPath = "Assets/music/Neo Rainbow.flac";
    static RunMusicSetup()
    {
        if (File.Exists(Request)) EditorApplication.update += SetupWhenReady;
    }
    static void SetupWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        string path = SceneManager.GetActiveScene().path;
        if (path != "Assets/PulseLook/NeonRunway.unity" && path != "Assets/Scenes/SampleScene.unity") return;
        if (FindRunner() == null) return;
        EditorApplication.update -= SetupWhenReady;
        if (Setup()) File.Delete(Request);
    }
    static RunnerController FindRunner()
    {
        RunnerController found = null;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (RunnerController runner in root.GetComponentsInChildren<RunnerController>(true))
            {
                if (found != null) return null;
                found = runner;
            }
        return found;
    }
    [MenuItem("PulseRun/Setup Neon Rainbow Music")]
    public static void SetupMenu()
    {
        if (Setup() && File.Exists(Request)) File.Delete(Request);
    }
    static bool Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        RunnerController runner = FindRunner();
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
        if (runner == null || clip == null)
        {
            Debug.LogWarning("需要当前场景中有一个 RunnerController，并能导入 Assets/music/Neo Rainbow.flac。");
            return false;
        }
        RunMusicClock clock = runner.musicClock;
        if (clock == null)
        {
            GameObject obj = new GameObject("Music Timeline - Neon Rainbow");
            SceneManager.MoveGameObjectToScene(obj,runner.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(obj,"Add Music Timeline");
            clock = Undo.AddComponent<RunMusicClock>(obj);
            clock.music = clip;
            AudioSource source = obj.GetComponent<AudioSource>();
            source.playOnAwake = false;source.spatialBlend = 0;source.loop = false;
            source.clip = clip;source.volume = clock.volume;
            Undo.RecordObject(runner,"Bind Music Timeline");
            runner.musicClock = clock;
        }
        EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
        Selection.activeGameObject = clock.gameObject;
        Debug.Log($"PULSE_MUSIC_SETUP_OK：{clip.name}，{clip.length:F2}秒。起跑和重试已接入音乐时钟，BPM尚未校准。请保存场景后运行。",clock);
        return true;
    }
}
