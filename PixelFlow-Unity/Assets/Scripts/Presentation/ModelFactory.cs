using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PixelFlow.Presentation
{
    public sealed class UfoVisual
    {
        public Transform Root { get; private set; }
        private readonly Transform numberRoot;
        private readonly TextMesh number;
        private readonly TextMesh[] outlines;
        private readonly Transform weapon;
        private readonly Material body, lower, rim;
        private int ammo = -1;

        internal UfoVisual(Transform root, Transform numberRoot, TextMesh number, TextMesh[] outlines, Transform weapon, Material body, Material lower, Material rim)
        {
            Root = root; this.numberRoot = numberRoot; this.number = number; this.outlines = outlines;
            this.weapon = weapon; this.body = body; this.lower = lower; this.rim = rim;
        }

        public void SetAmmo(int value)
        {
            value = Mathf.Max(0, value);
            if (ammo == value) return;
            ammo = value; string text = value.ToString(); number.text = text;
            foreach (TextMesh ink in outlines) ink.text = text;
        }

        public void SetColor(Color color)
        {
            body.color = color;
            lower.color = Color.Lerp(color, Color.black, .18f);
            rim.color = Color.Lerp(color, Color.black, .40f);
        }

        public void SetWeapon(float extension, Vector3 direction)
        {
            extension = Mathf.Clamp01(extension);
            weapon.gameObject.SetActive(extension > .01f);
            if (extension <= .01f) return;
            Vector3 local = Root.InverseTransformDirection(direction);
            if (local.sqrMagnitude > .001f)
            {
                Vector3 forward = local.normalized;
                weapon.localRotation = Quaternion.LookRotation(forward, Mathf.Abs(forward.y) > .96f ? Vector3.forward : Vector3.up);
            }
            weapon.localScale = new Vector3(1, 1, Mathf.Lerp(.25f, 1, extension));
        }

        /// <summary>Keep the number on the camera-facing side of the solid dome.</summary>
        public void FaceCamera(Camera camera)
        {
            if (!camera || !numberRoot) return;
            Vector3 local = Root.InverseTransformDirection(camera.transform.position - Root.position);
            local.y = 0;
            if (local.sqrMagnitude < .001f) local = Vector3.back;
            local.Normalize();
            numberRoot.localPosition = new Vector3(local.x * .58f, .91f, local.z * .58f);
            numberRoot.rotation = Quaternion.LookRotation(numberRoot.position - camera.transform.position, camera.transform.up);
            Bounds bounds = number.GetComponent<Renderer>().localBounds;
            if (bounds.size.y < .0001f) return;
            float fit = .43f / bounds.size.y;
            if (bounds.size.x * fit > 1.05f) fit = 1.05f / bounds.size.x;
            numberRoot.localScale = Vector3.one * fit;
            float offset = bounds.size.y * .055f;
            Vector2[] directions = { new Vector2(-1,-1), new Vector2(0,-1), new Vector2(1,-1), new Vector2(-1,0), new Vector2(1,0), new Vector2(-1,1), new Vector2(0,1), new Vector2(1,1) };
            for (int i = 0; i < outlines.Length; i++) outlines[i].transform.localPosition = new Vector3(directions[i].x * offset, directions[i].y * offset, .002f);
        }
    }

    public sealed class BinVisual
    {
        public Transform Root { get; private set; }
        public Transform[] StoredGear { get; private set; }
        public Transform ExitPoint { get; private set; }
        internal BinVisual(Transform root, Transform[] storedGear, Transform exitPoint) { Root = root; StoredGear = storedGear; ExitPoint = exitPoint; }
        public void SetStock(int available)
        {
            available = Mathf.Clamp(available, 0, StoredGear.Length);
            for (int i = 0; i < StoredGear.Length; i++) StoredGear[i].gameObject.SetActive(i < available);
        }
        public Vector3 GetStoredPosition(int slot) { return StoredGear[Mathf.Clamp(slot, 0, StoredGear.Length - 1)].position; }
    }

    /// <summary>All game models are actual closed meshes; no image replaces their depth.</summary>
    public static class ModelFactory
    {
        public const float GearMountHeight = 1.5f;
        private static readonly Dictionary<string, Material> SharedMaterials = new Dictionary<string, Material>();
        private static Font font;
        private static Mesh saucer, dome, shoulder, ring;

        public static UfoVisual CreateUfo(Transform parent, Color color)
        {
            Transform root = Group("UFO", parent);
            Material body = NewMaterial("Single-colour hull", color, .6f, .02f);
            Material underside = NewMaterial("Single-colour lower hull", Color.Lerp(color, Color.black, .18f), .45f, .02f);
            Material edge = NewMaterial("Single-colour rim", Color.Lerp(color, Color.black, .4f), .45f, .02f);
            if (!saucer) saucer = MeshFactory.Lathe(new[] {
                new Vector2(0,.06f),new Vector2(.52f,.015f),new Vector2(1.02f,.08f),new Vector2(1.22f,.19f),new Vector2(1.26f,.29f),
                new Vector2(1.20f,.40f),new Vector2(.78f,.46f),new Vector2(0,.46f) });
            Part("Solid saucer hull", root, saucer, body, Vector3.zero, Vector3.one, true);
            if (!dome)
            {
                var profile = new List<Vector2> { new Vector2(0, 0), new Vector2(.65f, 0) };
                for (int i = 1; i <= 12; i++) { float a = i * Mathf.PI * .5f / 12; profile.Add(new Vector2(Mathf.Cos(a) * .65f, Mathf.Sin(a) * .65f)); }
                dome = MeshFactory.Lathe(profile.ToArray());
            }
            Part("Solid colour dome", root, dome, body, new Vector3(0, .43f, 0), Vector3.one, true);
            if (!ring) ring = MeshFactory.Torus(1.22f, .035f, 48, 8);
            Part("Raised colour rim", root, ring, edge, new Vector3(0,.27f,0), Vector3.one, false);
            Transform bottom = Sphere("Shallow lower hull", root, underside, new Vector3(0,.12f,0), new Vector3(2.0f,.32f,2.0f));
            bottom.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;

            Transform numberRoot = Group("Number on dome", root);
            TextMesh white = Text("Energy", numberRoot, Color.white, 11);
            TextMesh[] strokes = new TextMesh[8];
            for (int i = 0; i < strokes.Length; i++) strokes[i] = Text("Number outline " + (i+1), numberRoot, new Color(.10f,.07f,.11f), 10);

            Transform cannon = Group("Retracting cannon", root); cannon.localPosition = new Vector3(0, .55f, 0);
            Rod("Cannon tube", cannon, new Vector3(0,0,.61f), new Vector3(0,0,1.17f), .095f, edge);
            Mesh muzzle = MeshFactory.Torus(.105f,.022f,20,8);
            Transform muzzlePart = Part("Muzzle", cannon, muzzle, edge, new Vector3(0,0,1.18f), Vector3.one, false);
            muzzlePart.localRotation = Quaternion.Euler(90,0,0);
            cannon.gameObject.SetActive(false);
            var result = new UfoVisual(root, numberRoot, white, strokes, cannon, body, underside, edge);
            result.SetAmmo(0);
            return result;
        }

        public static Transform CreateGear(Transform parent)
        {
            Transform root = Group("Aircraft landing gear", parent);
            Material rubber = Shared("Rubber", new Color(.065f,.085f,.10f), .18f, 0);
            Material shoulderMaterial = Shared("Rubber sidewall", new Color(.035f,.045f,.055f), .28f, 0);
            Material steel = Shared("Silver alloy", new Color(.70f,.78f,.80f), .72f, .65f);
            Material steelDark = Shared("Steel fork", new Color(.24f,.31f,.35f), .55f, .55f);
            Material hub = Shared("Wheel hub", new Color(.83f,.87f,.83f), .68f, .45f);
            if (!shoulder) shoulder = MeshFactory.Torus(.25f,.09f,28,10);
            foreach (float z in new[] { -.33f, .33f })
            {
                Transform wheel = Group(z < 0 ? "Rear aircraft tyre" : "Front aircraft tyre", root);
                wheel.localPosition = new Vector3(0,.35f,z);
                Transform barrel = Cylinder("Thick rubber tyre", wheel, rubber, Vector3.zero, .335f, .18f);
                barrel.localRotation = Quaternion.Euler(90,0,0);
                foreach (float side in new[] { -1f,1f })
                {
                    Transform curve = Part("Rounded tyre shoulder", wheel, shoulder, shoulderMaterial, new Vector3(0,0,side*.078f), Vector3.one, false);
                    curve.localRotation = Quaternion.Euler(90,0,0);
                    Transform hubFace = Cylinder("Recessed alloy hub", wheel, steelDark, new Vector3(0,0,side*.115f), .17f,.025f); hubFace.localRotation=Quaternion.Euler(90,0,0);
                    Transform hubCap = Cylinder("Silver wheel hub",wheel,hub,new Vector3(0,0,side*.135f),.106f,.025f);hubCap.localRotation=Quaternion.Euler(90,0,0);
                    Transform bolt = Cylinder("Axle cap",wheel,steelDark,new Vector3(0,0,side*.154f),.034f,.032f);bolt.localRotation=Quaternion.Euler(90,0,0);
                }
            }
            Rod("Cross axle", root, new Vector3(0,.35f,-.47f),new Vector3(0,.35f,.47f),.065f,steelDark);
            Rod("Rear fork",root,new Vector3(0,.86f,0),new Vector3(0,.35f,-.29f),.077f,steel);
            Rod("Front fork",root,new Vector3(0,.86f,0),new Vector3(0,.35f,.29f),.077f,steel);
            Rod("Piston",root,new Vector3(0,.68f,0),new Vector3(0,1.30f,0),.095f,steel);
            Rod("Main telescopic leg",root,new Vector3(0,1.04f,0),new Vector3(0,1.50f,0),.14f,steelDark);
            Rod("Upper chrome collar",root,new Vector3(0,1.22f,0),new Vector3(0,1.49f,0),.115f,steel);
            Rod("Torque link upper",root,new Vector3(0,.91f,.04f),new Vector3(.19f,.56f,.04f),.035f,steelDark);
            Rod("Torque link lower",root,new Vector3(.19f,.56f,.04f),new Vector3(0,.39f,.04f),.035f,steelDark);
            return root;
        }

        public static GameObject CreateBlock(Transform parent, Color color, float size = 1)
        {
            Vector3 dimensions = new Vector3(.95f,.86f,.46f) * size;
            Transform block = Part("Solid candy block", parent, MeshFactory.BeveledBox(dimensions,.055f*size), Shared("Block " + ColorUtility.ToHtmlStringRGB(color), color,.57f,.015f), new Vector3(0,dimensions.y*.5f,0),Vector3.one,false);
            // A thin dark expanded silhouette keeps each small cube readable.
            Transform line = Part("Block dark edge",block,MeshFactory.Inverted(block.GetComponent<MeshFilter>().sharedMesh),Shared("Block outline "+ColorUtility.ToHtmlStringRGB(color),Color.Lerp(color,Color.black,.58f),.25f,0),Vector3.zero,Vector3.one*1.025f,false);
            line.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            return block.gameObject;
        }

        public static BinVisual CreateBox(Transform parent, float width=4.2f, float depth=2.5f, float height=2.1f)
        {
            Transform root=Group("Deep landing gear magazine",parent);
            width=Mathf.Max(3.3f,width);depth=Mathf.Max(1.6f,depth);height=Mathf.Max(1.7f,height);
            Material gold=Shared("Magazine gold",new Color(.86f,.51f,.18f),.45f,.15f);
            Material lightGold=Shared("Magazine bevel",new Color(1,.72f,.33f),.53f,.12f);
            Material inner=Shared("Magazine inner wall",new Color(.44f,.24f,.10f),.25f,0);
            Material floor=Shared("Magazine dark inner floor",new Color(.11f,.085f,.065f),.16f,0);
            Material charcoal=Shared("Magazine charcoal housing",new Color(.20f,.17f,.15f),.25f,0);
            Material charcoalTop=Shared("Magazine top edge",new Color(.38f,.32f,.25f),.32f,.03f);
            Box("Thick base",root,new Vector3(width+.18f,.28f,depth+.20f),new Vector3(0,.06f,0),gold,.07f);
            Box("Recessed inner floor",root,new Vector3(width-.20f,.09f,depth-.18f),new Vector3(0,.225f,0),floor,.025f);
            Box("Upright brown back",root,new Vector3(width,height,.24f),new Vector3(0,height*.5f,depth*.5f),inner,.04f);
            Box("Back gold top",root,new Vector3(width+.12f,.17f,.31f),new Vector3(0,height,depth*.5f),lightGold,.045f);
            Box("Right thick gold wall",root,new Vector3(.26f,height*.86f,depth+.18f),new Vector3(width*.5f,height*.43f,0),gold,.05f);
            Box("Right gold top",root,new Vector3(.29f,.12f,depth+.22f),new Vector3(width*.5f,height*.86f,0),lightGold,.04f);
            Box("Low front gold fascia",root,new Vector3(width+.18f,.39f,.26f),new Vector3(0,.28f,-depth*.5f),gold,.06f);
            Box("Front rim highlight",root,new Vector3(width+.21f,.10f,.29f),new Vector3(0,.49f,-depth*.5f+.015f),lightGold,.035f);
            Box("Tall solid left partition",root,new Vector3(.56f,height*1.28f,depth+.60f),new Vector3(-width*.5f-.18f,height*.64f,.06f),charcoal,.06f);
            Box("Left partition top face",root,new Vector3(.58f,.14f,depth+.62f),new Vector3(-width*.5f-.18f,height*1.28f,.06f),charcoalTop,.04f);
            var stored=new Transform[5];
            float spacing=(width-.75f)/5f;
            for(int i=0;i<stored.Length;i++)
            {
                Transform gear=CreateGear(root);gear.name="Stored aircraft gear "+(i+1);
                gear.localPosition=new Vector3(-width*.5f+.40f+spacing*i,.28f,.04f);
                gear.localRotation=Quaternion.Euler(14,22,-12);gear.localScale=Vector3.one*.80f;stored[i]=gear;
            }
            Transform exit=Group("Boarding mouth",root);exit.localPosition=new Vector3(width*.25f,.28f,-depth*.5f-.50f);
            return new BinVisual(root,stored,exit);
        }

        private static Transform Group(string name, Transform parent)
        {
            GameObject go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;
        }
        private static Material NewMaterial(string name,Color color,float smoothness,float metallic)
        {
            Shader shader=Shader.Find("Standard");if(!shader)shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)shader=Shader.Find("Unlit/Color");
            Material material=new Material(shader){name=name,color=color};
            if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",smoothness);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smoothness);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",metallic);
            return material;
        }
        private static Material Shared(string name,Color color,float smoothness,float metallic)
        {
            Material material;if(SharedMaterials.TryGetValue(name,out material))return material;
            material=NewMaterial(name,color,smoothness,metallic);SharedMaterials.Add(name,material);return material;
        }
        private static Transform Part(string name,Transform parent,Mesh mesh,Material material,Vector3 position,Vector3 scale,bool outline)
        {
            Transform root=Group(name,parent);root.localPosition=position;root.localScale=scale;
            root.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer renderer=root.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            if(outline)
            {
                Material dark=Shared("UFO dark silhouette",new Color(.065f,.045f,.075f),0,0);
                Transform shell=Part(name+" outline",root,MeshFactory.Inverted(mesh),dark,Vector3.zero,Vector3.one*1.045f,false);
                shell.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
            return root;
        }
        private static Transform Box(string name,Transform parent,Vector3 dimensions,Vector3 position,Material material,float bevel)
        {
            return Part(name,parent,MeshFactory.BeveledBox(dimensions,bevel),material,position,Vector3.one,false);
        }
        private static Transform Cylinder(string name,Transform parent,Material material,Vector3 position,float radius,float length)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=new Vector3(radius*2,length*.5f,radius*2);
            RemoveCollider(go);
            MeshRenderer renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;return go.transform;
        }
        private static Transform Sphere(string name,Transform parent,Material material,Vector3 position,Vector3 scale)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            RemoveCollider(go);
            MeshRenderer renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;return go.transform;
        }
        private static Transform Rod(string name,Transform parent,Vector3 from,Vector3 to,float radius,Material material)
        {
            Transform rod=Cylinder(name,parent,material,(from+to)*.5f,radius,Vector3.Distance(from,to));rod.localRotation=Quaternion.FromToRotation(Vector3.up,(to-from).normalized);return rod;
        }
        private static void RemoveCollider(GameObject go)
        {
            Collider collider=go.GetComponent<Collider>();if(!collider)return;collider.enabled=false;
            if(Application.isPlaying)UnityEngine.Object.Destroy(collider);else UnityEngine.Object.DestroyImmediate(collider);
        }
        private static TextMesh Text(string name,Transform parent,Color color,int order)
        {
            if(!font)
            {
                try{font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");}catch(ArgumentException){font=Resources.GetBuiltinResource<Font>("Arial.ttf");}
            }
            Transform root=Group(name,parent);TextMesh text=root.gameObject.AddComponent<TextMesh>();text.font=font;text.fontSize=96;text.characterSize=1;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontStyle=FontStyle.Bold;text.color=color;text.text="0";
            MeshRenderer renderer=root.GetComponent<MeshRenderer>();if(font)renderer.sharedMaterial=font.material;renderer.sortingOrder=order;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return text;
        }
    }
}
