#nullable enable
using System.Numerics;
namespace IQuarters.Core;

public sealed class CollisionShape
{
    public int Owner {get;init;}
    public int ColliderId {get;init;}
    public int Multiplier {get;init;}
    public SurfaceMaterial Material {get;init;}=new();
    public int ReactionOwner {get;init;}
    public Vector3? LaunchVelocity {get;init;}
    public float LaunchNormalThreshold {get;init;}=.707f;
    public float ExtraShotTime {get;init;}=2;
    public Vector3[][] Triangles {get;init;}=[];
    public Vector3[][]? SurfaceVelocities {get;set;}
    public Vector3 Minimum {get;private set;}
    public Vector3 Maximum {get;private set;}
    public (Vector3 Min,Vector3 Max)[] Bounds {get;private set;}=[];
    public void Prepare()
    {
        Minimum=new(float.PositiveInfinity);Maximum=new(float.NegativeInfinity);
        if(Bounds.Length!=Triangles.Length)Bounds=new (Vector3,Vector3)[Triangles.Length];
        for(int i=0;i<Triangles.Length;i++){var t=Triangles[i];var min=Vector3.Min(t[0],Vector3.Min(t[1],t[2]));var max=Vector3.Max(t[0],Vector3.Max(t[1],t[2]));Bounds[i]=(min,max);Minimum=Vector3.Min(Minimum,min);Maximum=Vector3.Max(Maximum,max);}
    }
    public Vector3 SurfaceVelocity(int index,Vector3 point)
    {
        if(SurfaceVelocities==null)return Vector3.Zero;var t=Triangles[index];var a=t[1]-t[0];var b=t[2]-t[0];var p=point-t[0];
        float aa=Vector3.Dot(a,a),ab=Vector3.Dot(a,b),bb=Vector3.Dot(b,b),pa=Vector3.Dot(p,a),pb=Vector3.Dot(p,b),den=aa*bb-ab*ab;
        if(Math.Abs(den)<1e-12f)return SurfaceVelocities[index][0];float v=(bb*pa-ab*pb)/den,w=(aa*pb-ab*pa)/den;return SurfaceVelocities[index][0]*(1-v-w)+SurfaceVelocities[index][1]*v+SurfaceVelocities[index][2]*w;
    }
}
public sealed class ShotSimulation
{
    public Vector3 Position {get;private set;}
    public Vector3 Velocity {get;private set;}
    public Quaternion Orientation {get;private set;}
    public Vector3 AngularVelocity {get;private set;}
    public float Elapsed {get;private set;}
    public bool Finished {get;private set;}
    public int Multiplier {get;private set;}
    public int Ricochets=>chain.Ricochets;
    public int ContactCount {get;private set;}
    public CollisionShape? LastContact {get;private set;}
    public event Action<int>? ReactionTriggered;
    public event Action<CollisionShape,Vector3>? ContactEntered;
    readonly List<CollisionShape> shapes;readonly CoinBody body;readonly CollisionChain chain=new();
    readonly Dictionary<CollisionShape,float> lastContacts=[];readonly Dictionary<int,float> lastReactions=[];
    readonly (Vector3[] Vertices,Vector3[] Normals,Vector3[] Edges)[] transformed;
    readonly float power,angle,sideways;float timeout,accumulator;int steps,slowTicks;bool launched;
    int supportedMultiplier;
    public int ScoringOwner {get;private set;}
    public ShotSimulation(Vector3 start,float power,float angle,float sideways,IEnumerable<CollisionShape> shapes,float maxTime=4,CoinBody? body=null)
    {
        if(!float.IsFinite(power)||!float.IsFinite(angle)||!float.IsFinite(sideways))throw new ArgumentException("Shot inputs must be finite.");
        this.body=body??new CoinBody();Position=start;Orientation=this.body.InitialOrientation;this.power=Math.Max(power,0);this.angle=Math.Clamp(angle,45,55);this.sideways=sideways;timeout=maxTime+.08f;
        Velocity=new(this.sideways,-16,(1-(this.angle-45)/10)*30);this.shapes=shapes.ToList();
        transformed=this.body.Pieces.Select(p=>(new Vector3[p.Vertices.Length],new Vector3[p.Normals.Length],new Vector3[p.Edges.Length])).ToArray();
    }
    public void Advance(float seconds)
    {
        if(Finished||!float.IsFinite(seconds)||seconds<0)return;accumulator+=Math.Min(seconds,.1f);
        const float dt=.004f;while(accumulator>=dt&&!Finished){Step(dt);accumulator-=dt;}
    }
    Vector3 InverseInertia(Vector3 value)
    {
        var v=Vector3.Transform(value,Quaternion.Conjugate(Orientation));v=new(v.X/Math.Max(.001f,body.Inertia.X),v.Y/Math.Max(.001f,body.Inertia.Y),v.Z/Math.Max(.001f,body.Inertia.Z));return Vector3.Transform(v,Orientation);
    }
    void Impulse(Vector3 value,Vector3 arm){Velocity+=value/body.Mass;AngularVelocity+=InverseInertia(Vector3.Cross(arm,value));}
    float EffectiveMass(Vector3 arm,Vector3 direction)=>1/body.Mass+Vector3.Dot(direction,Vector3.Cross(InverseInertia(Vector3.Cross(arm,direction)),arm));
    bool Near(Vector3 min,Vector3 max)=>Position.X>=min.X-body.Radius&&Position.X<=max.X+body.Radius&&Position.Y>=min.Y-body.Radius&&Position.Y<=max.Y+body.Radius&&Position.Z>=min.Z-body.Radius&&Position.Z<=max.Z+body.Radius;
    void Step(float dt)
    {
        Elapsed=++steps*dt;
        if(!launched&&steps>=20){Position+=Vector3.UnitY;float radians=angle*MathF.PI/180;Velocity=new(sideways,9*power*MathF.Sin(radians),9*power*MathF.Cos(radians));AngularVelocity+=InverseInertia(Vector3.UnitX*6*.02f);launched=true;}
        Velocity+=new Vector3(0,-19.64f*dt,0);Position+=Velocity*dt;
        AngularVelocity/=1+body.AngularDrag*dt;float speed=AngularVelocity.Length();if(speed>7){AngularVelocity*=7/speed;speed=7;}
        if(speed>.00001f)Orientation=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(AngularVelocity/speed,speed*dt)*Orientation);
        for(int h=0;h<body.Pieces.Length;h++) {var p=body.Pieces[h];var world=transformed[h];
            for(int i=0;i<p.Vertices.Length;i++)world.Vertices[i]=Vector3.Transform(p.Vertices[i],Orientation);
            for(int i=0;i<p.Normals.Length;i++)world.Normals[i]=Vector3.Transform(p.Normals[i],Orientation);
            for(int i=0;i<p.Edges.Length;i++)world.Edges[i]=Vector3.Transform(p.Edges[i],Orientation);}
        foreach(var shape in shapes){if(!Near(shape.Minimum,shape.Maximum))continue;
            for(int h=0;h<body.Pieces.Length;h++){
                var hull=transformed[h];float deepest=-1;Vector3 normal=default,point=default;int triangle=-1;
                for(int i=0;i<shape.Triangles.Length;i++){var bounds=shape.Bounds[i];if(!Near(bounds.Min,bounds.Max))continue;var t=shape.Triangles[i];
                    if(ConvexContact.Triangle(hull.Vertices,hull.Normals,hull.Edges,Position,t[0],t[1],t[2],out var n,out var p,out var depth)&&depth>deepest){deepest=depth;normal=n;point=p;triangle=i;}}
                if(triangle<0)continue;
                var arm=point-Position;var surface=shape.SurfaceVelocity(triangle,point);var relative=Velocity+Vector3.Cross(AngularVelocity,arm)-surface;float incoming=Vector3.Dot(relative,normal);
                Position+=normal*Math.Max(0,deepest-.0005f)*.85f;
                // Native OnCollisionStay tests contact.normal.y > .05 (not separation).
                bool entered=!lastContacts.TryGetValue(shape,out float last)||Elapsed-last>.012f;lastContacts[shape]=Elapsed;
                if(!entered&&normal.Y>.05f){supportedMultiplier=shape.Multiplier;ScoringOwner=shape.Owner;}
                if(incoming<0){int mode=SurfaceMaterial.WinningMode(body.Material.BounceCombine,shape.Material.BounceCombine);float bounce=Math.Abs(incoming)<3?0:SurfaceMaterial.Combine(body.Material.Bounce,shape.Material.Bounce,mode);
                    float impulse=-(1+bounce)*incoming/EffectiveMass(arm,normal);Impulse(normal*impulse,arm);
                    var tangent=relative-incoming*normal;float length=tangent.Length();if(length>.00001f){tangent/=length;float frictionImpulse=length/EffectiveMass(arm,tangent);int fm=SurfaceMaterial.WinningMode(body.Material.FrictionCombine,shape.Material.FrictionCombine);float sf=SurfaceMaterial.Combine(body.Material.StaticFriction,shape.Material.StaticFriction,fm),df=SurfaceMaterial.Combine(body.Material.DynamicFriction,shape.Material.DynamicFriction,fm);if(frictionImpulse>sf*impulse)frictionImpulse=df*impulse;Impulse(-tangent*frictionImpulse,arm);}
                }
                if(entered){ContactCount++;LastContact=shape;if(launched){chain.Add(shape.ColliderId,shape.Owner,Elapsed-.08f);ContactEntered?.Invoke(shape,point);}
                    if(shape.LaunchVelocity is {} launch&&normal.Y>shape.LaunchNormalThreshold&&(!lastReactions.TryGetValue(shape.ReactionOwner,out float reactionTime)||Elapsed-reactionTime>.1f)){Velocity=launch;timeout+=shape.ExtraShotTime;lastReactions[shape.ReactionOwner]=Elapsed;ReactionTriggered?.Invoke(shape.ReactionOwner);}}
            }
        }
        if(launched&&steps%5==0){slowTicks=Velocity.LengthSquared()<1?slowTicks+1:0;
            if(slowTicks>45||Elapsed>timeout||Position.Y< -10){Finished=true;Multiplier=Position.Y>=-10?supportedMultiplier:0;}}
    }
    public static Vector3 ClosestPoint(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
    {
        var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
        if(d1<=0 && d2<=0)return a;
        var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);
        if(d3>=0 && d4<=d3)return b;
        float vc=d1*d4-d3*d2;
        if(vc<=0 && d1>=0 && d3<=0)return a+(d1/(d1-d3))*ab;
        var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);
        if(d6>=0 && d5<=d6)return c;
        float vb=d5*d2-d1*d6;
        if(vb<=0 && d2>=0 && d6<=0)return a+(d2/(d2-d6))*ac;
        float va=d3*d6-d5*d4;
        if(va<=0 && d4-d3>=0 && d5-d6>=0)return b+((d4-d3)/((d4-d3)+(d5-d6)))*(c-b);
        float total=va+vb+vc;
        if(Math.Abs(total)<1e-12f)return a;
        return a+ab*(vb/total)+ac*(vc/total);
    }
}
