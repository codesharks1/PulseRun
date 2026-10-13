using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 一次性的旧场景迁移工具；游戏运行时不需要它。
[InitializeOnLoad]
public static class StraightRunwaySetup
{
    const string Request = "Temp/PulseRun_StraightRunway.request";
    const string RootName = "Straight Neon Runway";
    static StraightRunwaySetup()
    {
        if (File.Exists(Request)) EditorApplication.update += ApplyRequestedChange;
    }

    static void ApplyRequestedChange()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/PulseLook/NeonRunway.unity") return;
        // 不覆盖用户尚未保存的编辑；此时可手动使用菜单，修改结果仍可撤销。
        if (scene.isDirty) return;
        EditorApplication.update -= ApplyRequestedChange;
        if (ConvertScene())
        {
            EditorSceneManager.SaveScene(scene);
            File.Delete(Request);
            Debug.Log("StraightRunway: conversion saved successfully.");
        }
    }

    [MenuItem("PulseRun/Convert Scene to Straight Runway")]
    public static void ConvertFromMenu()
    {
        if (ConvertScene() && File.Exists(Request)) File.Delete(Request);
    }

    public static bool ConvertScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        Scene scene = SceneManager.GetActiveScene();
        RunnerController runner = null;
        GameObject scenery = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == RootName) { Debug.Log("场景已是直线跑道，无需重复转换。"); return false; }
            if (root.name == "NEON RUNWAY - editable scene objects") scenery = root;
            foreach (RunnerController candidate in root.GetComponentsInChildren<RunnerController>(true))
            { if (runner != null) return false; runner = candidate; }
        }
        int groundLayer = LayerMask.NameToLayer("Ground");
        Material road = AssetDatabase.LoadAssetAtPath<Material>("Assets/PulseLook/Road.mat");
        Material cyan = AssetDatabase.LoadAssetAtPath<Material>("Assets/PulseLook/Cyan Light.mat");
        Material pink = AssetDatabase.LoadAssetAtPath<Material>("Assets/PulseLook/Pink Light.mat");
        if (runner == null || scenery == null || groundLayer < 0 || road == null || cyan == null || pink == null)
        { Debug.LogError("直线转换需要原 NeonRunway 场景、一个玩家、Ground Layer 和原霓虹材质。"); return false; }

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Convert to Straight Runway");
        Undo.RecordObject(runner.transform, "Straighten Player");
        runner.transform.rotation = Quaternion.identity;

        // 旧路面保留但隐藏，便于对照；建筑和装饰映射到相同的直线里程。
        foreach (Transform child in scenery.transform)
        {
            if (child.name.StartsWith("Road segment ") || child.name == "Road Collision" || child.name == "Cyan edge" || child.name == "Beat marker")
            { Undo.RecordObject(child.gameObject, "Hide Curved Road"); child.gameObject.SetActive(false); }
            else if (child.name.StartsWith("Background monolith ") || child.name == "Monolith light" ||
                child.name.StartsWith("Gate ") || child.name.StartsWith("Pink road wash") ||
                child.name.StartsWith("Jump obstacle") || child.name.StartsWith("Obstacle luminous face"))
                Straighten(child);
        }
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Road")
            { Undo.RecordObject(root, "Hide Legacy Curved Road Collider"); root.SetActive(false); }
            if (root.name == "Red Spike Practice" || root.name == "Hanging Beat Gate") Straighten(root.transform);
            if (root.name == "Generated Rhythm Obstacles")
                foreach (Transform obstacle in root.transform) Straighten(obstacle);
        }

        // 足够覆盖当前整首歌，并留出远景余量；这不是无限生成系统。
        float duration = runner.musicClock != null && runner.musicClock.music != null ? runner.musicClock.music.length : 300f;
        float start = Mathf.Min(-12f, runner.transform.position.z - 12f);
        float end = Mathf.Max(3000f, runner.transform.position.z + duration * Mathf.Max(8f, runner.forwardSpeed) + 300f);
        GameObject runway = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(runway, scene);
        Undo.RegisterCreatedObjectUndo(runway, "Create Straight Runway");
        float center = (start + end) * .5f, length = end - start;
        GameObject surface = Box(runway.transform, "Straight Ground - seamless collider", new Vector3(0f,-.25f,center), new Vector3(7f,.5f,length), road);
        surface.layer = groundLayer;
        surface.AddComponent<BoxCollider>();
        foreach (float side in new[]{-1f,1f})
            Box(runway.transform, "Continuous cyan edge", new Vector3(side*3.44f,.035f,center), new Vector3(.075f,.055f,length), cyan);
        for (float z = 0f; z < end; z += 8f)
            Box(runway.transform, "Beat marker", new Vector3(2.4f,.035f,z), new Vector3(.06f,.035f,.9f), pink);

        // 将已有建筑整组重复到远处，玩家前进后仍然有霓虹环境。
        for (float section = 180f; section < end; section += 180f)
        {
            foreach (Transform child in scenery.transform)
            {
                if (!child.name.StartsWith("Background monolith ") && child.name != "Monolith light") continue;
                GameObject copy = Object.Instantiate(child.gameObject, runway.transform);
                copy.transform.position += Vector3.forward * section;
            }
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.018f,.028f,.05f);
        RenderSettings.fogStartDistance = 100f;
        RenderSettings.fogEndDistance = 350f;
        foreach (CameraFollow follow in Object.FindObjectsByType<CameraFollow>(FindObjectsSortMode.None))
        {
            if (follow.gameObject.scene != scene || follow.target != runner.transform) continue;
            Undo.RecordObject(follow, "Straight Camera Composition");
            follow.offset = new Vector3(4.5f,4f,-9f);
            follow.lookAhead = 12f;
            Undo.RecordObject(follow.transform, "Snap Straight Camera");
            Camera camera = follow.GetComponent<Camera>();
            if (camera != null) { Undo.RecordObject(camera, "Camera Far Clip"); camera.farClipPlane = 450f; }
            follow.SnapToTarget();
        }
        Physics.SyncTransforms();
        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = runway;
        Debug.Log($"直线跑道已创建：{end-start:F0} 单位。原弯道已隐藏，障碍已拉直；请保存场景后试玩。", runway);
        return true;
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        return obj;
    }

    static void Straighten(Transform target)
    {
        Vector3 position = target.position;
        // 原场景为半径75的圆弧，25单位处开始转弯；仅迁移时需要这段数学。
        float distance = position.z <= 25f ? position.z : 25f + 75f * Mathf.Atan2(position.z-25f,75f-position.x);
        float angle = Mathf.Max(0f,distance-25f)/75f;
        Vector3 point = distance <= 25f ? new Vector3(0,0,distance) : new Vector3(75f*(1-Mathf.Cos(angle)),0,25f+75f*Mathf.Sin(angle));
        Vector3 right = new Vector3(Mathf.Cos(angle),0,-Mathf.Sin(angle));
        float lateral = Vector3.Dot(position-point,right);
        Undo.RecordObject(target, "Straighten Scenery or Obstacle");
        target.SetPositionAndRotation(new Vector3(lateral,position.y,distance), Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0)*target.rotation);
    }
}
