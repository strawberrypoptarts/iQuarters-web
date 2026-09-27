using System.Text.Json;
using SceneKit;
using CoreGraphics;

namespace IQuarters.iOS;

// Loads recovered geometry and samples the original legacy keyframe curves.
public sealed partial class LegacyScene
{
    public SCNScene Scene { get; } = new();
    public SCNNode World { get; } = new();
    public Dictionary<int, SCNNode> Nodes { get; } = new();
    public Dictionary<int, JsonElement> Records { get; } = new();
    readonly LegacyAssetCache assets;
    readonly Dictionary<string,SCNGeometry> meshes;
    readonly Dictionary<string,SCNMaterial> materials=new();
    readonly Dictionary<string,JsonElement> clips=new();
    readonly Dictionary<int,Dictionary<string,string>> bindings=new();
    readonly List<Playback> playing=new();
    SCNNode[] roots=[];
    public HashSet<SCNNode> AnimatedNodes {get;}=new();
    sealed class Playback { public int Owner;public JsonElement Clip;public float Time;public float Speed=1;public float End=-1;public bool Loop;public Action? Complete; }
    public static string Resource(string path)=>Path.Combine(NSBundle.MainBundle.ResourcePath!,"RecoveredAssets",path);
    public static float[] Values(JsonElement value)=>value.EnumerateArray().Select(v=>v.ValueKind==JsonValueKind.Null ? float.PositiveInfinity : v.GetSingle()).ToArray();
    static SCNVector3 Vector(JsonElement e){var a=Values(e);return new(a[0],a[1],a[2]);}
    public LegacyScene(string file,bool overlay=false,bool recoveredLighting=false,LegacyAssetCache? cache=null)
    {
        assets=cache??new LegacyAssetCache();meshes=assets.Meshes;
        var root=assets.Read(file);
        bool Included(JsonElement n)=>file!="scene.json"||(overlay?n.GetProperty("layer").GetInt32()==12:n.GetProperty("layer").GetInt32()!=12);
        var neededMaterials=root.GetProperty("nodes").EnumerateArray().Where(Included).Where(n=>n.TryGetProperty("materials",out _)).SelectMany(n=>n.GetProperty("materials").EnumerateArray().Select(v=>v.GetString()!)).ToHashSet();
        if(recoveredLighting)lighting=assets.Read("lighting.json");
        Scene.RootNode.AddChildNode(World);if(overlay)World.Scale=new SCNVector3(-1,1,1);
        foreach(var item in root.GetProperty("materials").EnumerateObject()) {
            if(!neededMaterials.Contains(item.Name))continue;
            var m=new SCNMaterial {DoubleSided=true};if(overlay)m.LightingModelName=SCNLightingModel.Constant;
            var d=item.Value;
            if(d.GetProperty("colors").TryGetProperty("_Color",out var color)) {var a=Values(color);m.Diffuse.Contents=UIColor.FromRGBA(a[0],a[1],a[2],a[3]);}
            if(d.GetProperty("textures").TryGetProperty("_MainTex",out var texture)) {
                string path=Resource("textures/"+texture.GetProperty("asset").GetString()+".png");if(File.Exists(path))m.Diffuse.Contents=assets.Texture(path);
            }
            if(overlay&&d.GetProperty("textures").TryGetProperty("_MainTex",out _)&&d.GetProperty("colors").TryGetProperty("_Color",out var overlayTint)){
                // Color alpha is independent of texture alpha in the original shaders.
                float alpha=overlayTint[3].GetSingle();
                if(alpha>0&&alpha<1){m.Transparency=alpha;m.TransparencyMode=SCNTransparencyMode.AOne;m.WritesToDepthBuffer=false;}
                if(d.GetProperty("name").GetString()=="dimplane"){
                    m.Diffuse.Contents=Color(overlayTint);m.Transparency=1;m.TransparencyMode=SCNTransparencyMode.AOne;m.WritesToDepthBuffer=false;
                }
            }
            if(recoveredLighting)ConfigureMaterial(m,item.Name,d);
            materials[item.Name]=m;
        }
        foreach(var item in root.GetProperty("nodes").EnumerateArray()) {
            int id=item.GetProperty("id").GetInt32();Records[id]=item;
            var node=new SCNNode {Name=item.GetProperty("name").GetString(),Hidden=!item.GetProperty("active").GetBoolean()};Nodes[id]=node;if(recoveredLighting)node.CategoryBitMask=(nuint)(1u<<item.GetProperty("layer").GetInt32());
#if BROWSER
            node.SourceId=id;
#endif
            if(Included(item)&&item.TryGetProperty("mesh",out var mesh)&&mesh.ValueKind==JsonValueKind.String) {
                string key=mesh.GetString()!;
                if(!meshes.TryGetValue(key,out var g)&&File.Exists(Resource("meshes/"+key+".json")))meshes[key]=g=LoadMesh(key,assets.Read("meshes/"+key+".json"));
                if(g!=null) {node.Geometry=(SCNGeometry)g.Copy();if(item.TryGetProperty("materials",out var mats))node.Geometry.Materials=mats.EnumerateArray().Select(v=>materials.GetValueOrDefault(v.GetString()??"")??new SCNMaterial()).ToArray();}
                // Keep the geometry available for scripts that turn renderers on later.
                if(item.TryGetProperty("rendererEnabled",out var enabled)&&!enabled.GetBoolean())node.Opacity=0;
            }
            if(item.TryGetProperty("animation",out var animation)) {
                var names=new Dictionary<string,string>();bindings[id]=names;
                foreach(var clip in animation.GetProperty("clips").EnumerateArray()) {
                    if(clip.ValueKind!=JsonValueKind.String)continue;string key=clip.GetString()!;
                    if(!clips.ContainsKey(key))clips[key]=assets.Read("animations/"+key+".json");
                    names[clips[key].GetProperty("name").GetString()!]=key;
                }
            }
        }
        var owners=root.GetProperty("transforms").EnumerateArray().ToDictionary(t=>t.GetProperty("id").GetInt32(),t=>t.GetProperty("owner")[1].GetInt32());
        foreach(var t in root.GetProperty("transforms").EnumerateArray()) {
            var node=Nodes[t.GetProperty("owner")[1].GetInt32()];node.Position=Vector(t.GetProperty("position"));node.Scale=Vector(t.GetProperty("scale"));var q=Values(t.GetProperty("rotation"));node.Orientation=new(q[0],q[1],q[2],q[3]);
            int parent=t.GetProperty("parent")[1].GetInt32();if(parent==0)World.AddChildNode(node);else Nodes[owners[parent]].AddChildNode(node);
        }
        // Unity Renderer.enabled hides that mesh only. Node opacity would also hide children.
        foreach(var (id,node) in Nodes)if(node.ChildNodes.Length>0&&Records[id].TryGetProperty("rendererEnabled",out var visible)&&!visible.GetBoolean()){
            node.Geometry=null;node.Opacity=1;
        }
        roots=World.ChildNodes;
        foreach(var (owner,map) in bindings)foreach(var key in map.Values)foreach(var curve in clips[key].GetProperty("curves").EnumerateArray()){
            SCNNode? target=ResolveCurveNode(owner,curve.GetProperty("path").GetString()!);
            if(target!=null)AnimatedNodes.Add(target);
        }
    }
    public SCNNode? Find(string name)=>Nodes.Values.FirstOrDefault(n=>n.Name==name);
    SCNNode? ResolveCurveNode(int owner,string path)
    {
        SCNNode? node=Nodes[owner];bool first=true;
        foreach(var part in path.Split('/',StringSplitOptions.RemoveEmptyEntries)){
            if(first&&part==node?.Name){first=false;continue;}
            first=false;
            node=node?.ChildNodes.FirstOrDefault(n=>n.Name==part);
            if(node==null)break;
        }
        return node;
    }
    public int Id(string name)=>Nodes.First(p=>p.Value.Name==name).Key;
    public void HideRoots(){foreach(var n in roots)n.Hidden=true;}
    public static void Activate(SCNNode node){node.Hidden=false;foreach(var c in node.ChildNodes)Activate(c);}
    public void Show(string name,bool recursive=true){var n=Find(name);if(n==null)return;if(recursive)Activate(n);else n.Hidden=false;}
    public void Play(int owner,string name,Action? complete=null,bool loop=false,float speed=1)
    {
        if(!bindings.TryGetValue(owner,out var map)||!map.TryGetValue(name,out var key)){complete?.Invoke();return;}
        playing.RemoveAll(p=>p.Owner==owner);var p=new Playback {Owner=owner,Clip=clips[key],Loop=loop,Complete=complete,Speed=speed};playing.Add(p);Apply(owner,p.Clip,0);
    }
    public void PlayRange(int owner,string name,float start,float end,Action? complete=null)
    {
        if(!bindings.TryGetValue(owner,out var b)||!b.TryGetValue(name,out var key)){complete?.Invoke();return;}
        playing.RemoveAll(p=>p.Owner==owner);playing.Add(new Playback {Owner=owner,Clip=clips[key],Time=start,End=end,Complete=complete});Apply(owner,clips[key],start);
    }
    public bool IsPlaying(int owner)=>playing.Any(p=>p.Owner==owner);
    public float Duration(int owner,string name)=>bindings.TryGetValue(owner,out var b)&&b.TryGetValue(name,out var key)?clips[key].GetProperty("duration").GetSingle():0;
    public void Sample(int owner,string name,float time)
    {
        if(bindings.TryGetValue(owner,out var b)&&b.TryGetValue(name,out var key))Apply(owner,clips[key],time);
    }
    public void PlayDefault(int owner,Action? complete=null)
    {
        if(Records.TryGetValue(owner,out var r)&&r.TryGetProperty("animation",out var a)){
            string? key=a.GetProperty("default").GetString();if(key!=null&&clips.TryGetValue(key,out var clip)){Play(owner,clip.GetProperty("name").GetString()!,complete);return;}}
        complete?.Invoke();
    }
    public void PlayAutomatic()
    {
        foreach(var (id,r) in Records)if(r.TryGetProperty("animation",out var a)&&a.GetProperty("autoPlay").GetBoolean()) {
            var key=a.GetProperty("default").GetString();if(key!=null&&clips.TryGetValue(key,out var c))Play(id,c.GetProperty("name").GetString()!,loop:a.GetProperty("wrap").GetInt32()==2);
        }
    }
    public void Tick(float dt,int? onlyOwner=null)
    {
        foreach(var p in playing.ToArray()) {
            if(onlyOwner.HasValue&&p.Owner!=onlyOwner.Value)continue;
            p.Time+=dt*p.Speed;float duration=p.End>=0?p.End:p.Clip.GetProperty("duration").GetSingle();float t=p.Loop&&duration>0?p.Time%duration:Math.Min(p.Time,duration);Apply(p.Owner,p.Clip,t);
            if(!p.Loop&&p.Time>=duration){playing.Remove(p);p.Complete?.Invoke();}
        }
    }
    void Apply(int owner,JsonElement clip,float time)
    {
        foreach(var c in clip.GetProperty("curves").EnumerateArray()) {
            string path=c.GetProperty("path").GetString()!;var node=ResolveCurveNode(owner,path);
            if(node==null)continue;var value=Evaluate(c,time);if(value.Length==0)continue;
            switch(c.GetProperty("kind").GetString()) {
                case "position":node.Position=new(value[0],value[1],value[2]);break;
                case "scale":node.Scale=new(value[0],value[1],value[2]);break;
                case "rotation":float norm=MathF.Sqrt(value.Sum(v=>v*v));if(norm>0)node.Orientation=new(value[0]/norm,value[1]/norm,value[2]/norm,value[3]/norm);break;
                case "float":if(c.GetProperty("attribute").GetString()=="m_Enabled")node.Opacity=value[0];break;
            }
        }
    }
    static float[] Field(JsonElement key,string name){var e=key.GetProperty(name);return e.ValueKind==JsonValueKind.Array?Values(e):[e.ValueKind==JsonValueKind.Null?float.PositiveInfinity:e.GetSingle()];}
    public static float[] Evaluate(JsonElement curve,float time)
    {
        var keys=curve.GetProperty("keys");int count=keys.GetArrayLength();if(count==0)return [];
        if(time<=keys[0].GetProperty("time").GetSingle())return Field(keys[0],"value");
        if(time>=keys[count-1].GetProperty("time").GetSingle())return Field(keys[count-1],"value");
        int index=1;while(keys[index].GetProperty("time").GetSingle()<time)index++;
        var a=keys[index-1];var b=keys[index];float span=b.GetProperty("time").GetSingle()-a.GetProperty("time").GetSingle();float t=(time-a.GetProperty("time").GetSingle())/span;
        var av=Field(a,"value");var bv=Field(b,"value");var slopeA=Field(a,"outSlope");var slopeB=Field(b,"inSlope");
        for(int i=0;i<av.Length;i++)av[i]=!float.IsFinite(slopeA[i])||!float.IsFinite(slopeB[i])?av[i]:(2*t*t*t-3*t*t+1)*av[i]+(t*t*t-2*t*t+t)*span*slopeA[i]+(-2*t*t*t+3*t*t)*bv[i]+(t*t*t-t*t)*span*slopeB[i];
        return av;
    }
    static SCNGeometry LoadMesh(string key,JsonElement d)
    {
#if BROWSER
        return new SCNGeometry { MeshKey=key };
#else
        int count=d.GetProperty("vertices").GetArrayLength();
        var sources=new List<SCNGeometrySource>{SCNGeometrySource.FromVertices(d.GetProperty("vertices").EnumerateArray().Select(Vector).ToArray())};
        if(d.GetProperty("normals").GetArrayLength()==count)sources.Add(SCNGeometrySource.FromNormals(d.GetProperty("normals").EnumerateArray().Select(Vector).ToArray()));
        if(d.GetProperty("uv").GetArrayLength()==count)sources.Add(SCNGeometrySource.FromTextureCoordinates(d.GetProperty("uv").EnumerateArray().Select(v=>new CGPoint(v[0].GetSingle(),1-v[1].GetSingle())).ToArray()));
        if(key is "sharedassets1.assets-426" or "sharedassets1.assets-427"){
            var colors=d.GetProperty("colors");var rgba=new float[count*4];
            for(int i=0;i<count;i++){rgba[i*4]=rgba[i*4+1]=rgba[i*4+2]=1;rgba[i*4+3]=colors[i][1].GetInt32()>0?0:1;}
            var bytes=new byte[rgba.Length*4];Buffer.BlockCopy(rgba,0,bytes,0,bytes.Length);
            sources.Add(SCNGeometrySource.FromData(NSData.FromArray(bytes),SCNGeometrySourceSemantics.Color,count,true,4,4,0,16));
        }
        var elements=d.GetProperty("submeshes").EnumerateArray().Select(g=>{var ids=g.EnumerateArray().SelectMany(t=>t.EnumerateArray().Select(v=>v.GetInt32())).ToArray();var bytes=new byte[ids.Length*4];Buffer.BlockCopy(ids,0,bytes,0,bytes.Length);return SCNGeometryElement.FromData(NSData.FromArray(bytes),SCNGeometryPrimitiveType.Triangles,ids.Length/3,4);}).ToArray();
        return SCNGeometry.Create(sources.ToArray(),elements);
#endif
    }
}
