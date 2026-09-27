using SceneKit;
using CoreGraphics;
using IQuarters.Core;
namespace IQuarters.iOS;

// Presentation behavior transcribed from the Android reference's script state machines.
public sealed partial class GameViewController
{
    readonly List<(SCNNode Node,float Age)> contactFlashes=[];
    float flashSize,flashLife,flashGrow;float[] flashAlpha=[];
    void InitializeContactFlashes()
    {
        using var document=System.Text.Json.JsonDocument.Parse(File.ReadAllText(LegacyScene.Resource("scene.json")));
        var data=document.RootElement.GetProperty("contactFlash");flashSize=data.GetProperty("size").GetSingle();flashLife=data.GetProperty("life").GetSingle();flashGrow=data.GetProperty("grow").GetSingle();flashAlpha=LegacyScene.Values(data.GetProperty("alpha"));
        for(int i=0;i<8;i++){var node=new SCNNode{Name="Contact flash",Geometry=game.Nodes[917].Geometry,Hidden=true};game.World.AddChildNode(node);contactFlashes.Add((node,flashLife));}
    }
    void ContactFlash(System.Numerics.Vector3 point)
    {
        int slot=contactFlashes.FindIndex(f=>f.Age>=flashLife);if(slot<0)slot=0;
        var node=contactFlashes[slot].Node;node.Position=new(point.X,point.Y,point.Z);node.Hidden=false;node.Opacity=flashAlpha[0];node.Scale=new(flashSize,flashSize,flashSize);contactFlashes[slot]=(node,0);
    }
    int helpPage;
    float stackClock,stackDuration;int stackCount;
    SCNVector3 angleArrowScale;
    bool angleArrowInitialized;
    Action? afterHighScores;
    string highlightedScore="";
    readonly List<(UILabel Label,CGRect Rect,double Size)> originalLabels=[];

    // Original GUI uses a 320 x 480 design space. Center it in the same expanded
    // orthographic viewport as the authored meshes, without stretching text.
    void Label(string text,double x,double y,double w,double h,double size=16,bool yellow=false,bool right=false,bool center=false)
    {
        var label=new UILabel {Text=text,TextColor=yellow?UIColor.FromRGBA(1,1,0,1):UIColor.White,
            TextAlignment=right?UITextAlignment.Right:center?UITextAlignment.Center:UITextAlignment.Left,
            Lines=0,AdjustsFontSizeToFitWidth=true,MinimumScaleFactor=.7f};
        textLayer.AddSubview(label);originalLabels.Add((label,new CGRect(x,y,w,h),size));LayoutOriginalLabels();
    }
    void LayoutOriginalLabels()
    {
        double w=(double)View!.Bounds.Width,h=(double)View.Bounds.Height;
        double scale=h/(2*PresentationRules.HalfHeight(w,h))*200/480;
        double left=(w-320*scale)/2,top=(h-480*scale)/2;
        foreach(var (label,r,size) in originalLabels){label.Frame=new CGRect(left+(double)r.X*scale,top+(double)r.Y*scale,(double)r.Width*scale,(double)r.Height*scale);label.Font=UIFont.BoldSystemFontOfSize((nfloat)(size*scale))!;}
    }
    void UiSequence(int owner,string[] clips,Action done)
    {
        void Next(int index){if(index==clips.Length){done();return;}ui.Play(owner,clips[index],()=>Next(index+1));}Next(0);
    }
    void DrawRules()
    {
        ClearText();Label("Game Rules",0,17,320,30,18,yellow:true,center:true);
        Label("Tap To Continue",0,450,320,30,16,yellow:true,center:true);
        if(helpPage==0){
            Label("Two Modes: Classic and Practice", 10, 50, 295, 16, 12, yellow:true);
            Label("Play", 10, 180, 295, 16, 12, yellow:true);
            Label("Classic Mode is the regular game", 25, 70, 295, 16, 12, yellow:false);
            Label("where players must sink 3 shots", 25, 82, 295, 16, 12, yellow:false);
            Label("before advancing to the next", 25, 94, 295, 16, 12, yellow:false);
            Label("round.", 25, 106, 295, 16, 12, yellow:false);
            Label("Practice Mode allows players to", 25, 126, 295, 16, 12, yellow:false);
            Label("practice taking shots in any of", 25, 138, 295, 16, 12, yellow:false);
            Label("the rounds.", 25, 150, 295, 16, 12, yellow:false);
            Label("Flick the quarter to make it fly", 25, 200, 295, 16, 12, yellow:false);
            Label("toward the glass. The quicker", 25, 212, 295, 16, 12, yellow:false);
            Label("you flick, the farther it flies.", 25, 224, 295, 16, 12, yellow:false);
            Label("Before taking a shot, tap on the", 25, 244, 295, 16, 12, yellow:false);
            Label("shot angle button in the upper", 25, 256, 295, 16, 12, yellow:false);
            Label("right of the screen.", 25, 268, 295, 16, 12, yellow:false);
            Label("Flick down on the green arrow", 25, 288, 295, 16, 12, yellow:false);
            Label("to take a steeper angle shot", 25, 300, 295, 16, 12, yellow:false);
            Label("(shorter distance).", 25, 312, 295, 16, 12, yellow:false);
            Label("Flick up on the green arrow to", 25, 332, 295, 16, 12, yellow:false);
            Label("use a flatter angle (longer", 25, 344, 295, 16, 12, yellow:false);
            Label("distance).", 25, 356, 295, 16, 12, yellow:false);
            Label("Tap on the shot angle button", 25, 376, 295, 16, 12, yellow:false);
            Label("again to return to the game.", 25, 388, 295, 16, 12, yellow:false);
            foreach(int y in new[]{70,126,200,244,288,332,376})Label("-",20,y,10,16,12);
        }else{
            Label("Rules", 10, 50, 295, 16, 12, yellow:true);
            Label("Scoring", 10, 268, 295, 16, 12, yellow:true);
            Label("The game starts with up to 4 ", 25, 70, 295, 16, 12, yellow:false);
            Label("players and 40 quarters per", 25, 82, 295, 16, 12, yellow:false);
            Label("player.", 25, 94, 295, 16, 12, yellow:false);
            Label("During the 12 rounds of Classic", 25, 114, 295, 16, 12, yellow:false);
            Label("Mode, attempt to make 3 shots per", 25, 126, 295, 16, 12, yellow:false);
            Label("round to move onto the next round.", 25, 138, 295, 16, 12, yellow:false);
            Label("The harder the shot, the more", 25, 158, 295, 16, 12, yellow:false);
            Label("points you earn. Bounce a shot off", 25, 170, 295, 16, 12, yellow:false);
            Label("a neighboring object for big", 25, 182, 295, 16, 12, yellow:false);
            Label("ricochet bonus points.", 25, 194, 295, 16, 12, yellow:false);
            Label("Be careful! Miss a shot and one", 25, 214, 295, 16, 12, yellow:false);
            Label("quarter is removed from your", 25, 226, 295, 16, 12, yellow:false);
            Label("available quarters.", 25, 238, 295, 16, 12, yellow:false);
            Label("Make an easy shot for 25 points.", 25, 288, 295, 16, 12, yellow:false);
            Label("Harder shots are worth 50 (2X)", 25, 308, 295, 16, 12, yellow:false);
            Label("and 75 (3X) points.", 25, 320, 295, 16, 12, yellow:false);
            Label("10 points for each unique ricochet", 25, 340, 295, 16, 12, yellow:false);
            Label("before sinking in the glass.", 25, 352, 295, 16, 12, yellow:false);
            Label("5 bonus points for every unused", 25, 372, 295, 16, 12, yellow:false);
            Label("quarter at the end of the game.", 25, 384, 295, 16, 12, yellow:false);
            foreach(int y in new[]{70,114,158,214,288,308,340,372})Label("-",20,y,10,16,12);
            Label("For Replays go to the Pause || Menu",0,414,320,30,12,yellow:true,center:true);
        }
    }
    void OpenRules()
    {
        state="pauseTransition";helpPage=0;
        ui.Play(ui.Id("ui_help"),"InfoClick",()=>{
            ui.Find("ui_help")!.Hidden=true;
            ui.Play(1882,"animout",()=>{ui.Nodes[1882].Hidden=true;ui.Show("ui_help");foreach(var n in ui.Nodes[1857].ChildNodes)n.Opacity=n.Name is "dimplane" or "help_text"?1:0;state="help";DrawRules();});
        });
    }
    void AdvanceRules()
    {
        if(++helpPage<2){DrawRules();return;}
        ClearText();state="pauseTransition";ui.Show("UI_backend_00");
        ui.Play(1882,"animin",()=>{state="pause";ui.Show("ui_help");foreach(var n in ui.Nodes[1857].ChildNodes)n.Opacity=n.Name=="help"?1:0;});
    }
    void LeavePause(string click,Action done)
    {
        state="pauseTransition";ui.Find("ui_help")!.Hidden=true;
        UiSequence(1882,[click,"animout"],()=>{ui.Nodes[1882].Hidden=true;done();});
    }
    void UpdateAngleArrow()
    {
        var arrow=game.Nodes[1907];
        if(!angleArrowInitialized){angleArrowScale=arrow.Scale;angleArrowInitialized=true;}
        arrow.Hidden=!angleInput||state!="play";arrow.Opacity=1;
        float t=Math.Clamp((angle-45)/10,0,1);
        arrow.Scale=new(angleArrowScale.X,angleArrowScale.Y-8*t+30*(1-t),angleArrowScale.Z-3*t-8*(1-t));
    }
    void DrawStats()
    {
        ClearText();int count=session.totPlayers;
        for(int i=0;i<count;i++){
            int[] values=[session.GetPlayerScore(i),session.GetPlayerMaxStreak(i),session.GetPlayerMaxRicochet(i),session.GetPlayerShotsLeft(i)];
            for(int row=0;row<4;row++)Label(values[row].ToString(),132-32*(count-1)+i*64,115+row*34,80,30,right:true);
        }
    }
    void CloseStats()
    {
        ClearText();state="statsTransition";
        UiSequence(128,[$"done_click_{session.totPlayers}",$"ui_out_{session.totPlayers}"],()=>{GameStorage.ClearSaved();Quit?.Invoke();});
    }
    void SecretRoundEffect(bool intro,Action done)
    {
        PlayExciter(948);After(2.25f,done);
        ui.Nodes[1053].Opacity=intro?0:1;ui.Nodes[931].Opacity=intro?0:1;ui.Nodes[934].Opacity=intro?1:0;
        var asset=physics.GetProperty("quarterFields").GetProperty("secretRoundSFX");
        EffectSound(asset[1].GetInt32(),1,1,asset[0].GetInt32()==3);
    }
    void StackBonus(int count,Action done)
    {
        if(count<=0){done();return;}
        state="effects";SetPauseBackdrop(true);ui.Show("ui_stack");stackCount=count;stackClock=0;
        float speed=count>11?1+(count-12)*1.5f/28:1;
        stackDuration=ui.Duration(1338,$"CoinsLeft{count}")/speed;
        for(int i=0;i<40;i++)ui.Find($"quarter_{i:00}")!.Hidden=i>=count;
        EffectSound(590,2);UpdateStackDigits(0);
        ui.Play(1338,$"CoinsLeft{count}",()=>{
            stackCount=0;audio?.Stop(2);UpdateStackDigits(count);
            ui.Show("exciters_bonus_score");int score=count*5;
            int[] ids=[1089,1086,1127,1095,1092];
            foreach(int id in ids)ui.Nodes[id].Opacity=0;
            int first=score<10?1:score<100?3:0,number=score<10?1:score<100?2:3;
            for(int i=0;i<number;i++)EffectDigit(ids[first+i],score/(int)Math.Pow(10,i)%10);
            EffectSound(601,2);
            ui.Play(1103,"SlideIn",()=>After(2,()=>ui.Play(1103,"SlideOut",()=>{
                ui.Nodes[1103].Hidden=true;ui.Nodes[1338].Hidden=true;done();
            })));
        },speed:speed);
    }
    void UpdateStackDigits(int count)
    {
        EffectDigit(1379,count/10,count>=10);EffectDigit(1105,count%10);
    }
    void TickParity(float dt)
    {
        UpdateAngleArrow();
        for(int i=0;i<contactFlashes.Count;i++){
            var (node,age)=contactFlashes[i];if(age>=flashLife)continue;age+=dt;contactFlashes[i]=(node,age);
            if(age>=flashLife){node.Hidden=true;continue;}
            float key=age/flashLife*4;int at=Math.Min(3,(int)key);node.Opacity=flashAlpha[at]+(flashAlpha[at+1]-flashAlpha[at])*(key-at);
            float size=flashSize+flashGrow*age;node.Scale=new(size,size,size);var q=camera.Orientation;node.Orientation=new(-q.X,-q.Y,q.Z,q.W);
        }
        if(stackCount>0){stackClock+=dt;UpdateStackDigits(Math.Min(stackCount,(int)(stackClock/stackDuration*stackCount)+2));}
    }
    void RequestedReplay()
    {
        beforeReplay=Capture();returnRound=round;ShowRound(replayRound);SetPauseBackdrop(false);
        replayFrame=0;replayTime=0;UseReplayCamera();state="replay";
    }
    void FinishReplay()
    {
        if(afterAutomaticReplay is {} completed){
            RestoreGameCamera();ShowRound(returnRound);Restore(beforeReplay);MoveColliders();
            afterAutomaticReplay=null;state="effects";completed();return;
        }
        state="replayDoneTransition";ui.Show("ui_save_replay");ui.Find("button_save_replay")!.Hidden=true;
        ui.Play(1243,"SlideIn",()=>state="replayDone");
    }
    void CloseReplay()
    {
        state="replayDoneTransition";
        UiSequence(1243,["ClickDone","SlideOut"],()=>{
            ui.Nodes[1243].Hidden=true;RestoreGameCamera();ShowRound(returnRound);Restore(beforeReplay);
            ResetCoin();state="play";Hud();ShowShotHud();
        });
    }
    void PlayerHighScore(int playerIndex,Action done)
    {
        var player=(PlayerInfoClass)session.playerData[playerIndex]!;
        if(!GameStorage.Scores.Any(s=>s.Id==player.recoveredScoreId)){done();return;}
        highlightedScore=player.recoveredScoreId;afterHighScores=done;
        SetPauseBackdrop(true);
        void Board(){
            ui.Nodes[915].Hidden=true;state="scoreTransition";ui.Show("ui_highscore");
            void Bars(){ui.Play(19,"barsin",()=>{state="highScores";DrawInGameScores();});}
            if(GameStorage.Scores.FirstOrDefault()?.Id==highlightedScore){
                PlayExciter(110);PlayExciter(181,null,Bars);
            }else Bars();
        }
        if(player.nameEnteredFlag){Board();return;}
        state="names";ui.Show("ui_name_leaderboard");ui.Play(915,"SlideIn",()=>{
            var prompt=UIAlertController.Create("New high score",$"Player {playerIndex+1}: {player.score}",UIAlertControllerStyle.Alert);
            prompt.AddTextField(t=>{t.Text=player.playerName;t.AutocorrectionType=UITextAutocorrectionType.No;t.AutocapitalizationType=UITextAutocapitalizationType.AllCharacters;});
            prompt.AddAction(UIAlertAction.Create("Done",UIAlertActionStyle.Default,_=>{
                var name=prompt.TextFields![0].Text?.Trim();if(!string.IsNullOrWhiteSpace(name))player.playerName=new string(name.Take(10).ToArray());
                player.nameEnteredFlag=true;GameStorage.RenameScore(player.recoveredScoreId,player.playerName);GameStorage.SavePlayerName(playerIndex,player.playerName);Board();
            }));PresentViewController(prompt,true,null);
        });
    }
    void DrawInGameScores()
    {
        ClearText();var rows=GameStorage.Scores;
        for(int i=0;i<10;i++){
            var entry=i<rows.Count?rows[i]:new ScoreRecord{Name="Empty"};bool selected=entry.Id!=""&&entry.Id==highlightedScore;
            string name=entry.Name.Length>8?entry.Name[..8]+"...":entry.Name;
            Label((i+1).ToString(),-130,87+i*34,160,30,yellow:selected,right:true);
            Label(name,50,87+i*34,160,30,yellow:selected);
            Label(entry.Score.ToString(),100,87+i*34,160,30,yellow:selected,right:true);
        }
    }
    void CloseInGameScores()
    {
        ClearText();state="scoreTransition";
        UiSequence(19,["doneclick","barsout"],()=>{ui.Nodes[19].Hidden=true;SetPauseBackdrop(false);var done=afterHighScores;afterHighScores=null;done?.Invoke();});
    }
}
