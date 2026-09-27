using System.Text.Json;
using SceneKit;
using CoreGraphics;
using IQuarters.Core;
using AVFoundation;
using CoreMotion;
using NVector3=System.Numerics.Vector3;
namespace IQuarters.iOS;

public sealed partial class GameViewController : UIViewController
{
    readonly CMMotionManager motion=new();double lastMotionTime;
    readonly GameSession session;readonly bool selectAtStart;public Action? Quit;
    LegacyScene game=null!,ui=null!;SCNView display=null!,overlay=null!;SCNNode coin=null!;
    List<Collider> activeColliders=[];
    readonly List<int[]> rounds=[];readonly List<Collider> colliders=[];readonly List<ReplayFrame> recording=[];List<ReplayFrame> replay=[];readonly List<int> frameContacts=[];
    readonly record struct NodePose(int Id,SCNVector3 Position,SCNQuaternion Orientation,SCNVector3 Scale);
    sealed record ReplayFrame(float Time,NodePose[] Poses,int Ricochets=0,int[]? Contacts=null);
    NodePose[] beforeReplay=[];int replayRound,returnRound;float replayTime,cameraT;SCNNode camera=null!;CoinBody coinBody=null!;JsonElement physics,presentation;
    readonly Dictionary<int,int> soundCounts=[];float lastSoundTime=-10;int lastSoundType=-1;
    readonly Dictionary<string,SCNMaterial> digits=[];
    sealed class Collider {public int[] Ancestors=[];public SCNNode Node=null!;public CollisionShape Shape=null!;public SCNVector3[][] Local=[];public SCNMatrix4 LastTransform;public bool HasTransform,Moving;}
    readonly UIView textLayer=new(){UserInteractionEnabled=false};
    NVector3 start;SCNQuaternion orientation;DisplayFrameLoop? frameLoop;NSObject? background,foreground;GameAudio? audio;ShotSimulation? shot;
    string pauseResumeState="play",state="busy",hitSound="";int round,replayFrame;float delay;float angle {get=>session.shotAngle;set=>session.shotAngle=value;}bool angleInput,shake;CGPoint lastTouch,angleTouch;readonly TimedFlickGesture flick=new();double lastTouchTimestamp;bool adjustingAngle;
    public GameViewController(GameSession session,bool selectRound){this.session=session;selectAtStart=selectRound;}
    bool prepared;
#if BROWSER
    public async Task PrepareAsync(){ await Task.Yield(); Prepare(); }
    void Prepare(){
#else
    public Task PrepareAsync()=>Task.Run(()=>{
#endif
        // No views are created here. Scene graphs are private until this task completes.
        using var pool=new NSAutoreleasePool();
        var assets=new LegacyAssetCache();
        game=new("scene.json",recoveredLighting:true,cache:assets);game.World.Scale=new(1,1,-1);game.HideRoots();game.RestoreLighting();
        ui=new("scene.json",true,cache:assets);ui.HideRoots();
        var scene=assets.Read("scene.json");
        foreach(var r in scene.GetProperty("rounds").EnumerateArray())rounds.Add(r.EnumerateArray().Select(x=>x.GetInt32()).ToArray());
        foreach(var name in new[]{"table_00","scene_bar_00","a_quarter5"})game.Show(name,false);
        coin=game.Nodes[scene.GetProperty("quarterObject").GetInt32()];start=new(coin.Position.X,coin.Position.Y,coin.Position.Z);orientation=coin.Orientation;
        string physicsText=File.ReadAllText(LegacyScene.Resource("physics.json"));coinBody=CoinBody.Read(physicsText);using(var pd=JsonDocument.Parse(physicsText))physics=pd.RootElement.Clone();
        using var collisions=JsonDocument.Parse(File.ReadAllText(LegacyScene.Resource("collision.json")));
        foreach(var c in collisions.RootElement.EnumerateArray()) {
            var ids=c.GetProperty("ancestors").EnumerateArray().Select(x=>x.GetInt32()).ToArray();var node=game.Nodes[c.GetProperty("owner").GetInt32()];
            var triangles=c.GetProperty("triangles").EnumerateArray().Select(t=>t.EnumerateArray().Select(v=>new NVector3(v[0].GetSingle(),v[1].GetSingle(),v[2].GetSingle())).ToArray()).ToArray();
            var reaction=c.GetProperty("reaction");
            var shape=new CollisionShape {Owner=c.GetProperty("body").GetInt32(),ColliderId=c.GetProperty("collider").GetInt32(),Material=SurfaceMaterial.Read(c.GetProperty("material")),ReactionOwner=c.GetProperty("reactionOwner").GetInt32(),LaunchVelocity=reaction.ValueKind==JsonValueKind.Null?null:CoinBody.ReadVector(reaction.GetProperty("velocity")),LaunchNormalThreshold=reaction.ValueKind==JsonValueKind.Null?.707f:reaction.GetProperty("normalThreshold").GetSingle(),ExtraShotTime=reaction.ValueKind==JsonValueKind.Null?0:reaction.GetProperty("extraTime").GetSingle(),Multiplier=c.GetProperty("multiplier").GetInt32(),Triangles=triangles,SurfaceVelocities=triangles.Select(_=>new NVector3[3]).ToArray()};shape.Prepare();
            colliders.Add(new(){Ancestors=ids,Node=node,Shape=shape,Local=triangles.Select(t=>t.Select(p=>node.ConvertPositionFromNode(new(p.X,p.Y,p.Z),game.World)).ToArray()).ToArray()});
        }
        using var sounds=JsonDocument.Parse(File.ReadAllText(LegacyScene.Resource("audio/manifest.json")));hitSound=sounds.RootElement.EnumerateArray().First(v=>v.GetProperty("name").GetString()=="coin_table_hit1").GetProperty("file").GetString()!;
        presentation=assets.Read("presentation.json");
        audio=new GameAudio();session.SecretRoundUnlocked+=()=>EffectSound(580);prepared=true;
#if BROWSER
    }
#else
    });
#endif
    public async Task PrepareRenderingAsync(){var ready=await Task.WhenAll(display.PrepareAsync([game.Scene]),overlay.PrepareAsync([ui.Scene]));if(ready.Any(ok=>!ok))throw new InvalidOperationException("Scene resources could not be prepared.");}
    public void ReleasePreparedResources(){
        frameLoop?.Dispose();frameLoop=null;motion.StopAccelerometerUpdates();
        if(background!=null){NSNotificationCenter.DefaultCenter.RemoveObserver(background);background=null;}
        if(foreground!=null){NSNotificationCenter.DefaultCenter.RemoveObserver(foreground);foreground=null;}
        audio?.Dispose();audio=null;
#if BROWSER
        WebBridge.ReleaseScenes(game?.Scene,ui?.Scene);
#endif
    }
    public override bool CanBecomeFirstResponder=>true;
    public override bool PrefersStatusBarHidden()=>true;
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();View!.BackgroundColor=UIColor.Black;
        if(!prepared)throw new InvalidOperationException("PrepareAsync must finish before presenting gameplay.");
        camera=new SCNNode {Camera=new SCNCamera {FieldOfView=55,ZNear=.05,ZFar=300},Position=new(0,4.815438f,6.119505f),EulerAngles=new(-.5585f,0,0)};game.Scene.RootNode.AddChildNode(camera);
        display=new SCNView(View.Bounds){Scene=game.Scene,PointOfView=camera,AutoenablesDefaultLighting=false,BackgroundColor=UIColor.Black,AutoresizingMask=UIViewAutoresizing.FlexibleDimensions};View.AddSubview(display);
        var uiCamera=new SCNNode {Camera=new SCNCamera {UsesOrthographicProjection=true,OrthographicScale=100,ZNear=.3,ZFar=1100},Position=new(0,0,1000)};ui.Scene.RootNode.AddChildNode(uiCamera);
        overlay=new TouchSceneView(View.Bounds){Scene=ui.Scene,PointOfView=uiCamera,Opaque=false,BackgroundColor=UIColor.Clear,AutoresizingMask=UIViewAutoresizing.FlexibleDimensions};View.AddSubview(overlay);
        textLayer.Frame=View.Bounds;textLayer.AutoresizingMask=UIViewAutoresizing.FlexibleDimensions;View.AddSubview(textLayer);
        overlay.AddGestureRecognizer(new UITapGestureRecognizer(g=>Tap(g.LocationInView(overlay))){CancelsTouchesInView=false});
        var touchView=(TouchSceneView)overlay;
        touchView.BeginContact=(p,timestamp)=>{
            flick.Cancel();lastTouch=angleTouch=p;lastTouchTimestamp=timestamp;adjustingAngle=false;
            if(state!="play"||shot!=null||Hit(p)!="")return;
            if(angleInput){adjustingAngle=true;return;}
            if(!shake)flick.Begin(timestamp);
        };
        touchView.MoveContact=(p,timestamp)=>{
            if(timestamp<=lastTouchTimestamp)return;lastTouchTimestamp=timestamp;
            float scale=PresentationRules.TouchScale((double)overlay.Bounds.Width);
            if(state!="play"||shot!=null){flick.Cancel();return;}
            if(adjustingAngle){int change=(int)MathF.Floor((float)(angleTouch.Y-p.Y)*scale*.075f);if(change!=0){angle=Math.Clamp(angle-change,45,55);angleTouch=p;Digits("deg_",(int)angle,2);}return;}
            var delta=new System.Numerics.Vector2((float)(p.X-lastTouch.X),(float)(lastTouch.Y-p.Y));lastTouch=p;
            if(flick.Sample(delta*scale,timestamp,out float power,out float aim))Shoot(power,aim);
        };
        touchView.EndContact=()=>{if(flick.End(out float power,out float aim))Shoot(power,aim);adjustingAngle=false;};
        touchView.CancelContact=()=>{flick.Cancel();adjustingAngle=false;};
        background=NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.WillResignActiveNotification,_=>{audio?.Pause();Pause();GameStorage.Save(session);});
        foreground=NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.DidBecomeActiveNotification,_=>audio?.Resume());
        InitializeContactFlashes();game.PlayAutomatic();ShowRound(session.curRound);if(selectAtStart)Practice();else BeginRound();
    }
    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();if(overlay==null)return;LayoutGame();
    }
    public override void ViewDidAppear(bool animated){base.ViewDidAppear(animated);BecomeFirstResponder();audio??=new GameAudio();audio.Resume();if(motion.AccelerometerAvailable){motion.AccelerometerUpdateInterval=.02;motion.StartAccelerometerUpdates();}frameLoop?.Dispose();frameLoop=new(View!.Window?.Screen??UIScreen.MainScreen,Tick,display,overlay);}
    public override void ViewDidDisappear(bool animated){base.ViewDidDisappear(animated);ReleasePreparedResources();}

    bool Visible(SCNNode n){for(SCNNode? a=n;a!=null;a=a.ParentNode)if(a.Hidden||a.Opacity<.01)return false;return true;}
    string Hit(CGPoint p)=>overlay.HitTest(p,new SCNHitTestOptions {IgnoreHiddenNodes=true}).Where(h=>Visible(h.Node)).Select(h=>h.Node.Name??"").FirstOrDefault(n=>n.StartsWith("button_")||n is "sound_off" or "hall_of_fame" or "help" or "go" or "quit" or "select_left" or "select_right" or "stats_done")??"";
    void Tap(CGPoint point)
    {
        string name=Hit(point);
        if(state=="help"){AdvanceRules();return;}
        if(state=="replay"&&afterAutomaticReplay!=null){replaySkipped=true;replayTime=replay[^1].Time+1;return;}
        if(state=="replayDone"){if(name=="button_hs_done")CloseReplay();return;}
        if(state=="highScores"){if(name=="button_hs_done")CloseInGameScores();return;}
        if(state=="scores"){ClearText();state="pause";return;}
        if(state=="stats"){if(name=="stats_done")CloseStats();return;}
        if(name=="")return;Sound("sharedassets0.assets-131.wav");
        if(state=="select") {
            if(name=="quit"){state="selectTransition";ui.Find("ui_practice_lock")!.Hidden=true;UiSequence(1106,["quitclick","slideout"],()=>Quit?.Invoke());return;}
            if(name is "select_left" or "select_right") {int direction=name=="select_left"?-1:1;state="selectTransition";ui.Play(1106,direction<0?"leftclick":"rightclick",()=>{ShowRound((round+direction+12)%12);state="select";PracticeLock();});}
            else if(name=="go"&&round<GameStorage.Unlocked){session.curRound=round;session.ClearScores();session.SetAllShotsLeft(40);session.curMadeShotsThisRound=session.curShotThisRound=0;angle=50;state="selectTransition";ui.Find("ui_practice_lock")!.Hidden=true;UiSequence(1106,["goclick","slideout"],BeginRound);}return;
        }
        if(state=="pause") {
            if(name=="button_done"){session.StoreSecretRoundUnlockCode(0);LeavePause("doneclick",()=>{SetPauseBackdrop(false);state=pauseResumeState;if(state=="play"&&shot==null)ShowShotHud();});}
            else if(name=="button_quit"){session.StoreSecretRoundUnlockCode(6);LeavePause("quitclick",()=>{GameStorage.ClearSaved();Quit?.Invoke();});}
            else if(name=="sound_off"){GameStorage.Muted=!GameStorage.Muted;session.StoreSecretRoundUnlockCode(GameStorage.Muted?1:2);ui.Play(1882,"soundclick");Texture(ui.Find("UI_backend_00")!.ChildNodes.First(n=>n.Name=="sound_off"),GameStorage.Muted?18:24);}
            else if(name=="button_replay"&&replay.Count>0&&shot==null){session.StoreSecretRoundUnlockCode(5);LeavePause("flickclick",RequestedReplay);}
            else if(name=="help"){session.StoreSecretRoundUnlockCode(3);OpenRules();}
            return;
        }
        if(state is "play" or "wait" or "effects") {
            if(name=="button_pause"&&state=="play"&&shot==null&&!angleInput){session.ResetUserSecretCodes();state="pauseClick";ui.PlayDefault(1835,()=>{state="play";Pause();});}
            else if(state=="play"&&shot==null&&name is "button_angle" or "button_done2") {angleInput=!angleInput;shotHud.PressAngle();shotHud.HolderVisible(!angleInput);var done=ui.Find("UI_ingame_angle")!.ChildNodes.First(n=>n.Name=="button_done2");done.Opacity=angleInput?1:0;}
        }
    }
    void Texture(SCNNode n,int id){if(n.Geometry!=null){n.Geometry.FirstMaterial=(SCNMaterial)n.Geometry.FirstMaterial!.Copy();n.Geometry.FirstMaterial.Diffuse.Contents=UIImage.FromFile(LegacyScene.Resource($"textures/sharedassets1.assets-{id}.png"));}}
    float pauseIntroElapsed;bool pauseBackdropPending;
    void SetPauseBackdrop(bool visible){var backdrop=ui.Nodes[1825];backdrop.Hidden=!visible;backdrop.Opacity=visible?1:0;if(!visible)pauseBackdropPending=false;}
    void Pause(){if(state is not ("play" or "effects" or "replay"))return;flick.Cancel();pauseResumeState=state;HideShotHud();state="pause";pauseIntroElapsed=0;pauseBackdropPending=true;ui.Show("UI_backend_00");ui.Play(ui.Id("UI_backend_00"),"animin");Texture(ui.Find("UI_backend_00")!.ChildNodes.First(n=>n.Name=="sound_off"),GameStorage.Muted?18:24);Texture(ui.Find("UI_backend_00")!.ChildNodes.First(n=>n.Name=="button_replay"),replay.Count>0&&shot==null?69:17);ui.Show("ui_help");foreach(var n in ui.Find("ui_help")!.ChildNodes)n.Opacity=n.Name=="help"?1:0;}
    void ClearText(){originalLabels.Clear();foreach(var v in textLayer.Subviews)v.RemoveFromSuperview();}
    void Text(string value,float top,float height){ClearText();var l=new UILabel(new CGRect(20,View!.Bounds.Height*top,View.Bounds.Width-40,View.Bounds.Height*height)){Text=value,TextColor=UIColor.White,BackgroundColor=UIColor.FromWhiteAlpha(0,.8f),TextAlignment=UITextAlignment.Center,Lines=0,Font=UIFont.BoldSystemFontOfSize(18)!,AdjustsFontSizeToFitWidth=true};textLayer.AddSubview(l);}
    void Practice(){state="selectTransition";ClearText();ui.HideRoots();coin.Hidden=true;ui.Show("ui_practice_mode");ui.Play(1106,"slidein",()=>{state="select";PracticeLock();});}
    void PracticeLock(){ClearText();Label($"Hi Score  {GameStorage.RoundScore(round)}",90,46,180,40);var locked=ui.Find("ui_practice_lock")!;if(round>=GameStorage.Unlocked){ui.Show("ui_practice_lock");ui.Play(1254,"lockin");}else locked.Hidden=true;}
    void BeginRound()
    {
        ClearText();ui.HideRoots();foreach(var name in new[]{"UI_pause","UI_ingame_player","ui_ingame_3coin_hold","ui_ingame_angle_root","ex_round_mon"})ui.Show(name);
        InitializeShotHud();
        ui.Sample(ui.Id("UI_pause"),"Take 001",1);ui.Nodes[1829].Opacity=0;ui.Sample(ui.Id("UI_ingame_angle"),"hold",0);
        foreach(var n in ui.Find("UI_ingame_angle")!.ChildNodes)if(n.Name is "button_done" or "button_done2")n.Opacity=0;
        ui.Play(ui.Id("UI_ingame_player"),$"player{session.curPlayer+1}");
        angleInput=false;shake=session.GetCurrentInputType()==0;coin.Hidden=false;ResetCoin();Hud();
        if(!session.IsPractice)PlayExciter(1187,shake?"shake":"flick");
        state="effects";After(.5f,()=>{state="play";ShowShotHud();});GameStorage.Save(session);
    }
    void Hud()
    {
        Digits("score_",Math.Clamp(session.GetCurrentScore(),0,9999),4);Digits("coins_",session.GetCurrentShotsLeft(),2);Digits("deg_",(int)angle,2);
        var holder=ui.Find("ui_ingame_3coin_hold")!;for(int i=1;i<=3;i++)holder.ChildNodes.First(n=>n.Name==$"quarter_card_0{i}").Opacity=i<=session.curMadeShotsThisRound?1:0;
        var banner=ui.Find("ex_round_mon")!;foreach(var n in banner.ChildNodes)if(int.TryParse(n.Name,out _)||n.Name is "secret" or "round_move_marker")n.Opacity=n.Name==(round==12?"secret":$"{round+1:00}")?1:0;
    }
    void Digits(string prefix,int value,int count)
    {
        for(int i=1;i<=count;i++){string name=prefix+$"{i:00}";var n=ui.Find(name);if(n?.Geometry==null)continue;
            if(!digits.TryGetValue(name,out var material)){material=(SCNMaterial)n.Geometry.FirstMaterial!.Copy();n.Geometry.FirstMaterial=material;digits[name]=material;}
            int divisor=(int)Math.Pow(10,count-i);n.Opacity=i==count||value>=divisor?1:0;material.Diffuse.ContentsTransform=SCNMatrix4.CreateTranslation((value/divisor%10)/16f,0,0);
        }
    }
    void ShowRound(int index)
    {
        foreach(int id in rounds.SelectMany(r=>r).Distinct())game.Nodes[id].Hidden=true;round=index;
        foreach(int id in rounds[round]){LegacyScene.Activate(game.Nodes[id]);for(SCNNode? n=game.Nodes[id];n!=null;n=n.ParentNode)n.Hidden=false;}
        var ids=rounds[round].ToHashSet();activeColliders=colliders.Where(c=>c.Ancestors.Any(id=>ids.Contains(id)||game.Nodes[id].Name is "table_00" or "TableTop")).ToList();
        game.ShowGlassShadows(round);CacheReplayNodes();
    }
    void MoveColliders(float dt=0)
    {
        foreach(var c in activeColliders){
            var transform=c.Node.ConvertTransformToNode(SCNMatrix4.Identity,game.World);
            if(c.HasTransform&&transform.Equals(c.LastTransform)){
                if(c.Moving){foreach(var v in c.Shape.SurfaceVelocities!)Array.Clear(v);c.Moving=false;}
                continue;
            }
            // One native matrix conversion per object, then managed vector transforms.
            for(int i=0;i<c.Local.Length;i++)for(int j=0;j<3;j++){
                var p=SCNVector3.TransformPosition(c.Local[i][j],transform);var next=new NVector3(p.X,p.Y,p.Z);
                c.Shape.SurfaceVelocities![i][j]=dt>0?(next-c.Shape.Triangles[i][j])/dt:NVector3.Zero;c.Shape.Triangles[i][j]=next;
            }
            c.Shape.Prepare();c.LastTransform=transform;c.HasTransform=true;c.Moving=dt>0;
        }
    }
    void Shoot(float power,float aim)
    {
        if(state!="play"||shot!=null||angleInput)return;MoveColliders();shot=new(start,power,angle,aim,activeColliders.Select(c=>c.Shape).ToList(),body:coinBody);shot.ReactionTriggered+=React;shot.ContactEntered+=(shape,point)=>{frameContacts.Add(shape.ColliderId);CollisionSound(shape);ContactFlash(point);};recording.Clear();recording.Add(new(0,Capture()));soundCounts.Clear();lastSoundType=-1;lastSoundTime=-10;HideShotHud();Sound("sharedassets1.assets-580.wav");
    }
    void Tick(float dt)
    {
        shotHud.Tick(dt);ApplyShotHud();
        if(state is "pause" or "help" or "scores" or "pauseTransition"){ui.Tick(dt);if(pauseResumeState=="play"&&shot==null)game.Tick(dt);if(pauseBackdropPending){pauseIntroElapsed+=dt;if(pauseIntroElapsed>.25f){SetPauseBackdrop(true);pauseBackdropPending=false;}}return;}
        if(state is "stats" or "names" or "statsTransition" or "highScores" or "scoreTransition" or "replayDone" or "replayDoneTransition"){ui.Tick(dt);return;}
        ui.Tick(dt);
        if(state=="replay"){
            replayTime+=dt;while(replayFrame+1<replay.Count&&replay[replayFrame+1].Time<=replayTime){replayFrame++;foreach(int id in replay[replayFrame].Contacts??[]){CollisionSound(colliders.FirstOrDefault(c=>c.Shape.ColliderId==id)?.Shape,replay[replayFrame].Time);var pose=replay[replayFrame].Poses.FirstOrDefault(p=>p.Id==game.Id("a_quarter5"));ContactFlash(new NVector3(pose.Position.X,pose.Position.Y,pose.Position.Z));}}
            Restore(replay[replayFrame].Poses);UpdateCameraAndShadow(dt);TickParity(dt);
            if(afterAutomaticReplay!=null)ReplayContactEffect(replay[replayFrame].Ricochets);
            if(replayTime>replay[^1].Time)FinishReplay();return;
        }
        game.Tick(dt);UpdateCameraAndShadow(dt);TickEffects(dt);TickParity(dt);
        if(state is "select" or "selectTransition"){game.Nodes[1962].Hidden=true;return;}
        if(state=="practiceResult"){delay-=dt;if(delay<=0)Practice();return;}
        ApplyShotHud();
        if(state=="effects")return;

        if(shot==null&&state=="play"&&shake&&!angleInput&&motion.AccelerometerData is {} sample&&sample.Timestamp>lastMotionTime){lastMotionTime=sample.Timestamp;var a=sample.Acceleration;if(ShakeGesture.Sample(new((float)a.X,(float)a.Y,(float)a.Z),out var power,out var aim))Shoot(power,aim);}
        if(shot==null)return;frameContacts.Clear();MoveColliders(dt);shot.Advance(dt);var p=shot.Position;coin.Position=new(p.X,p.Y,p.Z);var rotation=shot.Orientation;coin.Orientation=new(rotation.X,rotation.Y,rotation.Z,rotation.W);recording.Add(new(shot.Elapsed,Capture(),shot.Ricochets,frameContacts.ToArray()));
        if(!shot.Finished)return;
        var finished=shot;replay=new(recording);replayRound=round;shot=null;
        CelebrateShot(finished);
    }
    void CommitShot(int multiplier)
    {
        int oldPlayer=session.curPlayer,oldRound=round;var player=(PlayerInfoClass)session.playerData[oldPlayer]!;
        int roundScore=player.roundScore;bool newHigh=roundScore>GameStorage.RoundScore(round);
        if(session.IsPractice){
            session.curShotThisRound++;if(multiplier>0)session.curMadeShotsThisRound++;else session.DecrementCurrentShotsLeft();
            if(session.GetCurrentShotsLeft()==0){GameOverEffect(true,Practice);return;}
            if(session.curMadeShotsThisRound>=3){
                // Android's practice "great score" is informational; it does not write Classic round records.
                if(newHigh)PracticeScoreEffect(roundScore,Practice);else Practice();return;
            }
            ResetCoin();state="play";Hud();ShowShotHud();return;
        }
        int flags=session.UpdateShotCount(multiplier);
        bool ended=(flags&session.flagPlayerGameOver)!=0,allEnded=(flags&session.flagGameOver)!=0;
        bool outOfCoins=(flags&session.flagOutOfShotsReason)!=0;
        bool completedRound=multiplier>0&&flags!=0;
        if(completedRound){GameStorage.RecordRound(oldRound,roundScore,player.playerName);GameStorage.Unlocked=Math.Max(GameStorage.Unlocked,Math.Min(12,oldRound+1));}
        if(ended&&!outOfCoins)session.AddPlayerScore(player.shotsLeft*5,oldPlayer);
        if(ended&&player.recoveredScoreId=="")player.recoveredScoreId=GameStorage.AddScore(player.playerName,player.score);
        if(allEnded)GameStorage.ClearSaved();else GameStorage.Save(session);
        void ContinueGame(){
            SetPauseBackdrop(false);
            if(allEnded){Stats();return;}
            if(flags!=0){angle=50;ShowRound(session.curRound);BeginRound();}
            else {ResetCoin();state="play";Hud();ShowShotHud();}
        }
        void AfterRound(){
            if(ended){GameOverEffect(outOfCoins,()=>StackBonus(player.shotsLeft,()=>PlayerHighScore(oldPlayer,ContinueGame)));}
            else ContinueGame();
        }
        if(completedRound){
            shotHud.Hide();
            if(oldRound==12)SecretRoundEffect(false,AfterRound);
            else if(oldRound==8&&session.secretRoundUnlocked)SecretRoundEffect(true,AfterRound);
            else RoundCompleteEffect(roundScore,newHigh,AfterRound);
        }else if(ended)AfterRound();else ContinueGame();
    }
    void Stats()
    {
        state="statsTransition";GameStorage.ClearSaved();ClearText();ui.HideRoots();SetPauseBackdrop(true);ui.Show("ui_stats");
        ui.Play(128,$"ui_in_{session.totPlayers}",()=>{state="stats";DrawStats();});
    }
    void ResetCoin(){coin.Position=new(start.X,start.Y,start.Z);coin.Orientation=orientation;FollowMainCamera(1,true);}
    void Sound(string file){if(!GameStorage.Muted)audio?.Play(file);}
}
