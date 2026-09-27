using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using SceneKit;
using CoreGraphics;
using IQuarters.iOS;
namespace IQuarters.Web;
public static partial class WebBridge {
 [JSImport("hit","host")]internal static partial string Hit(int root,int camera,double x,double y);
 [JSImport("project","host")]internal static partial double[] Project(int camera,double x,double y,double z);
 [JSImport("audio.play","host")]internal static partial void PlayAudio(string file,int channel,double volume);
 [JSImport("audio.stop","host")]internal static partial void StopAudio(int channel);
 [JSImport("audio.pause","host")]internal static partial void AudioPause();
 [JSImport("audio.resume","host")]internal static partial void AudioResume();
 [JSImport("storage.get","host")]internal static partial string? Load(string key);
 [JSImport("storage.set","host")]internal static partial void Save(string key,string value);
 [JSImport("storage.remove","host")]internal static partial void Remove(string key);
 [JSImport("prompt","host")]internal static partial string Prompt(string message,string value);
 [JSImport("alert","host")]internal static partial void Alert(string message);
 [JSImport("releaseScenes","host")]internal static partial void ReleaseGraphics(string ids);
 internal static void ReleaseScenes(params SCNScene?[] scenes){var ids=new List<int>();foreach(var scene in scenes){if(scene==null)continue;ids.Add(scene.RootNode.WebId);void Drop(SCNNode node){foreach(var child in node.ChildNodes)Drop(child);SCNNode.All.Remove(node.WebId);sent.Remove(node.WebId);}Drop(scene.RootNode);}ReleaseGraphics(string.Join(',',ids));}
 static readonly Dictionary<int,NodeStamp> sent=[];
 readonly record struct NodeStamp(int Parent,string? Name,bool Hidden,float Alpha,System.Numerics.Vector3 Position,System.Numerics.Quaternion Rotation,System.Numerics.Vector3 Scale,string? Mesh,int Materials,bool Ortho,double Half,double Fov,double Near,double Far);
 [JSExport]public static void InstallAsset(string path,string contents){path="/RecoveredAssets/"+path;Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,contents);}
 [JSExport]public static void Start(double width,double height,double ratio){Resize(width,height,ratio,0,0,0,0);var menu=new MainMenuController();UIHost.Current=menu;menu.LoadViewIfNeeded();menu.ViewDidAppear(false);}
 [JSExport]public static void Resize(double width,double height,double ratio,double top,double right,double bottom,double left){UIHost.Width=width;UIHost.Height=height;UIScreen.MainScreen.NativeScale=ratio;UIHost.SafeArea=new(){Top=top,Right=right,Bottom=bottom,Left=left};if(UIHost.Current is {} c){c.View.Frame=new(0,0,width,height);foreach(var v in c.View.Descendants())if(v.AutoresizingMask==UIViewAutoresizing.FlexibleDimensions)v.Frame=new(0,0,width,height);c.ViewDidLayoutSubviews();}}
 [JSExport]public static void Pointer(int kind,double x,double y,double time){var touch=UIHost.Current?.View.Descendants().OfType<TouchSceneView>().FirstOrDefault();if(touch==null)return;var p=new CGPoint(x,y);if(kind==0)touch.BeginContact?.Invoke(p,time);else if(kind==1)touch.MoveContact?.Invoke(p,time);else if(kind==2)touch.EndContact?.Invoke();else touch.CancelContact?.Invoke();}
 [JSExport]public static void Tap(double x,double y){UIHost.Current?.View.Descendants().OfType<SCNView>().LastOrDefault(v=>v.Tapped!=null)?.Tapped?.Invoke(new(x,y));}
 [JSExport]public static void Visibility(bool hidden)=>NSNotificationCenter.DefaultCenter.Post(hidden?UIApplication.WillResignActiveNotification:UIApplication.DidBecomeActiveNotification);
 [JSExport]public static void Motion(double timestamp,double x,double y,double z){if(CoreMotion.CMMotionManager.Active is {} m)m.AccelerometerData=new(timestamp,new(x,y,z));}
 [JSExport]public static string Frame(double seconds){foreach(var loop in DisplayFrameLoop.Active.ToArray())loop.Tick((float)Math.Clamp(seconds,0,.05));return Snapshot();}
 static MaterialMessage Material(SCNMaterial m)=>new(m.Key,m.LightingModelName==SCNLightingModel.Constant,m.WritesToDepthBuffer,m.Transparency,m.Diffuse.Contents is UIImage img?Path.GetFileName(img.Path):null,m.Diffuse.Contents is UIColor col?col.Rgba:null,m.Diffuse.ContentsTransform.Value.M41);
 static string Snapshot(){var current=UIHost.Current!;var views=current.View.Descendants().OfType<SCNView>().ToArray();var updates=new List<string>();
 void Visit(SCNNode n){var p=n.Position;var q=n.Orientation;var s=n.Scale;var c=n.Camera;var hash=new HashCode();if(n.Geometry!=null)foreach(var m in n.Geometry.Materials){hash.Add(m.Key);hash.Add(m.LightingModelName);hash.Add(m.WritesToDepthBuffer);hash.Add(m.Transparency);hash.Add(m.Diffuse.Contents);hash.Add(m.Diffuse.ContentsTransform.Value.M41);}
 var stamp=new NodeStamp(n.ParentNode?.WebId??0,n.Name,n.Hidden,n.Opacity,p,q,s,n.Geometry?.MeshKey,hash.ToHashCode(),c?.UsesOrthographicProjection??false,c?.OrthographicScale??0,c?.FieldOfView??0,c?.ZNear??0,c?.ZFar??0);
 if(!sent.TryGetValue(n.WebId,out var previous)||previous!=stamp){sent[n.WebId]=stamp;updates.Add(JsonSerializer.Serialize(new NodeMessage(n.WebId,n.ParentNode?.WebId??0,n.Name,n.SourceId,(uint)n.CategoryBitMask,n.Hidden,n.Opacity,[p.X,p.Y,p.Z],[q.X,q.Y,q.Z,q.W],[s.X,s.Y,s.Z],n.Geometry?.MeshKey,n.Geometry?.Materials.Select(Material).ToArray(),c==null?null:new CameraMessage(c.UsesOrthographicProjection,c.OrthographicScale,c.FieldOfView,c.ZNear,c.ZFar)),RenderJson.Default.NodeMessage));}foreach(var child in n.ChildNodes)Visit(child);}
 foreach(var view in views)Visit(view.Scene.RootNode);
 var roots=views.Select(v=>new RootMessage(v.Scene.RootNode.WebId,v.PointOfView!.WebId)).ToArray();
 var labels=current.View.Descendants().OfType<UILabel>().Select(v=>new LabelMessage(v.WebId,v.Text,v.Frame.X,v.Frame.Y,v.Frame.Width,v.Frame.Height,v.Font.Size,v.TextAlignment==UITextAlignment.Left?"left":v.TextAlignment==UITextAlignment.Right?"right":"center",v.BackgroundColor?.Rgba,v.TextColor.Rgba)).ToArray();
 return "{\"nodes\":["+string.Join(',',updates)+"],\"roots\":"+JsonSerializer.Serialize(roots,RenderJson.Default.RootMessageArray)+",\"labels\":"+JsonSerializer.Serialize(labels,RenderJson.Default.LabelMessageArray)+"}";
 }
 [JSExport]public static int Verify()=>IQuarters.Verification.VerificationSuite.Run(["/RecoveredAssets"],exhaustive:false);
 public static void Main(){}
}
