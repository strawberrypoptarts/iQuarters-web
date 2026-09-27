using SceneKit;
using CoreGraphics;
using IQuarters.Core;
namespace IQuarters.iOS;

// Only anchor wrappers move. Original animation curves keep their authored coordinates.
public sealed partial class GameViewController
{
    readonly Dictionary<string,SCNNode> anchors=[];
    void Anchor(string name,float x,float y)
    {
        if(!anchors.TryGetValue(name,out var wrapper)){
            var root=ui.Find(name);if(root==null)return;
            wrapper=new SCNNode{Name="Screen anchor: "+name};root.ParentNode!.AddChildNode(wrapper);root.RemoveFromParentNode();wrapper.AddChildNode(root);anchors[name]=wrapper;
        }
        wrapper.Position=new(x,y,0);
    }
    void LayoutGame()
    {
        LayoutOriginalLabels();
        double w=(double)View!.Bounds.Width,h=(double)View.Bounds.Height;if(w<=0||h<=0)return;
        double half=PresentationRules.HalfHeight(w,h),halfWidth=half*w/h,unit=2*half/h;
        var safe=View.SafeAreaInsets;
        float top=(float)(half-100-(double)safe.Top*unit),bottom=(float)(-half+100+(double)safe.Bottom*unit);
        float left=(float)(halfWidth-200d/3-(double)safe.Left*unit),right=(float)(-halfWidth+200d/3+(double)safe.Right*unit);
        float center=(float)(((double)safe.Right-(double)safe.Left)*unit/2);
        overlay.PointOfView!.Camera!.OrthographicScale=half;
        // The original pause BackDrop is separate from the animated menu background child.
        ui.Nodes[1825].Scale=new(33*(float)Math.Max(1,(w/h)/(2d/3)),33,33*(float)(half/100));
        camera.Camera!.FieldOfView=(nfloat)PresentationRules.VerticalFieldOfView(w,h,usingReplayCamera?replayFieldOfView:UIDevice.CurrentDevice.UserInterfaceIdiom==UIUserInterfaceIdiom.Pad?70:55);
        Anchor("ui_ingame_3coin_hold",left,top);Anchor("ui_ingame_3coin_root",left,top);
        Anchor("ui_ingame_angle_root",right,top);Anchor("ex_round_mon",right,top);
        Anchor("UI_pause",right,bottom);Anchor("UI_ingame_player",center,top);
        Anchor("exciter_in_a_row",center,top);Anchor("RicochetParent",center,bottom);
        Anchor("ui_richochet_score",center,bottom);
        var scale=View.Window?.Screen.NativeScale??UIScreen.MainScreen.NativeScale;
        display.ContentScaleFactor=scale;overlay.ContentScaleFactor=scale;
    }
    int nextReplayCamera=3;
    bool usingReplayCamera;
    SCNVector3 savedCameraPosition;SCNQuaternion savedCameraOrientation;
    float replayDamping=2; double replayFieldOfView=55;
    void UseReplayCamera()
    {
        soundCounts.Clear();lastSoundType=-1;lastSoundTime=-10;
        if(!usingReplayCamera){savedCameraPosition=camera.Position;savedCameraOrientation=camera.Orientation;}
        int selected=PresentationRules.ReplayCamera(replayRound,nextReplayCamera);
        nextReplayCamera=selected==3?1:selected+1;
        int owner=physics.GetProperty("quarterFields").GetProperty(selected==1?"replayCamObject":$"replayCamObject{selected}")[1].GetInt32();
        var source=game.Nodes[owner];camera.Position=source.WorldPosition;
        var q=source.Orientation;camera.Orientation=new(-q.X,-q.Y,q.Z,q.W);
        var data=presentation.GetProperty("cameras").EnumerateArray().First(c=>c.GetProperty("owner").GetInt32()==owner);
        replayDamping=data.GetProperty("follow").GetProperty("damping").GetSingle();
        camera.Camera!.ZNear=data.GetProperty("near").GetSingle();camera.Camera.ZFar=data.GetProperty("far").GetSingle();
        replayFieldOfView=data.GetProperty("fov").GetDouble();usingReplayCamera=true;LayoutGame();
    }
    void RestoreGameCamera()
    {
        if(!usingReplayCamera)return;
        camera.Position=savedCameraPosition;camera.Orientation=savedCameraOrientation;camera.Camera!.ZNear=.05;camera.Camera.ZFar=300;usingReplayCamera=false;LayoutGame();
    }
    void FollowReplayCamera(float dt)
    {
        var previous=camera.Orientation;camera.Look(coin.WorldPosition,new SCNVector3(0,1,0),new SCNVector3(0,0,-1));var target=camera.Orientation;
        var q=System.Numerics.Quaternion.Slerp(new(previous.X,previous.Y,previous.Z,previous.W),new(target.X,target.Y,target.Z,target.W),CameraFollow.Blend(replayDamping/60,dt));camera.Orientation=new(q.X,q.Y,q.Z,q.W);
    }
}
