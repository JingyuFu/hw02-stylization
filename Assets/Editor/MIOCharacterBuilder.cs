using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Original mesh construction for the user's MIO illustration study.
// Reference: Raphaelle Colin, "Mio: Memories in Orbit - Shii".
// Hidden/back surfaces are interpretations; no game mesh has been extracted.
public static class MIOCharacterBuilder
{
    const string Folder = "Assets/Models/MIOCharacter";
    const string MeshFile = Folder + "/MIO Character Meshes.asset";
    const string PrefabFile = Folder + "/MIO Character.prefab";
    const string SceneFile = "Assets/Scenes/MIO Character Study.unity";
    const string MaterialFolder = "Assets/Materials/MIOCharacter";
    const string Output = "Screenshots/MIO/Character";
    static GameObject root;
    static readonly List<Mesh> meshes = new List<Mesh>();
    static Material purple, magenta, gold, cyan, dark, white, paleGold, deepPurple;
    static int meshIndex;

    [MenuItem("HW02 MIO/3 - Build detailed character and preview")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(PrefabFile) && !EditorUtility.DisplayDialog("Rebuild MIO character", "Rebuild the generated character meshes, materials, prefab and character study scene? Custom edits to those generated assets will be replaced.", "Rebuild", "Cancel")) return;
        BuildAndValidate();
    }

    public static void BuildAndValidate()
    {
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory(MaterialFolder);
        Directory.CreateDirectory(Output);
        AssetDatabase.Refresh();
        meshIndex = 0; meshes.Clear();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupMaterials();
        root = new GameObject("MIO - Shii illustration study");
        BuildHead();
        BuildTorso();
        BuildWings(-1); BuildWings(1);
        BuildLimbs(-1); BuildLimbs(1);
        BuildTendrils(-1); BuildTendrils(1);
        MIOShaderStage.ApplyToCharacter(root);
        AssetDatabase.SaveAssets();
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabFile, InteractionMode.AutomatedAction);
        SetupScene();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), SceneFile);
        AssetDatabase.SaveAssets();
        ValidateGeometry();
        CaptureViews();
        WriteNotes();
        AssetDatabase.Refresh();
        Debug.Log("MIO CHARACTER BUILD AND VALIDATION COMPLETE");
    }

    static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
    static Vector3 V(float x, float y, float z = 0) => new Vector3(x, y, z);
    static Transform Group(string name, Vector3 pivot)
    {
        var g = new GameObject(name).transform;
        g.SetParent(root.transform, false); g.localPosition = pivot; return g;
    }

    static void SetupMaterials()
    {
        purple = Toon("01 - Orchid shell", "D889E9", "BA64D3", "8251A8", .48f, 5);
        magenta = Toon("02 - Rose wing rails", "EA9CEB", "C168D0", "8447A4", .35f, 5);
        gold = Toon("03 - Golden membranes", "FFF58B", "F5E15D", "C1AF79", .40f, 6);
        cyan = Toon("04 - Celadon armor", "CBEBDF", "A3D4CF", "768DB5", .48f, 4);
        deepPurple = Toon("05 - Violet articulated armor", "B185D4", "8E5DB3", "5B3D88", .48f, 4);
        paleGold = Toon("06 - Collar and fittings", "FFF2A8", "E4CF7D", "A59075", .30f, 4);
        dark = Flat("07 - Aubergine seams", "4D286D");
        white = Flat("08 - Ivory luminous filaments", "FFFEEE");
    }

    static Material Toon(string name, string hi, string mid, string shade, float ink, float scale)
    {
        string p = MaterialFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/MIO/MIO Toon.shadergraph");
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, p); }
        m.shader = shader;
        m.SetColor("_Highlight", Hex(hi)); m.SetColor("_Midtone", Hex(mid)); m.SetColor("_Shadow", Hex(shade));
        m.SetFloat("_ThreeBands", 1); m.SetFloat("_ShadowThreshold", .34f); m.SetFloat("_HighlightThreshold", .72f);
        m.SetFloat("_Smoothness", .045f); m.SetFloat("_PatternStrength", ink); m.SetFloat("_ShadowScale", scale);
        m.SetTexture("_ShadowPattern", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/MIO/MIO Hatching.png"));
        EditorUtility.SetDirty(m); return m;
    }

    static Material Flat(string name, string color)
    {
        string p = MaterialFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, p); }
        m.shader = shader; m.SetColor("_BaseColor", Hex(color));
        EditorUtility.SetDirty(m); return m;
    }

    static void BuildHead()
    {
        var head = Group("01 - Segmented helmet and optical lens", V(0, 2.02f));
        Ellipsoid("Helmet continuous shell", head, V(0, 2.02f), V(.49f, .49f, .39f), purple, 64, 32);
        // Seams conform to the shell rather than floating on a flat card.
        ShellSeam("Lower visor arc", head, new[] {V(-.46f,.10f),V(-.42f,-.08f),V(-.26f,-.23f),V(0,-.28f),V(.26f,-.23f),V(.42f,-.08f),V(.46f,.10f)});
        ShellSeam("Left shell petal seam", head, new[] {V(-.18f,.44f),V(-.36f,.33f),V(-.40f,.08f),V(-.31f,-.12f),V(-.26f,-.23f),V(-.38f,-.29f)});
        ShellSeam("Right shell petal seam", head, new[] {V(.18f,.44f),V(.36f,.33f),V(.40f,.08f),V(.31f,-.12f),V(.26f,-.23f),V(.38f,-.29f)});
        ShellSeam("Lower jaw partition", head, new[] {V(-.35f,-.33f),V(-.20f,-.36f),V(0,-.35f),V(.20f,-.36f),V(.35f,-.33f)});
        Ellipsoid("Ivory optical lens", head, V(0,2.22f,-.36f), V(.204f,.177f,.073f), white,48,24);
        Ellipsoid("Lens mounting socket", head, V(0,2.22f,-.304f), V(.232f,.209f,.104f), deepPurple,48,24);
        Ring("Lens inset rim",head,V(0,2.22f,-.383f),.220f,.193f,.012f,dark);
        Ring("Lens rose bezel",head,V(0,2.22f,-.368f),.242f,.214f,.015f,magenta);
        Ellipsoid("Crown pearl socket",head,V(0,2.492f,.008f),V(.050f,.059f,.041f),dark,24,16);
        Ellipsoid("Crown pearl",head,V(0,2.52f,-.015f),V(.030f,.039f,.033f),white,24,16);
        // The reverse is designed as the same shell with a small cable hub.
        Ellipsoid("Rear cable hub recess",head,V(0,1.99f,.38f),V(.24f,.235f,.03f),deepPurple,40,20);
        Ring("Rear hub outer ring",head,V(0,1.99f,.419f),.203f,.203f,.018f,paleGold);
        Ellipsoid("Rear hub celadon lens",head,V(0,1.99f,.424f),V(.126f,.125f,.022f),cyan,32,16);
        for (int i=0;i<3;i++)
        {
            float x=(i-1)*.075f;
            Tube("Rear cooling groove "+i,head,new[]{V(x,1.62f,.27f),V(x,1.76f,.361f)},.008f,dark,6,false);
        }
        for(int s=-1;s<=1;s+=2)
            Ellipsoid((s<0?"Left":"Right")+" hinge",head,V(s*.454f,1.88f,.015f),V(.046f,.10f,.092f),paleGold,24,16);
    }

    static void ShellSeam(string name, Transform parent, Vector3[] points)
    {
        var line = Smooth(points, 8, false);
        for(int i=0;i<line.Length;i++)
        {
            var p=line[i];
            float z=-.39f*Mathf.Sqrt(Mathf.Max(.012f,1-p.x*p.x/(.49f*.49f)-p.y*p.y/(.49f*.49f)))-.009f;
            line[i]=V(p.x,2.02f+p.y,z);
        }
        Tube(name,parent,line,.0105f,dark,6,false);
    }

    static void BuildTorso()
    {
        var body=Group("02 - Collar, thorax and layered breastplate",V(0,1));
        Ellipsoid("Flexible neck",body,V(0,1.54f,.015f),V(.20f,.18f,.15f),deepPurple,32,18);
        Ring("Collar lower edge",body,V(0,1.46f,-.11f),.28f,.11f,.020f,dark);
        Plate("Raised golden gorget",body,new[]{V(-.29f,1.51f),V(-.23f,1.63f),V(0,1.69f),V(.23f,1.63f),V(.29f,1.51f),V(.18f,1.44f),V(0,1.52f),V(-.18f,1.44f)},-.13f,.09f,paleGold,true,.011f);
        Ellipsoid("Thorax core",body,V(0,1.01f,.025f),V(.28f,.47f,.19f),deepPurple,40,24);
        Ellipsoid("Lower abdominal volume",body,V(0,.56f,.025f),V(.135f,.29f,.15f),cyan,32,20);
        Plate("Golden throat badge",body,new[]{V(-.17f,1.46f),V(-.19f,1.26f),V(0,1.16f),V(.19f,1.26f),V(.17f,1.46f)},-.185f,.07f,gold,true,.008f);
        Plate("Celadon breastplate",body,new[]{V(0,1.22f),V(-.225f,1.13f),V(-.17f,.82f),V(-.065f,.62f),V(0,.59f),V(.065f,.62f),V(.17f,.82f),V(.225f,1.13f)},-.245f,.09f,cyan,true,.010f);
        Plate("Abdominal keel",body,new[]{V(-.10f,.80f),V(-.105f,.42f),V(0,.21f),V(.105f,.42f),V(.10f,.80f)},-.10f,.105f,cyan,true,.009f);
        for(int s=-1;s<=1;s+=2)
        {
            string side=s<0?"Left":"Right";
            var shoulder=new[]{V(s*.20f,1.36f),V(s*.40f,1.24f),V(s*.70f,1.03f),V(s*.60f,.94f),V(s*.38f,.96f),V(s*.19f,1.15f)};
            Plate(side+" celadon shoulder petal",body,shoulder,-.02f,.09f,cyan,true,.010f);
            Ellipsoid(side+" shoulder joint",body,V(s*.33f,1.32f,.03f),V(.11f,.115f,.13f),paleGold,24,16);
            Tube(side+" ivory throat filament",body,Smooth(new[]{V(s*.105f,1.45f,-.27f),V(s*.075f,1.34f,-.32f),V(s*.14f,1.20f,-.31f)},9,false),.023f,white,8,true);
            Ring(side+" collar fastening",body,V(s*.112f,1.44f,-.264f),.039f,.047f,.008f,dark);
            Tube(side+" back spine rib",body,Smooth(new[]{V(s*.08f,1.34f,.19f),V(s*.13f,.99f,.216f),V(s*.07f,.48f,.13f)},9,false),.017f,paleGold,8,false);
        }
        Ellipsoid("Back dorsal plate",body,V(0,.99f,.193f),V(.15f,.33f,.05f),purple,32,20);
        Ring("Back thoracic clasp",body,V(0,.97f,.25f),.072f,.11f,.010f,paleGold);
    }

    static void BuildWings(int s)
    {
        string side=s<0?"Left":"Right";
        var group=Group((s<0?"03":"04")+" - "+side+" articulated wings",V(s*.31f,1.38f,.05f));
        var primary=Smooth(new[]{V(.29f,1.42f),V(.82f,1.31f),V(1.68f,1.07f),V(2.67f,.69f),V(3.55f,.25f),V(4.15f,-.075f),V(3.86f,-.075f),V(3.37f,.04f),V(2.69f,.35f),V(1.81f,.77f),V(.78f,1.16f)},5,true);
        var secondary=Smooth(new[]{V(.32f,1.29f),V(.88f,1.10f),V(1.40f,.75f),V(1.86f,.43f),V(1.58f,.34f),V(1.70f,.10f),V(2.13f,-.51f),V(1.91f,-.40f),V(1.41f,-.04f),V(.79f,.49f),V(.35f,1.11f)},5,true);
        primary=Mirror(primary,s); secondary=Mirror(secondary,s);
        Plate(side+" primary wing shell",group,primary,.08f,.085f,magenta,true,.012f);
        Plate(side+" primary golden inlay",group,Inset(primary,.952f),.017f,.029f,gold,false,0);
        Plate(side+" lower wing shell",group,secondary,.13f,.075f,magenta,true,.011f);
        Plate(side+" lower golden inlay",group,Inset(secondary,.946f),.072f,.028f,gold,false,0);
        var rail=Smooth(Mirror(new[]{V(.34f,1.425f,-.005f),V(1.1f,1.255f,.003f),V(2.36f,.80f,.027f),V(3.42f,.285f,.057f),V(4.10f,-.064f,.086f)},s),10,false);
        Tube(side+" sculpted leading edge",group,rail,.024f,magenta,8,true);
        var vein=Smooth(Mirror(new[]{V(.46f,1.30f,-.035f),V(1.20f,1.02f,-.025f),V(2.13f,.64f,-.011f),V(3.15f,.20f,.037f)},s),8,false);
        Tube(side+" main membrane vein",group,vein,.0065f,paleGold,6,true);
        Tube(side+" secondary structural rib",group,Smooth(Mirror(new[]{V(.39f,1.23f,.018f),V(.94f,.65f,.02f),V(1.57f,-.03f,.025f),V(2.06f,-.47f,.04f)},s),8,false),.007f,paleGold,6,true);
        Ellipsoid(side+" wing swivel bearing",group,V(s*.37f,1.37f,.09f),V(.12f,.13f,.11f),deepPurple,28,18);
        Ellipsoid(side+" wing bearing cap",group,V(s*.37f,1.37f,-.029f),V(.070f,.078f,.021f),paleGold,24,16);
        Ring(side+" wing pin engraving",group,V(s*.37f,1.37f,-.053f),.036f,.041f,.0065f,dark);
    }

    static void BuildLimbs(int s)
    {
        string side=s<0?"Left":"Right";
        var group=Group((s<0?"05":"06")+" - "+side+" layered limb",V(s*.20f,.96f));
        float offset=s<0?-.12f:.13f;
        // Long tapered shell with an inset leaf-shaped panel and joint opening.
        var outer=Smooth(new[]{V(s*.19f,1.06f),V(s*.35f,.92f),V(s*.49f,.32f+offset),V(s*.49f,-.22f+offset),V(s*.38f,-.55f+offset),V(s*.28f,-.30f+offset),V(s*.19f,.27f)},6,true);
        Plate(side+" violet upper limb armor",group,outer,-.10f,.18f,deepPurple,true,.011f);
        Spindle(side+" upper limb sculpted core",group,
            Smooth(new[]{V(s*.205f,.98f,.03f),V(s*.33f,.50f+offset,.055f),V(s*.365f,-.47f+offset,.035f)},14,false),
            .137f,.205f,deepPurple);
        var inset=Smooth(new[]{V(s*.235f,.94f),V(s*.35f,.67f),V(s*.41f,.21f+offset),V(s*.37f,-.26f+offset),V(s*.29f,-.09f+offset),V(s*.245f,.40f)},6,true);
        Plate(side+" orchid armor inset",group,inset,-.208f,.018f,purple,false,0);
        Tube(side+" raised armor ridge",group,Smooth(new[]{V(s*.236f,.97f,-.233f),V(s*.29f,.40f,-.236f),V(s*.38f,-.42f+offset,-.211f)},12,false),.009f,dark,6,true);
        float jointY=-.48f+offset;
        Ellipsoid(side+" knee articulation",group,V(s*.36f,jointY,.02f),V(.093f,.12f,.092f),paleGold,28,18);
        Plate(side+" knee diamond socket",group,new[]{V(s*.36f,jointY+.18f),V(s*.46f,jointY),V(s*.36f,jointY-.16f),V(s*.27f,jointY)},-.215f,.035f,dark,false,0);
        Plate(side+" knee diamond inlay",group,new[]{V(s*.36f,jointY+.125f),V(s*.414f,jointY),V(s*.36f,jointY-.102f),V(s*.307f,jointY)},-.242f,.02f,gold,false,0);
        float tipY=s<0?-1.90f:-1.48f;
        float tipX=s<0?-.08f:.50f;
        var leg=Smooth(new[]{V(s*.315f,jointY-.08f),V(s*.268f,jointY-.43f),V(tipX,tipY),V(s*.44f,jointY-.55f),V(s*.427f,jointY-.13f)},6,true);
        Plate(side+" gold tapered greave",group,leg,-.01f,.145f,gold,true,.010f);
        Spindle(side+" greave sculpted reverse",group,
            Smooth(new[]{V(s*.353f,jointY-.13f,.017f),V(s*.36f,(jointY+tipY)*.5f,.012f),V(tipX,tipY,.002f)},14,false),
            .072f,.132f,gold);
        Tube(side+" greave central ridge",group,Smooth(new[]{V(s*.364f,jointY-.12f,-.10f),V(s*.365f,(jointY+tipY)*.5f,-.095f),V(tipX,tipY+.07f,-.071f)},12,false),.007f,paleGold,6,true);
        // Reverse-facing spine makes the leaf armor read as a volume from behind.
        Tube(side+" reverse armor spine",group,Smooth(new[]{V(s*.20f,.96f,.04f),V(s*.36f,.3f+offset,.085f),V(s*.36f,jointY,.085f)},10,false),.024f,purple,8,true);
    }

    static void BuildTendrils(int s)
    {
        string side=s<0?"Left":"Right";
        var group=Group((s<0?"07":"08")+" - "+side+" flowing filaments",V(s*.23f,2.08f,.26f));
        var outer=BezierChain(new[]{
            V(.31f,2.19f,.27f),V(1.30f,2.37f,.35f),V(2.38f,3.86f,.43f),V(3.93f,3.68f,.24f),
            V(5.28f,3.52f,.1f),V(6.05f,2.39f,-.10f),V(5.65f,1.31f,-.08f),
            V(5.25f,.18f,.0f),V(4.46f,.05f,.20f),V(4.56f,.81f,.26f)
        },36);
        var inner=BezierChain(new[]{
            V(.33f,1.98f,.25f),V(1.60f,1.48f,.26f),V(2.40f,2.80f,.32f),V(3.62f,2.69f,.18f),
            V(4.53f,2.64f,.02f),V(5.07f,1.93f,-.06f),V(4.47f,1.55f,.06f),
            V(3.97f,1.25f,.17f),V(3.90f,2.08f,.27f),V(4.40f,2.04f,.28f)
        },32);
        var lower=BezierChain(new[]{
            V(.22f,1.22f,.25f),V(.88f,-.23f,.45f),V(2.22f,-.54f,.33f),V(3.22f,-.83f,.12f),
            V(4.39f,-1.14f,-.10f),V(4.28f,-2.71f,-.20f),V(3.43f,-2.75f,-.10f),
            V(2.68f,-2.78f,.09f),V(2.52f,-1.90f,.26f),V(2.98f,-1.77f,.34f)
        },36);
        var paths=new[]{outer,inner,lower};
        var labels=new[]{"upper arch","inner spiral","lower curl"};
        for(int k=0;k<paths.Length;k++)
        {
            var main=Mirror(paths[k],s);
            for(int i=0;i<main.Length;i++)
            {
                float t=(float)i/(main.Length-1);
                main[i].z += s*.30f*Mathf.Sin(t*2*Mathf.PI) + .22f*Mathf.Sin(t*Mathf.PI);
            }
            Tube(side+" "+labels[k]+" - main filament",group,main,.024f,white,8,true);
            var companion=new Vector3[main.Length];
            for(int i=0;i<main.Length;i++)
            {
                float t=(float)i/(main.Length-1);
                var tangent=main[Mathf.Min(main.Length-1,i+1)]-main[Mathf.Max(0,i-1)];
                var n=Vector3.Cross(tangent.normalized,Vector3.forward);
                companion[i]=main[i]+n*(.061f*Mathf.Sin(Mathf.PI*t)) + V(0,0,.026f*Mathf.Sin(Mathf.PI*t));
            }
            Tube(side+" "+labels[k]+" - fine companion",group,companion,.013f,white,6,true);
        }
        for(int k=0;k<3;k++)
            Ellipsoid(side+" filament root socket "+k,group,V(s*(.17f+k*.06f),2.01f+k*.067f,.295f),V(.036f,.044f,.043f),paleGold,20,12);
    }

    static Vector3[] Mirror(Vector3[] points,int s) => points.Select(p=>V(p.x*s,p.y,p.z)).ToArray();
    static Vector3[] Inset(Vector3[] points,float amount)
    {
        Vector3 center=points.Aggregate(Vector3.zero,(sum,p)=>sum+p)/points.Length;
        return points.Select(p=>Vector3.Lerp(center,p,amount)).ToArray();
    }

    static Vector3[] Smooth(Vector3[] p,int steps,bool closed)
    {
        var result=new List<Vector3>();
        int count=closed?p.Length:p.Length-1;
        for(int i=0;i<count;i++)
        {
            Vector3 p0=p[closed?(i-1+p.Length)%p.Length:Mathf.Max(0,i-1)];
            Vector3 p1=p[i],p2=p[(i+1)%p.Length];
            Vector3 p3=p[closed?(i+2)%p.Length:Mathf.Min(p.Length-1,i+2)];
            for(int j=0;j<steps;j++)
            {
                float t=(float)j/steps,t2=t*t,t3=t2*t;
                result.Add(.5f*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t2+(-p0+3*p1-3*p2+p3)*t3));
            }
        }
        if(!closed)result.Add(p[p.Length-1]);
        return result.ToArray();
    }

    static Vector3[] BezierChain(Vector3[] controls,int samples)
    {
        var result=new List<Vector3>();
        for(int seg=0;seg<(controls.Length-1)/3;seg++)
        {
            int o=seg*3;
            for(int i=0;i<samples;i++)
            {
                float t=(float)i/samples,a=1-t;
                result.Add(a*a*a*controls[o]+3*a*a*t*controls[o+1]+3*a*t*t*controls[o+2]+t*t*t*controls[o+3]);
            }
        }
        result.Add(controls[controls.Length-1]); return result.ToArray();
    }

    static void Ellipsoid(string name,Transform parent,Vector3 center,Vector3 radii,Material mat,int columns,int rows)
    {
        var verts=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        for(int j=0;j<=rows;j++)for(int i=0;i<=columns;i++)
        {
            float u=(float)i/columns,v=(float)j/rows;
            float a=u*2*Mathf.PI,b=(v-.5f)*Mathf.PI;
            var n=V(Mathf.Cos(b)*Mathf.Cos(a),Mathf.Sin(b),Mathf.Cos(b)*Mathf.Sin(a));
            verts.Add(center+Vector3.Scale(n,radii));
            normals.Add(V(n.x/radii.x,n.y/radii.y,n.z/radii.z).normalized);uv.Add(new Vector2(u,v));
        }
        for(int j=0;j<rows;j++)for(int i=0;i<columns;i++)
        {
            int a=j*(columns+1)+i,b=a+columns+1;
            if(j>0)Face(tris,verts,normals,a,b,a+1);
            if(j<rows-1)Face(tris,verts,normals,a+1,b,b+1);
        }
        SaveMeshObject(name,parent,verts,uv,tris,mat,normals);
    }

    static void Ring(string name,Transform parent,Vector3 center,float rx,float ry,float radius,Material mat)
    {
        var points=new Vector3[65];
        for(int i=0;i<65;i++){float a=i*2*Mathf.PI/64;points[i]=center+V(rx*Mathf.Cos(a),ry*Mathf.Sin(a));}
        Tube(name,parent,points,radius,mat,8,false,true);
    }

    static void Spindle(string name,Transform parent,Vector3[] line,float width,float depth,Material mat)
    {
        const int sides=24;
        var verts=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        for(int i=0;i<line.Length;i++)
        {
            float t=(float)i/(line.Length-1);
            float scale=Mathf.Max(.002f,Mathf.Pow(Mathf.Max(0,Mathf.Sin(Mathf.PI*t)),.68f));
            var tangent=(line[Mathf.Min(line.Length-1,i+1)]-line[Mathf.Max(0,i-1)]).normalized;
            var across=Vector3.Cross(tangent,Vector3.forward).normalized;
            var back=Vector3.Cross(tangent,across).normalized;
            for(int j=0;j<=sides;j++)
            {
                float a=j*2*Mathf.PI/sides,c=Mathf.Cos(a),sn=Mathf.Sin(a);
                verts.Add(line[i]+across*(width*scale*c)+back*(depth*scale*sn));
                normals.Add((across*(c/width)+back*(sn/depth)).normalized);uv.Add(new Vector2((float)j/sides,t));
            }
        }
        for(int i=0;i<line.Length-1;i++)for(int j=0;j<sides;j++)
        {
            int a=i*(sides+1)+j,b=a+sides+1;
            Face(tris,verts,normals,a,b,a+1);Face(tris,verts,normals,a+1,b,b+1);
        }
        for(int end=0;end<2;end++)
        {
            int row=end==0?0:line.Length-1;
            var norm=(end==0?line[0]-line[1]:line[row]-line[row-1]).normalized;
            int c=verts.Count;verts.Add(line[row]);normals.Add(norm);uv.Add(new Vector2(.5f,end));
            for(int j=0;j<sides;j++)Face(tris,verts,normals,c,row*(sides+1)+j,row*(sides+1)+j+1);
        }
        SaveMeshObject(name,parent,verts,uv,tris,mat,normals);
    }

    static void Tube(string name,Transform parent,Vector3[] line,float radius,Material mat,int sides,bool taper,bool closed=false)
    {
        var verts=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        float length=0;
        for(int i=0;i<line.Length;i++)
        {
            if(i>0)length+=Vector3.Distance(line[i],line[i-1]);
            int prev=closed?(i==0?line.Length-2:i-1):Mathf.Max(0,i-1);
            int next=closed?(i==line.Length-1?1:i+1):Mathf.Min(line.Length-1,i+1);
            var tangent=(line[next]-line[prev]).normalized;
            var n=Vector3.Cross(tangent,Vector3.forward).normalized;
            if(n.sqrMagnitude<.1f)n=Vector3.Cross(tangent,Vector3.up).normalized;
            var b=Vector3.Cross(tangent,n).normalized;
            float t=(float)i/(line.Length-1),r=radius*(taper?Mathf.Lerp(1,.10f,Mathf.Pow(t,5)):1);
            for(int j=0;j<=sides;j++)
            {
                float a=j*2*Mathf.PI/sides;
                var norm=n*Mathf.Cos(a)+b*Mathf.Sin(a);
                verts.Add(line[i]+norm*r);normals.Add(norm);uv.Add(new Vector2((float)j/sides,length));
            }
        }
        for(int i=0;i<line.Length-1;i++)for(int j=0;j<sides;j++)
        {
            int a=i*(sides+1)+j,b=a+sides+1;
            Face(tris,verts,normals,a,b,a+1);Face(tris,verts,normals,a+1,b,b+1);
        }
        if(!closed)
        {
            for(int end=0;end<2;end++)
            {
                int ring=end==0?0:(line.Length-1)*(sides+1);
                var normal=(end==0?line[0]-line[1]:line[line.Length-1]-line[line.Length-2]).normalized;
                int c=verts.Count;verts.Add(end==0?line[0]:line[line.Length-1]);normals.Add(normal);uv.Add(new Vector2(.5f,.5f));
                for(int j=0;j<sides;j++)Face(tris,verts,normals,c,ring+j,ring+j+1);
            }
        }
        SaveMeshObject(name,parent,verts,uv,tris,mat,normals);
    }

    static void Face(List<int> tris,List<Vector3> verts,List<Vector3> normals,int a,int b,int c)
    {
        var cross=Vector3.Cross(verts[b]-verts[a],verts[c]-verts[a]);
        if(cross.sqrMagnitude<1e-15f)return;
        if(Vector3.Dot(cross,normals[a]+normals[b]+normals[c])<0){int temp=b;b=c;c=temp;}
        tris.Add(a);tris.Add(b);tris.Add(c);
    }

    static float SignedArea(Vector3[] p)
    { float a=0;for(int i=0;i<p.Length;i++){var b=p[(i+1)%p.Length];a+=p[i].x*b.y-b.x*p[i].y;}return a*.5f; }

    static List<int> Triangulate(Vector3[] p)
    {
        var ring=Enumerable.Range(0,p.Length).ToList();var result=new List<int>();int guard=p.Length*p.Length;
        while(ring.Count>3&&guard-->0)
        {
            bool found=false;
            for(int j=0;j<ring.Count;j++)
            {
                int a=ring[(j-1+ring.Count)%ring.Count],b=ring[j],c=ring[(j+1)%ring.Count];
                float cross=Cross2(p[b]-p[a],p[c]-p[a]);if(cross<=1e-8f)continue;
                bool inside=false;
                for(int k=0;k<ring.Count;k++)
                {
                    int q=ring[k];if(q==a||q==b||q==c)continue;
                    if(Cross2(p[b]-p[a],p[q]-p[a])>=-1e-8f&&Cross2(p[c]-p[b],p[q]-p[b])>=-1e-8f&&Cross2(p[a]-p[c],p[q]-p[c])>=-1e-8f){inside=true;break;}
                }
                if(inside)continue;
                result.Add(a);result.Add(b);result.Add(c);ring.RemoveAt(j);found=true;break;
            }
            if(!found)throw new InvalidOperationException("A plate contour is self-intersecting or degenerate.");
        }
        if(ring.Count==3)result.AddRange(ring);return result;
    }
    static float Cross2(Vector3 a,Vector3 b)=>a.x*b.y-a.y*b.x;

    static void Plate(string name,Transform parent,Vector3[] outline,float z,float thickness,Material mat,bool trim,float trimRadius)
    {
        var p=(Vector3[])outline.Clone();if(SignedArea(p)<0)Array.Reverse(p);
        var inset=Inset(p,.975f);int n=p.Length;
        var verts=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        float minX=p.Min(v=>v.x),maxX=p.Max(v=>v.x),minY=p.Min(v=>v.y),maxY=p.Max(v=>v.y);
        for(int layer=0;layer<4;layer++)for(int i=0;i<n;i++)
        {
            var q=(layer==0||layer==3)?inset[i]:p[i];
            float depth=layer==0?-thickness*.5f:layer==1?-thickness*.16f:layer==2?thickness*.16f:thickness*.5f;
            // A small spanwise sweep gives wings thickness and a gentle rearward arc.
            float sweep=Mathf.Abs(q.x)>1?.007f*q.x*q.x:0;
            verts.Add(V(q.x,q.y,z+depth+sweep));
            uv.Add(new Vector2((q.x-minX)/Mathf.Max(.001f,maxX-minX),(q.y-minY)/Mathf.Max(.001f,maxY-minY)));
        }
        var face=Triangulate(inset);
        for(int i=0;i<face.Count;i+=3)
        {
            tris.Add(face[i]);tris.Add(face[i+2]);tris.Add(face[i+1]);
            tris.Add(3*n+face[i]);tris.Add(3*n+face[i+1]);tris.Add(3*n+face[i+2]);
        }
        for(int layer=0;layer<3;layer++)for(int i=0;i<n;i++)
        {
            int j=(i+1)%n,a=layer*n+i,b=layer*n+j,c=(layer+1)*n+j,d=(layer+1)*n+i;
            tris.Add(a);tris.Add(b);tris.Add(c);tris.Add(a);tris.Add(c);tris.Add(d);
        }
        SaveMeshObject(name,parent,verts,uv,tris,mat,null);
        if(trim)
        {
            var border=new Vector3[n+1];
            for(int i=0;i<=n;i++){var q=p[i%n];border[i]=V(q.x,q.y,z-thickness*.18f+(Mathf.Abs(q.x)>1?.007f*q.x*q.x:0));}
            Tube(name+" - edge binding",parent,border,trimRadius,dark,6,false,true);
        }
    }

    static void SaveMeshObject(string name,Transform parent,List<Vector3> verts,List<Vector2> uv,List<int> tris,Material material,List<Vector3> normals)
    {
        meshIndex++;
        string assetName=name;
        var bounds=new Bounds(verts[0],Vector3.zero);foreach(var v in verts)bounds.Encapsulate(v);
        Vector3 pivot=bounds.center;
        var mesh=new Mesh{name=assetName,indexFormat=verts.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
        mesh.SetVertices(verts.Select(v=>v-pivot).ToList());mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);
        if(normals!=null)mesh.SetNormals(normals);else mesh.RecalculateNormals();
        mesh.RecalculateBounds();mesh.RecalculateTangents();
        if(!File.Exists(MeshFile))AssetDatabase.CreateAsset(mesh,MeshFile);
        else
        {
            var old=AssetDatabase.LoadAllAssetsAtPath(MeshFile).OfType<Mesh>().FirstOrDefault(m=>m.name==assetName);
            if(old!=null){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;EditorUtility.SetDirty(mesh);}
            else AssetDatabase.AddObjectToAsset(mesh,MeshFile);
        }
        meshes.Add(mesh);
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=pivot;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial=material;
    }

    static void SetupScene()
    {
        var cam=new GameObject("Main Camera").AddComponent<Camera>();cam.tag="MainCamera";
        cam.orthographic=true;cam.orthographicSize=4.02f;cam.nearClipPlane=.1f;cam.farClipPlane=60;
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Hex("BDD5C5");cam.allowHDR=false;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var key=new GameObject("Key Light").AddComponent<Light>();key.type=LightType.Directional;key.intensity=.95f;
        key.shadows=LightShadows.Soft;key.shadowBias=.08f;key.shadowNormalBias=.18f;
        var fill=new GameObject("Fill Light").AddComponent<Light>();fill.type=LightType.Point;fill.intensity=1.7f;fill.range=9;fill.shadows=LightShadows.None;
        SetView(0,0,4.02f);
        RenderSettings.skybox=null;RenderSettings.fog=false;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;
    }

    static void SetView(float yaw,float elevation,float size)
    {
        var q=Quaternion.Euler(elevation,yaw,0);var target=V(0,.45f,0);
        Camera.main.transform.position=target+q*V(0,0,-15);Camera.main.transform.LookAt(target);Camera.main.orthographicSize=size;
        GameObject.Find("Key Light").transform.rotation=q*Quaternion.LookRotation(V(.40f,-.70f,.65f));
        GameObject.Find("Fill Light").transform.position=target+q*V(2.8f,1.2f,-3.3f);
    }

    [MenuItem("HW02 MIO/4 - Capture character views")]
    public static void CaptureViews()
    {
        SetView(0,0,4.02f);Capture(Output+"/01-front.png",1600,1000);
        SetView(-38,6,4.25f);Capture(Output+"/02-three-quarter.png",1600,1000);
        SetView(90,0,3.95f);Capture(Output+"/03-side.png",1200,1000);
        SetView(180,0,4.02f);Capture(Output+"/04-back.png",1600,1000);
        SetView(-12,5,2.25f);Capture(Output+"/05-armor-detail.png",1200,1200);
        SetView(0,0,4.02f);
    }

    static void Capture(string path,int width,int height)
    {
        var camera=Camera.main;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=4};
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
    }

    static void ValidateGeometry()
    {
        int vertices=0,triangles=0;
        foreach(var mesh in meshes)
        {
            var v=mesh.vertices;var uv=mesh.uv;var normal=mesh.normals;var t=mesh.triangles;
            if(uv.Length!=v.Length||normal.Length!=v.Length)throw new InvalidOperationException("Missing UVs/normals: "+mesh.name);
            if(v.Any(p=>float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z)))throw new InvalidOperationException("Non-finite vertex: "+mesh.name);
            for(int i=0;i<t.Length;i+=3)
                if(Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).sqrMagnitude<1e-16f)throw new InvalidOperationException("Degenerate triangle: "+mesh.name);
            vertices+=v.Length;triangles+=t.Length/3;
        }
        if(triangles>150000)throw new InvalidOperationException("Character exceeds 150k triangle budget.");
        if(root.GetComponentsInChildren<MeshRenderer>().Any(r=>r.sharedMaterial==null))throw new InvalidOperationException("Unassigned material.");
        var shader=purple.shader;
        var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
        if(errors.Length>0)throw new InvalidOperationException(string.Join("\n",errors.Select(e=>e.message)));
        string report="MIO MODEL VALIDATED\nMeshes: "+meshes.Count+"\nVertices: "+vertices+"\nTriangles: "+triangles+"\nUV0 and normals: present on all meshes\nNon-finite vertices: none\nDegenerate triangles: none\nMaterials: assigned\n";
        File.WriteAllText(Output+"/geometry-validation.txt",report);Debug.Log(report);
    }

    static void WriteNotes()
    {
        File.WriteAllText(Folder+"/Model Notes.txt",
            "MIO character - illustration study\n\nReference artwork: Raphaelle Colin, Mio: Memories in Orbit - Shii\nhttps://raphaelle_colin.artstation.com/projects/BkbRor\nLocal reference: raphaelle-colin-shii-5.webp\nUnderlying character/IP: Douze Dixiemes / MIO: Memories in Orbit.\n\nThese are newly constructed study meshes, not extracted game assets. Side and rear surfaces are artistic interpretations of the supplied front illustration. No redistribution license for the original character or artwork is asserted.\n\nThe prefab contains separate helmet, torso, wing, limb and filament groups. All meshes have UV0 coordinates, vertex normals and tangents. Wing groups pivot near their shoulder joints. Meshes are stored as sub-assets in MIO Character Meshes.asset.\n\nScene: Assets/Scenes/MIO Character Study.unity\nRendering uses the improved toon shader with soft rim lighting. The 12 long filaments use the derived vertex-animation shader; open Assets/Scenes/MIO Shader Study.unity and press Play to see them move. Mesh edge bindings represent physical seams; they do not replace the assignment's future depth/normal post-process outline.\n\nSource: Assets/Editor/MIOCharacterBuilder.cs\nGeneration uses no paid service.\n");
    }
}
