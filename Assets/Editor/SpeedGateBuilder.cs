using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class SpeedGateBuilder
{
    const string Folder="Assets/PulseLook/SpeedGate";
    const string VisualName="Gate Visual v2 - energy accelerator";
    const string Request="Temp/PulseRun_PolishSpeedGates.request";
    static SpeedGateBuilder(){EditorApplication.update+=TryRequestedUpgrade;}

    static void TryRequestedUpgrade()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
        Scene scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/PulseLook/NeonRunway.unity" || scene.isDirty)return;
        if(AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/GateEnergy.shader")==null)return;
        int count=Upgrade();
        if(count>0)EditorSceneManager.SaveScene(scene);
        File.Delete(Request);
    }

    [MenuItem("PulseRun/Upgrade Speed Gate Visuals")]
    public static void UpgradeFromMenu(){Upgrade();if(File.Exists(Request))File.Delete(Request);}

    static int Upgrade()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return 0;
        Scene scene=SceneManager.GetActiveScene();
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Polish Speed Gates");
        int count=0;
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            if(root.name!="Generated Rhythm Obstacles")continue;
            foreach(Transform gate in root.transform)
            {
                bool start=gate.name.StartsWith("X BOOST @");
                if(!start && !gate.name.StartsWith("C NORMAL @"))continue;
                if(gate.Find(VisualName)!=null){count+=RemoveGateLabels(gate);continue;}
                // 保留旧外观，只隐藏；不改变门的位置、时间或谱面。
                foreach(Transform child in gate)
                {Undo.RecordObject(child.gameObject,"Hide old LED frame");child.gameObject.SetActive(false);}
                Transform visual=Build(gate,start);
                Undo.RegisterCreatedObjectUndo(visual.gameObject,"Create energy gate visual");count++;
            }
        }
        Undo.CollapseUndoOperations(undo);
        if(count>0)EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"光门外观已升级：{count} 个。位置和加速逻辑未改变；旧外观隐藏保留，可撤销。");
        return count;
    }

    public static Transform Build(Transform gate,bool start)
    {
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/PulseLook","SpeedGate");
        Material shell=MakeMaterial("Gate Graphite",new Color(.045f,.055f,.085f),Color.black);
        Material alloy=MakeMaterial("Gate Alloy",new Color(.15f,.18f,.23f),Color.black);
        Color color=start?new Color(.45f,.035f,1f):new Color(.025f,.6f,.85f);
        Material light=MakeMaterial(start?"Violet Energy":"Cyan Energy",color,color*2.5f);
        Transform root=new GameObject(VisualName).transform;root.SetParent(gate,false);
        foreach(float side in new[]{-1f,1f})
        {
            Box(root,"Anchored machine base",new Vector3(side*3.85f,.3f,0),new Vector3(.9f,.6f,1.6f),shell);
            Box(root,"Power module",new Vector3(side*3.7f,1.35f,0),new Vector3(.65f,2.2f,1.1f),shell);
            Box(root,"Inset module plate",new Vector3(side*3.7f,1.4f,-.57f),new Vector3(.44f,1.65f,.1f),alloy);
            for(int i=0;i<4;i++)
                Box(root,"Status light",new Vector3(side*3.7f,.85f+i*.28f,-.63f),new Vector3(.22f,.035f,.035f),light);
            for(int i=0;i<3;i++)
                Box(root,"Cooling fin",new Vector3(side*4.1f,1.1f+i*.28f,0),new Vector3(.14f,.08f,.85f),alloy);
            Box(root,"Vertical housing",new Vector3(side*3.6f,3.5f,0),new Vector3(.4f,3.2f,.85f),shell);
            foreach(float z in new[]{-.46f,.46f})
            {
                Beam(root,"Chamfered shoulder",new Vector3(side*3.6f,5.1f,z),new Vector3(side*2.9f,5.9f,z),.28f,shell);
                Beam(root,"Inset energy channel",new Vector3(side*3.42f,.6f,z),new Vector3(side*3.42f,5.04f,z),.055f,light);
                Beam(root,"Shoulder light",new Vector3(side*3.42f,5.04f,z),new Vector3(side*2.8f,5.75f,z),.055f,light);
            }
            Box(root,"Upper mechanical joint",new Vector3(side*3.56f,4.9f,0),new Vector3(.55f,.55f,1f),alloy);
            // 只放在两侧的脉冲片，不挡住玩家中心路线。
            for(int i=0;i<3;i++)
                Box(root,"Emitter segment",new Vector3(side*3.32f,2.6f+i*.65f,0),new Vector3(.08f,.3f,.5f),light);
            for(int i=0;i<3;i++)
            {
                float z=-1.5f+i*.35f;
                Beam(root,"Approach chevron",new Vector3(side*1.6f-.25f,.04f,z-.18f),new Vector3(side*1.6f,.04f,z),.04f,light);
                Beam(root,"Approach chevron",new Vector3(side*1.6f+.25f,.04f,z-.18f),new Vector3(side*1.6f,.04f,z),.04f,light);
            }
        }
        Box(root,"Deep upper cross-member",new Vector3(0,5.92f,0),new Vector3(5.95f,.32f,1.2f),shell);
        foreach(float z in new[]{-.63f,.63f})
            Box(root,"Upper energy channel",new Vector3(0,5.76f,z),new Vector3(5.65f,.055f,.055f),light);
        Shader shader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/GateEnergy.shader");
        if(shader!=null)
        {
            string path=Folder+"/"+(start?"Violet Curtain":"Cyan Curtain")+".mat";
            Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null)
            {
                material=new Material(shader);material.SetColor("_BaseColor",new Color(color.r*1.5f,color.g*1.5f,color.b*1.5f,1));
                material.SetFloat("_FlowSpeed",start?1.5f:.6f);AssetDatabase.CreateAsset(material,path);
            }
            GameObject curtain=GameObject.CreatePrimitive(PrimitiveType.Quad);curtain.name="Transparent flowing energy field";
            curtain.transform.SetParent(root,false);curtain.transform.localPosition=new Vector3(0,2.85f,0);curtain.transform.localScale=new Vector3(6.75f,5.5f,1);
            Object.DestroyImmediate(curtain.GetComponent<Collider>());
            Renderer renderer=curtain.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        return root;
    }

    // 去掉两种门的字和专用标牌，不删门，不改 X/C 标记。
    public static int RemoveGateLabels(Transform gate)
    {
        int count=0;
        foreach(TextMesh label in gate.GetComponentsInChildren<TextMesh>(true))
        {
            string text=label.text.Trim();
            if((text!="BOOST" && text!="NORMAL") || !label.gameObject.activeSelf)continue;
            Undo.RecordObject(label.gameObject,"Hide gate display");label.gameObject.SetActive(false);count++;
            Transform housing=label.transform.parent.Find("Integrated label housing");
            if(housing!=null){Undo.RecordObject(housing.gameObject,"Hide gate label housing");housing.gameObject.SetActive(false);}
        }
        return count;
    }

    static Material MakeMaterial(string name,Color color,Color emission)
    {
        string path=Folder+"/"+name+".mat";
        Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material!=null)return material;
        material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",.55f);material.SetFloat("_Smoothness",.5f);
        if(emission.maxColorComponent>0)
        {
            material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            material.SetColor("_EmissionColor",emission);material.EnableKeyword("_EMISSION");
        }
        material.enableInstancing=true;AssetDatabase.CreateAsset(material,path);return material;
    }
    static GameObject Box(Transform root,string name,Vector3 position,Vector3 size,Material material)
    {
        GameObject obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(root,false);
        obj.transform.localPosition=position;obj.transform.localScale=size;Object.DestroyImmediate(obj.GetComponent<Collider>());
        obj.GetComponent<Renderer>().sharedMaterial=material;return obj;
    }
    static void Beam(Transform root,string name,Vector3 a,Vector3 b,float width,Material material)
    {
        GameObject obj=Box(root,name,(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),material);
        obj.transform.localRotation=Quaternion.LookRotation(b-a);
    }
}
