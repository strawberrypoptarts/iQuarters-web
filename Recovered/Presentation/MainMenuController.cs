using SceneKit;
using CoreGraphics;
using AVFoundation;
using IQuarters.Core;
namespace IQuarters.iOS;

public sealed class MainMenuController : UIViewController
{
    LegacyScene legacy=null!;SCNView display=null!;DisplayFrameLoop? frameLoop;AVAudioPlayer? audio;
    readonly UIView textLayer=new();
    UILabel? loadingLabel;
    string screen="busy";bool roundScores;
    int Main=>legacy.Id("ui_main_menu_00");
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();View!.BackgroundColor=UIColor.Black;
        legacy=new LegacyScene("frontend.json",true);legacy.HideRoots();
        var camera=new SCNNode {Position=new SCNVector3(0,0,1000),Camera=new SCNCamera {UsesOrthographicProjection=true,OrthographicScale=100,ZNear=.3,ZFar=1100}};legacy.Scene.RootNode.AddChildNode(camera);
        display=new SCNView(View.Bounds){Scene=legacy.Scene,PointOfView=camera,BackgroundColor=UIColor.Black,AutoresizingMask=UIViewAutoresizing.FlexibleDimensions};View.AddSubview(display);
        textLayer.Frame=View.Bounds;textLayer.AutoresizingMask=UIViewAutoresizing.FlexibleDimensions;textLayer.UserInteractionEnabled=false;View.AddSubview(textLayer);
        display.AddGestureRecognizer(new UITapGestureRecognizer(g=>Tap(g.LocationInView(display))));
        Home(true);
    }
    public override void ViewDidAppear(bool animated){base.ViewDidAppear(animated);frameLoop?.Dispose();frameLoop=new(View!.Window?.Screen??UIScreen.MainScreen,dt=>legacy.Tick(dt),display);}
    public override void ViewDidDisappear(bool animated){base.ViewDidDisappear(animated);frameLoop?.Dispose();frameLoop=null;}
    public override void ViewDidLayoutSubviews(){base.ViewDidLayoutSubviews();if(display==null||View!.Bounds.Width<=0||View.Bounds.Height<=0)return;double scale=Math.Max(100,66.666667*(double)View!.Bounds.Height/(double)View.Bounds.Width);display.ContentScaleFactor=View.Window?.Screen.NativeScale??UIScreen.MainScreen.NativeScale;display.PointOfView!.Camera!.OrthographicScale=scale;var backdrop=legacy.Find("backdrop");if(backdrop!=null){backdrop.Scale=new(33*(float)Math.Max(1,((double)View.Bounds.Width/(double)View.Bounds.Height)/(2d/3)),33,33*(float)(scale/100));}if(screen=="scores")ScoreText();if(screen=="about")DrawAbout();if(loadingLabel!=null)LayoutLoading();}
    void Sound(string file){if(GameStorage.Muted)return;audio?.Stop();audio?.Dispose();audio=AVAudioPlayer.FromUrl(NSUrl.FromFilename(LegacyScene.Resource("audio/"+file)));audio?.Play();}
    void ClearText(){foreach(var child in textLayer.Subviews)child.RemoveFromSuperview();}
    void Text(string text,CGRect rect,double size=18)
    {
        var label=new UILabel(rect){Text=text,TextColor=UIColor.White,TextAlignment=UITextAlignment.Center,Lines=0,Font=UIFont.BoldSystemFontOfSize((nfloat)size)!,AdjustsFontSizeToFitWidth=true,MinimumScaleFactor=.6f};label.Layer.ShadowColor=UIColor.Black.CGColor;label.Layer.ShadowOpacity=1;label.Layer.ShadowOffset=new CGSize(1,2);textLayer.AddSubview(label);
    }
    void Home(bool intro=false)
    {
        screen="busy";ClearText();legacy.HideRoots();legacy.Show("backdrop");legacy.Show("ui_main_menu_00");legacy.Show("ui_about");
        foreach(var n in legacy.Nodes.Values)if(n.Name is "dimplane" or "about_text"||n.Name?.StartsWith("high_score_bg_")==true)n.Opacity=0;
        var about=legacy.Find("itme");if(about!=null)about.Opacity=1;
        legacy.Play(Main,"intro",()=>{screen="main";if(intro&&GameStorage.Load()!=null)ResumePrompt();});
        if(intro)Sound("sharedassets0.assets-130.wav");
    }
    void Sequence(string[] clips,Action complete)
    {
        screen="busy";void Next(int i){if(i==clips.Length){complete();return;}legacy.Play(Main,clips[i],()=>Next(i+1));}Next(0);
    }
    void ResumePrompt()
    {
        legacy.Show("ui_resume");foreach(var c in legacy.Find("ui_resume")!.ChildNodes)c.Opacity=1;screen="resume";
    }
    static bool Visible(SCNNode node){for(SCNNode? n=node;n!=null;n=n.ParentNode)if(n.Hidden||n.Opacity<.01)return false;return true;}
    public override bool PrefersStatusBarHidden()=>true;
    void Tap(CGPoint point)
    {
        if(screen=="busy")return;
        var hits=display.HitTest(point,new SCNHitTestOptions {IgnoreHiddenNodes=true});
        string name=hits.Where(h=>Visible(h.Node)).Select(h=>h.Node.Name??"").FirstOrDefault(n=>n.StartsWith("button_")||n is "itme" or "yes" or "no")??"";
        if(screen=="about"){Home();return;}
        if(name=="")return;Sound("sharedassets0.assets-131.wav");
        if(screen=="resume") {if(name is "yes" or "no"){screen="busy";legacy.Play(legacy.Id("ui_resume"),name=="yes"?"YesClick":"NoClick",()=>{legacy.Find("ui_resume")!.Hidden=true;if(name=="yes"){var saved=GameStorage.Load();if(saved!=null){Start(saved,false);return;}}else GameStorage.ClearSaved();screen="main";});}return;}
        if(screen=="clear") {if(name is "yes" or "no"){screen="busy";legacy.Play(legacy.Id("ui_are_you_sure"),name=="yes"?"YesClick":"NoClick",()=>{if(name=="yes")GameStorage.ClearScores(roundScores);legacy.Find("ui_are_you_sure")!.Hidden=true;screen="scores";ScoreText();});}return;}
        if(screen=="main") {
            if(name=="button_playnow"){legacy.Find("ui_about")!.Hidden=true;Sequence(["playnowclick","playnowout","gtin"],()=>{screen="type";SetPracticeTexture();});}
            else if(name=="button_highscore"){legacy.Find("ui_about")!.Hidden=true;Sequence(["hiscoreclick","playnowout"],Scores);}
            else if(name=="itme"){Sequence(["playnowout"],About);}
        } else if(screen=="type") {
            if(name=="button_classic")Sequence(["gtclassicclick","gtout","npin"],()=>screen="players");
            else if(name=="button_practice"&&GameStorage.Unlocked>=2)Sequence(["gtpracticeclick","gtpracticeout"],()=>Start(new GameSession(1,true),true));
            else if(name is "button_button_left" or "button_right_bk")Sequence(["gtbackclick","gtout"],()=>Home());
        } else if(screen=="players") {
            if(name.StartsWith("button_player_")&&int.TryParse(name[^2..],out int count)) {
                string[] clips=["nponeplayer","nptwoplayer","npthreeplayer","npfourplayer"];
                Sequence([clips[count-1],"npout"],()=>Start(new GameSession(count),false));
            }else if(name is "button_button_left" or "button_right_bk")Sequence(["npbackclick","npout","gtin"],()=>screen="type");
        } else if(screen=="scores") {
            if(name=="button_back_hs")ExitScores();
            else if(name=="button_clear_hs"){ClearText();legacy.PlayRange(legacy.Id("ui_back_clear"),"Take 001",40f/30,45f/30);legacy.Show("ui_are_you_sure");foreach(var n in legacy.Find("ui_are_you_sure")!.ChildNodes)n.Opacity=1;screen="clear";}
            else if(name is "button_roundhigh" or "button_highscore"){bool next=name=="button_roundhigh";if(next!=roundScores){ClearText();screen="busy";string old=roundScores?"ui_round_high":"ui_high_high";legacy.PlayRange(legacy.Id(old),"Take 001",50f/30,(roundScores?70f:66f)/30,()=>legacy.Find(old)!.Hidden=true);roundScores=next;ShowScorePanel();}}
        }
    }
    void SetPracticeTexture()
    {
        var n=legacy.Find("button_practice");if(n?.Geometry!=null)n.Geometry.FirstMaterial!.Diffuse.Contents=UIImage.FromFile(LegacyScene.Resource("textures/sharedassets0.assets-"+(GameStorage.Unlocked<2?16:13)+".png"));
    }
    void Scores()
    {
        screen="busy";roundScores=false;ClearText();legacy.HideRoots();legacy.Show("backdrop");
        foreach(var name in new[]{"ui_back_clear","ui_button_high_round","ui_quarter_logo_score"}){legacy.Show(name);foreach(var n in legacy.Find(name)!.ChildNodes)n.Opacity=1;legacy.PlayRange(legacy.Id(name),"Take 001",0,(name=="ui_quarter_logo_score"?15f:16f)/30);}
        ShowScorePanel();
    }
    void ShowScorePanel()
    {
        screen="busy";ClearText();
        SetScoreButtons();string name=roundScores?"ui_round_high":"ui_high_high";legacy.Show(name);foreach(var n in legacy.Find(name)!.ChildNodes)n.Opacity=1;
        legacy.PlayRange(legacy.Id(name),"Take 001",0,(roundScores?30f:22f)/30,()=>{screen="scores";ScoreText();});
    }
    void MenuLabel(string value,double x,double y,double width,double height,double size=16,bool right=false,bool center=false)
    {
        double w=(double)View!.Bounds.Width,h=(double)View.Bounds.Height,scale=h/(2*PresentationRules.HalfHeight(w,h))*200/480;
        var label=new UILabel(new CGRect((w-320*scale)/2+x*scale,(h-480*scale)/2+y*scale,width*scale,height*scale)){
            Text=value,TextColor=UIColor.White,TextAlignment=right?UITextAlignment.Right:center?UITextAlignment.Center:UITextAlignment.Left,
            Lines=0,Font=UIFont.BoldSystemFontOfSize((nfloat)(size*scale))!,AdjustsFontSizeToFitWidth=true,MinimumScaleFactor=.7f};textLayer.AddSubview(label);
    }
    void SetScoreButtons()
    {
        var group=legacy.Find("ui_button_high_round")!;
        foreach(var (name,id) in new[]{("button_highscore",roundScores?26:7),("button_roundhigh",roundScores?19:10)}){
            var node=group.FindChildNode(name,true)!;var material=(SCNMaterial)node.Geometry!.FirstMaterial!.Copy();node.Geometry.FirstMaterial=material;
            material.Diffuse.Contents=UIImage.FromFile(LegacyScene.Resource($"textures/sharedassets0.assets-{id}.png"));
        }
    }
    void ExitScores()
    {
        screen="busy";ClearText();
        legacy.PlayRange(legacy.Id("ui_back_clear"),"Take 001",25f/30,29f/30,()=>legacy.PlayRange(legacy.Id("ui_back_clear"),"Take 001",60f/30,65f/30));
        legacy.PlayRange(legacy.Id("ui_button_high_round"),"Take 001",70f/30,83f/30);
        legacy.PlayRange(legacy.Id("ui_quarter_logo_score"),"Take 001",55f/30,65f/30);
        legacy.PlayRange(legacy.Id(roundScores?"ui_round_high":"ui_high_high"),"Take 001",50f/30,(roundScores?70f:66f)/30,()=>Home());
    }
    void ScoreText()
    {
        ClearText();var rows=GameStorage.Scores;
        if(roundScores){for(int i=0;i<12;i++){
            MenuLabel("Round",45,90+i*28,100,30);MenuLabel((i+1).ToString(),50,90+i*28,100,30,right:true);MenuLabel(GameStorage.RoundScore(i).ToString(),150,90+i*28,100,30,right:true);
        }}else for(int i=0;i<10;i++){
            var entry=i<rows.Count?rows[i]:new ScoreRecord{Name="Empty"};
            MenuLabel((i+1).ToString(),-130,87+i*34,160,30,right:true);MenuLabel(entry.Name,50,87+i*34,160,30);MenuLabel(entry.Score.ToString(),100,87+i*34,160,30,right:true);
        }
    }
    void About()
    {
        screen="busy";ClearText();legacy.HideRoots();legacy.Show("backdrop");legacy.Show("ui_about");
        legacy.Play(legacy.Id("ui_about"),"Click",()=>{screen="about";DrawAbout();});
    }
    void DrawAbout()
    {
        ClearText();var root=legacy.Find("ui_about")!;foreach(var n in root.ChildNodes)n.Opacity=n.Name=="itme"?0:1;
        var dim=root.FindChildNode("dimplane",true);if(dim!=null){var pos=dim.Position;pos.Z=-3.1f;dim.Position=pos;var scale=dim.Scale;scale.Z=2.9f;dim.Scale=scale;}
        MenuLabel("For 25 years, Incredible Technologies has forged a name in the entertainment industry as the innovative thinkers behind some of the world's most popular arcade games.  You probably know us better as the Golden Tee Golf and Silver Strike Bowling guys and now we're pleased to introduce you to our more personal side, ITme.\n\nITme - IT mobile entertainment - is the newest division of the company, and the first one designed to entertain in the palm of your hand.  This new brand of IT will adorn our collection of mobile applications and we guarantee that they'll live up to our name.\n\nOur super-creative developers are going to be busy in the coming months unleashing games, utilities, productivity apps, you name it and you can count on all of them to look and feel great, be easy to use, and, most importantly, work as advertised.",30,30,260,385,12);
        MenuLabel("Visit www.ITMobileEntertainment.com",0,424,320,20,12,center:true);
        MenuLabel("V 1.1.0    06/21/2010",8,460,200,20,10);
    }
    void LayoutLoading()
    {
        if(loadingLabel==null)return;
        var safe=View!.SafeAreaInsets;double scale=Math.Min(2,(double)View.Bounds.Width/320);
        loadingLabel.Frame=new CGRect((double)safe.Left+6*scale,(double)View.Bounds.Height-(double)safe.Bottom-32*scale,Math.Min(240,(double)View.Bounds.Width-12*scale),32*scale);
        loadingLabel.Font=UIFont.SystemFontOfSize((nfloat)(16*scale))!;
    }
    async void Start(GameSession session,bool selectRound)
    {
        if(session.curShotThisRound==0&&session.curRound==0&&session.GetCurrentScore()==0)for(int i=0;i<session.totPlayers;i++)session.SetName(GameStorage.PlayerName(i),i);
        screen="busy";audio?.Stop();ClearText();legacy.HideRoots();legacy.Show("backdrop");
        loadingLabel=new UILabel {Text="Loading...",TextColor=UIColor.White,TextAlignment=UITextAlignment.Left};
        textLayer.AddSubview(loadingLabel);LayoutLoading();
        var game=new GameViewController(session,selectRound){ModalPresentationStyle=UIModalPresentationStyle.FullScreen};
        try {
            await game.PrepareAsync();
            // Build views on the main thread and prepare GPU resources while Loading remains visible.
            game.LoadViewIfNeeded();
            await game.PrepareRenderingAsync();
            game.Quit=()=>DismissViewController(false,()=>Home());
            PresentViewController(game,false,()=>{ClearText();loadingLabel=null;});
        }catch(Exception error){
            game.ReleasePreparedResources();Console.Error.WriteLine(error);
            ClearText();loadingLabel=null;Home();
            var alert=UIAlertController.Create("Couldn’t load game",error.Message,UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("OK",UIAlertActionStyle.Default,null));PresentViewController(alert,true,null);
        }
    }
}
