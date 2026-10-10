using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoadCollisionBuilder
{
    [MenuItem("PulseRun/Build Continuous Road Collider")]
    private static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Road Collision", "请先退出 Play 模式。", "OK");
            return;
        }

        GameObject root = Selection.activeGameObject;
        if (root == null || !root.scene.IsValid())
        {
            EditorUtility.DisplayDialog("Road Collision", "请在 Hierarchy 中选中包含道路切片的 Road 父物体。", "OK");
            return;
        }

        var segments = new SortedDictionary<int, Transform>();
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            const string prefix = "Road segment ";
            if (!child.name.StartsWith(prefix) ||
                !int.TryParse(child.name.Substring(prefix.Length), out int index))
                continue;
            if (segments.ContainsKey(index))
            {
                EditorUtility.DisplayDialog("Road Collision", "道路切片编号重复，请只选择一条道路的父物体。", "OK");
                return;
            }
            segments.Add(index, child);
        }

        int layer = LayerMask.NameToLayer("Ground");
        if (segments.Count < 2 || layer < 0)
        {
            EditorUtility.DisplayDialog("Road Collision", "需要至少两个 Road segment 切片，并且项目中已有 Ground Layer。", "OK");
            return;
        }
        var ordered = segments.ToArray();
        for (int i = 0; i < ordered.Length; i++)
        {
            if ((i > 0 && ordered[i].Key != ordered[i - 1].Key + 1) ||
                ordered[i].Value.GetComponent<MeshFilter>() == null ||
                ordered[i].Value.GetComponent<MeshFilter>().sharedMesh == null ||
                ordered[i].Value.GetComponentInParent<Rigidbody>() != null ||
                Vector3.Dot(ordered[i].Value.up, Vector3.up) < 0.999f)
            {
                EditorUtility.DisplayDialog("Road Collision", "此工具用于原始平坦道路：编号必须连续，切片需有 MeshFilter，不能倾斜或带有父级 Rigidbody。", "OK");
                return;
            }
        }

        Transform existing = root.transform.Find("Road Collision");
        if (existing != null && existing.GetComponent<Rigidbody>() != null)
        {
            EditorUtility.DisplayDialog("Road Collision", "现有 Road Collision 有 Rigidbody，请先移除它再重建。", "OK");
            return;
        }

        // Each shared cross-section has top-left/right and bottom-left/right.
        // Connect the sections directly instead of merging the old closed boxes.
        var vertices = new List<Vector3>();
        AddSection(vertices, root.transform, ordered[0].Value, -0.5f);
        foreach (var item in ordered)
            AddSection(vertices, root.transform, item.Value, 0f);
        AddSection(vertices, root.transform, ordered[ordered.Length - 1].Value, 0.5f);

        var triangles = new List<int>();
        int sections = vertices.Count / 4;
        for (int i = 0; i < sections - 1; i++)
        {
            int a = i * 4, b = a + 4;
            Quad(triangles, a, b, b + 1, a + 1);         // Top, faces up.
            Quad(triangles, a + 2, a + 3, b + 3, b + 2); // Bottom.
            Quad(triangles, a, a + 2, b + 2, b);         // Left.
            Quad(triangles, a + 1, b + 1, b + 3, a + 3); // Right.
        }
        Quad(triangles, 0, 1, 3, 2);
        int end = (sections - 1) * 4;
        Quad(triangles, end, end + 2, end + 3, end + 1);

        var mesh = new Mesh { name = "Continuous Road Collision" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            "Assets/PulseLook/ContinuousRoadCollision.asset");
        AssetDatabase.CreateAsset(mesh, assetPath);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Continuous Road Collider");
        GameObject collision;
        if (existing == null)
        {
            collision = new GameObject("Road Collision");
            Undo.RegisterCreatedObjectUndo(collision, "Create Road Collision");
            Undo.SetTransformParent(collision.transform, root.transform, "Parent Road Collision");
        }
        else
        {
            collision = existing.gameObject;
        }
        Undo.RecordObject(collision.transform, "Reset Collision Transform");
        collision.transform.localPosition = Vector3.zero;
        collision.transform.localRotation = Quaternion.identity;
        collision.transform.localScale = Vector3.one;
        Undo.RecordObject(collision, "Set Ground Layer");
        collision.layer = layer;
        collision.SetActive(true);
        MeshCollider collider = collision.GetComponent<MeshCollider>();
        if (collider == null)
            collider = Undo.AddComponent<MeshCollider>(collision);
        Undo.RecordObject(collider, "Assign Continuous Mesh");
        collider.sharedMesh = null;
        collider.convex = false;
        collider.isTrigger = false;
        collider.enabled = true;
        collider.sharedMesh = mesh;

        foreach (var item in ordered)
        {
            foreach (Collider oldCollider in item.Value.GetComponents<Collider>())
            {
                Undo.RecordObject(oldCollider, "Disable Segment Collider");
                oldCollider.enabled = false;
            }
        }
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = collision;
        Debug.Log($"连续道路碰撞面已创建：{ordered.Length} 个切片，{triangles.Count / 3} 个三角形。请保存场景后测试。", collision);
    }

    private static void AddSection(List<Vector3> vertices, Transform root, Transform segment, float z)
    {
        Bounds bounds = segment.GetComponent<MeshFilter>().sharedMesh.bounds;
        float localZ = bounds.center.z + z * bounds.size.z;
        vertices.Add(root.InverseTransformPoint(segment.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, localZ))));
        vertices.Add(root.InverseTransformPoint(segment.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, localZ))));
        vertices.Add(root.InverseTransformPoint(segment.TransformPoint(new Vector3(bounds.min.x, bounds.min.y, localZ))));
        vertices.Add(root.InverseTransformPoint(segment.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, localZ))));
    }

    private static void Quad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }
}
