global using nfloat=System.Double;
global using IQuarters.Web;
using CoreGraphics;
using SceneKit;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
namespace CoreGraphics {
 public record struct CGPoint(double X,double Y);
 public record struct CGSize(double Width,double Height);
 public record struct CGRect(double X,double Y,double Width,double Height);
}
namespace IQuarters.Web {
public class NSObject:IDisposable {public virtual void Dispose(){}}
public sealed class NSAutoreleasePool:NSObject{}
public sealed class NSBundle {public static NSBundle MainBundle=new();public string ResourcePath=>"/";}
public sealed class NSUrl(string path){public string Path=path;public static NSUrl FromFilename(string p)=>new(p);}
public sealed class UIColor(float r,float g,float b,float a){public float[] Rgba=[r,g,b,a];public UIColor CGColor=>this;public static UIColor Black=new(0,0,0,1),White=new(1,1,1,1),Clear=new(0,0,0,0);public static UIColor FromRGBA(float r,float g,float b,float a)=>new(r,g,b,a);public static UIColor FromWhiteAlpha(float w,float a)=>new(w,w,w,a);}
public sealed class UIImage(string path){public string Path=path;public static UIImage FromFile(string p)=>new(p);}
public enum UIViewAutoresizing {None,FlexibleDimensions}public enum UITextAlignment {Center,Left,Right}public enum UIUserInterfaceIdiom {Phone,Pad}
public enum UIModalPresentationStyle {FullScreen}public enum UIAlertControllerStyle {Alert}public enum UIAlertActionStyle {Default}public enum UITextAutocorrectionType {No}public enum UITextAutocapitalizationType {AllCharacters}
public sealed class UILayer {public UIColor? ShadowColor;public float ShadowOpacity;public CGSize ShadowOffset;}
public sealed class UIFont(double size){public double Size=size;public static UIFont BoldSystemFontOfSize(double s)=>new(s);public static UIFont SystemFontOfSize(double s)=>new(s);}
public sealed class UIScreen {public static UIScreen MainScreen=new();public double NativeScale=1;}
public sealed class UIDevice {public static UIDevice CurrentDevice=new();public UIUserInterfaceIdiom UserInterfaceIdiom=>UIHost.Width>=768?UIUserInterfaceIdiom.Pad:UIUserInterfaceIdiom.Phone;}
public sealed class UIWindow {public UIScreen Screen=>UIScreen.MainScreen;}
public struct Insets {public double Top,Bottom,Left,Right;}
public class UIView {
 static int serial;public int WebId=++serial;public CGRect Frame;public virtual CGRect Bounds=>new(0,0,Frame.Width,Frame.Height);public UIColor? BackgroundColor;public bool UserInteractionEnabled=true;public UIViewAutoresizing AutoresizingMask;public UILayer Layer=new();public Insets SafeAreaInsets=>UIHost.SafeArea;public UIWindow Window=new();public UIView? Parent;readonly List<UIView> children=[];public UIView[] Subviews=>children.ToArray();public Action<CGPoint>? Tapped;
 public UIView():this(new(0,0,UIHost.Width,UIHost.Height)){} public UIView(CGRect frame){Frame=frame;}
 public void AddSubview(UIView v){v.RemoveFromSuperview();v.Parent=this;children.Add(v);} public void RemoveFromSuperview(){Parent?.children.Remove(this);Parent=null;}
 public void AddGestureRecognizer(UITapGestureRecognizer g){Tapped=p=>{g.Position=p;g.Action(g);};}
 public IEnumerable<UIView> Descendants(){foreach(var v in children){yield return v;foreach(var c in v.Descendants())yield return c;}}
}
public class UILabel:UIView {public string Text="";public UIColor TextColor=UIColor.White;public UITextAlignment TextAlignment;public int Lines;public UIFont Font=new(18);public bool AdjustsFontSizeToFitWidth;public float MinimumScaleFactor;public UILabel(){}public UILabel(CGRect frame):base(frame){}}
public sealed class UITapGestureRecognizer(Action<UITapGestureRecognizer> action){public Action<UITapGestureRecognizer> Action=action;public bool CancelsTouchesInView;public CGPoint Position;public CGPoint LocationInView(UIView view)=>Position;}
public class UIViewController {
 public UIView View=new();public UIModalPresentationStyle ModalPresentationStyle;bool loaded;public UIViewController? ParentController;
 public virtual bool CanBecomeFirstResponder=>false;public virtual bool PrefersStatusBarHidden()=>true;public virtual void ViewDidLoad(){}public virtual void ViewDidLayoutSubviews(){}public virtual void ViewDidAppear(bool animated){}public virtual void ViewDidDisappear(bool animated){}public void BecomeFirstResponder(){}
 public void LoadViewIfNeeded(){if(loaded)return;loaded=true;ViewDidLoad();ViewDidLayoutSubviews();}
 public void PresentViewController(UIViewController controller,bool animated,Action? done){if(controller is UIAlertController alert){alert.Show();done?.Invoke();return;}ViewDidDisappear(false);controller.ParentController=this;UIHost.Current=controller;controller.LoadViewIfNeeded();controller.ViewDidAppear(false);done?.Invoke();}
 public void DismissViewController(bool animated,Action? done){UIHost.Current?.ViewDidDisappear(false);UIHost.Current=this;ViewDidAppear(false);done?.Invoke();}
}
public sealed class UITextField {public string? Text;public UITextAutocorrectionType AutocorrectionType;public UITextAutocapitalizationType AutocapitalizationType;}
public sealed class UIAlertAction(Action<UIAlertAction> action){public Action<UIAlertAction> Action=action;public static UIAlertAction Create(string title,UIAlertActionStyle style,Action<UIAlertAction>? action)=>new(action??(_=>{}));}
public sealed class UIAlertController:UIViewController {string title="",message="";public UITextField[] TextFields=[];readonly List<UIAlertAction> actions=[];public static UIAlertController Create(string t,string m,UIAlertControllerStyle s)=>new(){title=t,message=m};public void AddTextField(Action<UITextField> setup){var f=new UITextField();setup(f);TextFields=[f];}public void AddAction(UIAlertAction action)=>actions.Add(action);public void Show(){if(TextFields.Length>0)TextFields[0].Text=WebBridge.Prompt(title+"\n"+message,TextFields[0].Text??"");else WebBridge.Alert(title+"\n"+message);actions.FirstOrDefault()?.Action(actions[0]);}}
public static class UIApplication {public const string WillResignActiveNotification="pause",DidBecomeActiveNotification="resume";}
public sealed class NSNotificationCenter {
 public static NSNotificationCenter DefaultCenter=new();readonly Dictionary<NSObject,(string,Action<NSObject>)> observers=[];
 public NSObject AddObserver(string name,Action<NSObject> action){var token=new NSObject();observers[token]=(name,action);return token;}public void RemoveObserver(NSObject token)=>observers.Remove(token);
 public void Post(string name){foreach(var (token,(n,a)) in observers.ToArray())if(n==name)a(token);}
}
public sealed class NSUserDefaults {public static NSUserDefaults StandardUserDefaults=new();public long IntForKey(string k)=>long.TryParse(StringForKey(k),out var v)?v:0;public bool BoolForKey(string k)=>StringForKey(k)=="true";public string? StringForKey(string k)=>WebBridge.Load(k);public void SetInt(long v,string k)=>SetString(v.ToString(),k);public void SetBool(bool v,string k)=>SetString(v?"true":"false",k);public void SetString(string v,string k)=>WebBridge.Save(k,v);public void RemoveObject(string k)=>WebBridge.Remove(k);}
public static class UIHost {public static double Width=390,Height=844;public static Insets SafeArea;public static UIViewController? Current;}
}
namespace AVFoundation {public sealed class AVAudioPlayer:IQuarters.Web.NSObject {string path="";public static AVAudioPlayer FromUrl(IQuarters.Web.NSUrl url)=>new(){path=url.Path};public void Play()=>IQuarters.Web.WebBridge.PlayAudio(System.IO.Path.GetFileName(path),4,1);public void Stop()=>IQuarters.Web.WebBridge.StopAudio(4);}}
namespace CoreMotion {public sealed class CMMotionManager {public static CMMotionManager? Active;public bool AccelerometerAvailable=>true;public double AccelerometerUpdateInterval;public MotionSample? AccelerometerData;public void StartAccelerometerUpdates()=>Active=this;public void StopAccelerometerUpdates(){if(Active==this)Active=null;}}public record MotionSample(double Timestamp,Acceleration Acceleration);public record Acceleration(double X,double Y,double Z);}
namespace IQuarters.iOS {
public sealed class GameAudio:IDisposable {public void Stop(int channel)=>WebBridge.StopAudio(channel);public void Play(string file,int channel=0,float volume=1)=>WebBridge.PlayAudio(file,channel,volume);public void Pause()=>WebBridge.AudioPause();public void Resume()=>WebBridge.AudioResume();public void Dispose()=>WebBridge.AudioPause();}
public sealed class DisplayFrameLoop:IDisposable {public static readonly List<DisplayFrameLoop> Active=[];public Action<float> Tick;public DisplayFrameLoop(UIScreen screen,Action<float> tick,params SCNView[] views){Tick=tick;Active.Add(this);}public void Dispose()=>Active.Remove(this);}
public sealed class TouchSceneView:CGRectView {public Action<CGPoint,double>? BeginContact,MoveContact;public Action? EndContact,CancelContact;public TouchSceneView(CGRect frame):base(frame){}}
public class CGRectView:SCNView {public CGRectView(CGRect frame):base(frame){}}
public sealed partial class LegacyScene {
 System.Text.Json.JsonElement lighting;
 static UIColor Color(System.Text.Json.JsonElement e)=>UIColor.FromRGBA(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle(),e[3].GetSingle());
 void ConfigureMaterial(SCNMaterial material,string key,System.Text.Json.JsonElement data){material.Key=key;material.LightingModelName=SCNLightingModel.Lambert;}
 public void RestoreLighting(){foreach(var source in lighting.GetProperty("lights").EnumerateArray()){int owner=source.GetProperty("owner").GetInt32();if(source.GetProperty("enabled").GetBoolean()&&Records[owner].GetProperty("active").GetBoolean()&&(source.GetProperty("mask").GetUInt32()&0xf00)!=0)Nodes[owner].Hidden=false;}}
 public void ShowGlassShadows(int round){foreach(var n in Nodes.Values)if(n.Name?.StartsWith("glassShadow")==true)n.Hidden=true;foreach(var item in lighting.GetProperty("roundShadows")[round].EnumerateArray()){var glass=Nodes[item.GetProperty("glass").GetInt32()];var shadow=Nodes[item.GetProperty("shadow").GetInt32()];shadow.Hidden=false;shadow.Opacity=1;var p=glass.Position;shadow.Position=new(p.X,shadow.Position.Y,p.Z);float scale=item.GetProperty("scale").GetSingle();shadow.Scale=new(scale,shadow.Scale.Y,scale);}}
}
}
