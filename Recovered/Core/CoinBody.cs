using System.Numerics;
using System.Text.Json;
namespace IQuarters.Core;

public sealed class SurfaceMaterial
{
    public float DynamicFriction=.6f, StaticFriction=.6f, Bounce;
    public int FrictionCombine, BounceCombine;
    public static SurfaceMaterial Read(JsonElement e)=>e.ValueKind==JsonValueKind.Null?new():new(){DynamicFriction=e.GetProperty("dynamicFriction").GetSingle(),StaticFriction=e.GetProperty("staticFriction").GetSingle(),Bounce=e.GetProperty("bounce").GetSingle(),FrictionCombine=e.GetProperty("frictionCombine").GetInt32(),BounceCombine=e.GetProperty("bounceCombine").GetInt32()};
    // Serialized Unity modes: average, multiply, minimum, maximum.
    public static int Priority(int mode)=>mode switch{2=>1,1=>2,_=>mode};
    public static int WinningMode(int a,int b)=>Priority(a)>=Priority(b)?a:b;
    public static float Combine(float a,float b,int mode)=>mode switch {1=>a*b,2=>Math.Min(a,b),3=>Math.Max(a,b),_ =>(a+b)*.5f};
}
public sealed class ConvexPiece
{
    public Vector3[] Vertices {get;}
    public Vector3[] Normals {get;}
    public Vector3[] Edges {get;}
    static Vector3 Direction(Vector3 v){v=Vector3.Normalize(v);if(v.X<-.0001f||(Math.Abs(v.X)<.0001f&&v.Y<-.0001f)||(Math.Abs(v.X)<.0001f&&Math.Abs(v.Y)<.0001f&&v.Z<0))v=-v;return v;}
    public ConvexPiece(Vector3[] input,int[][] triangles)
    {
        var vertices=new List<Vector3>();var map=new int[input.Length];
        for(int i=0;i<input.Length;i++){int j=vertices.FindIndex(v=>Vector3.DistanceSquared(v,input[i])<1e-10f);if(j<0){j=vertices.Count;vertices.Add(input[i]);}map[i]=j;}Vertices=vertices.ToArray();
        var normals=new List<Vector3>();var adjacency=new Dictionary<(int,int),List<Vector3>>();
        foreach(var tri in triangles){int a=map[tri[0]],b=map[tri[1]],c=map[tri[2]];var n=Vector3.Cross(Vertices[b]-Vertices[a],Vertices[c]-Vertices[a]);if(n.LengthSquared()<1e-12f)continue;n=Direction(n);if(!normals.Any(v=>Vector3.Dot(v,n)>.99999f))normals.Add(n);
            foreach(var edge in new[]{(a,b),(b,c),(c,a)}){var key=edge.Item1<edge.Item2?edge:(edge.Item2,edge.Item1);if(!adjacency.TryGetValue(key,out var list))adjacency[key]=list=[];list.Add(n);}}
        var edges=new List<Vector3>();foreach(var (pair,ns) in adjacency){if(ns.Count>1&&ns.All(n=>Vector3.Dot(n,ns[0])>.99999f))continue;var e=Direction(Vertices[pair.Item1]-Vertices[pair.Item2]);if(!edges.Any(v=>Vector3.Dot(v,e)>.99999f))edges.Add(e);}Normals=normals.ToArray();Edges=edges.ToArray();
    }
}
public sealed class CoinBody
{
    public ConvexPiece[] Pieces=[];
    public Quaternion InitialOrientation=Quaternion.Identity;
    public SurfaceMaterial Material=new(){DynamicFriction=.2f,StaticFriction=.2f,Bounce=.65f,BounceCombine=3};
    public float AngularDrag=.05f,Mass=1,Radius;
    public Vector3 Inertia;
    public static CoinBody Read(string text)
    {
        using var doc=JsonDocument.Parse(text);var root=doc.RootElement;var q=root.GetProperty("initialOrientation");
        var body=new CoinBody {InitialOrientation=Quaternion.Normalize(new(q[0].GetSingle(),q[1].GetSingle(),q[2].GetSingle(),q[3].GetSingle())),AngularDrag=root.GetProperty("quarter").GetProperty("angularDrag").GetSingle(),Mass=root.GetProperty("quarter").GetProperty("mass").GetSingle(),Material=SurfaceMaterial.Read(root.GetProperty("materials").GetProperty("744"))};
        body.Pieces=root.GetProperty("compound").EnumerateArray().Select(e=>new ConvexPiece(e.GetProperty("vertices").EnumerateArray().Select(ReadVector).ToArray(),e.GetProperty("triangles").EnumerateArray().Select(t=>t.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToArray())).ToArray();
        var all=body.Pieces.SelectMany(p=>p.Vertices).ToArray();body.Radius=all.Max(v=>v.Length());
        // Volume-weighted box/cylinder inertias; legacy PhysX auto-inertia itself is unavailable.
        var components=new List<(float Volume,Vector3 Inertia)>();
        foreach(var p in body.Pieces){var min=p.Vertices.Aggregate(Vector3.Min);var max=p.Vertices.Aggregate(Vector3.Max);var s=max-min;
            if(p.Vertices.Length==8)components.Add((s.X*s.Y*s.Z,new((s.Y*s.Y+s.Z*s.Z)/12,(s.X*s.X+s.Z*s.Z)/12,(s.X*s.X+s.Y*s.Y)/12)));
            else {float r=s.X/2;components.Add((MathF.PI*r*r*s.Z,new(r*r/4+s.Z*s.Z/12,r*r/4+s.Z*s.Z/12,r*r/2)));}}
        float volume=components.Sum(c=>c.Volume);body.Inertia=components.Aggregate(Vector3.Zero,(sum,c)=>sum+c.Inertia*(c.Volume/volume))*body.Mass;return body;
    }
    public static Vector3 ReadVector(JsonElement e)=>new(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle());
}

public static class ConvexContact
{
    public static bool Triangle(Vector3[] vertices,Vector3[] normals,Vector3[] edges,Vector3 center,Vector3 a,Vector3 b,Vector3 c,out Vector3 normal,out Vector3 point,out float penetration)
    {
        Vector3 best=default;float depth=float.PositiveInfinity;var ab=b-a;var bc=c-b;var ca=a-c;
        bool Axis(Vector3 n){float length=n.LengthSquared();if(length<1e-10f)return true;n/=MathF.Sqrt(length);
            float lo=float.PositiveInfinity,hi=float.NegativeInfinity;foreach(var v in vertices){float d=Vector3.Dot(center+v,n);lo=Math.Min(lo,d);hi=Math.Max(hi,d);}
            float aa=Vector3.Dot(a,n),bb=Vector3.Dot(b,n),cc=Vector3.Dot(c,n),tl=Math.Min(aa,Math.Min(bb,cc)),th=Math.Max(aa,Math.Max(bb,cc));
            float left=hi-tl,right=th-lo;if(left<0||right<0)return false;float amount=Math.Min(left,right);if(amount<depth){depth=amount;best=left<right?-n:n;}return true;}
        normal=point=default;penetration=0;
        if(!Axis(Vector3.Cross(ab,bc)))return false;
        foreach(var n in normals)if(!Axis(n))return false;
        foreach(var e in edges)if(!Axis(Vector3.Cross(e,ab))||!Axis(Vector3.Cross(e,bc))||!Axis(Vector3.Cross(e,ca)))return false;
        float support=float.PositiveInfinity;foreach(var v in vertices)support=Math.Min(support,Vector3.Dot(v,best));var contact=Vector3.Zero;int count=0;
        foreach(var v in vertices)if(Vector3.Dot(v,best)<support+.0001f){contact+=v;count++;}
        point=ShotSimulation.ClosestPoint(center+contact/Math.Max(1,count),a,b,c);normal=best;penetration=depth;return float.IsFinite(depth);
    }
}
