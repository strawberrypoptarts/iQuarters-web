using System.Text.Json;
using System.Text.Json.Serialization;
using IQuarters.Core;
namespace IQuarters.iOS;
public sealed class SavedGame
{
    public int Round {get;set;} public int Player {get;set;} public int Shots {get;set;} public int Made {get;set;}
    public float Angle {get;set;}=50;public bool Secret {get;set;} public List<PlayerInfoClass> Players {get;set;}=[];
}
public sealed class ScoreRecord {public string Id {get;set;}="";public string Name {get;set;}="PLR1";public int Score {get;set;}}
[JsonSourceGenerationOptions(IncludeFields=true)]
[JsonSerializable(typeof(SavedGame))]
[JsonSerializable(typeof(List<ScoreRecord>))]
internal partial class SaveJson : JsonSerializerContext {}
public static class GameStorage
{
    static NSUserDefaults Prefs=>NSUserDefaults.StandardUserDefaults;
    public static int Unlocked {get=>Math.Max(1,(int)Prefs.IntForKey("original.unlocked"));set=>Prefs.SetInt(value,"original.unlocked");}
    public static bool Muted {get=>Prefs.BoolForKey("original.muted");set=>Prefs.SetBool(value,"original.muted");}
    public static List<ScoreRecord> Scores {get {try{return JsonSerializer.Deserialize(Prefs.StringForKey("original.scores")??"[]",SaveJson.Default.ListScoreRecord)??[];}catch{return [];}}}
    public static string AddScore(string name,int score) {if(score<=0)return "";string id=Guid.NewGuid().ToString("N");var rows=Scores;rows.Add(new(){Id=id,Name=name,Score=score});rows=rows.OrderByDescending(s=>s.Score).Take(10).ToList();Prefs.SetString(JsonSerializer.Serialize(rows,SaveJson.Default.ListScoreRecord),"original.scores");return id;}
    public static void RenameScore(string id,string name){var rows=Scores;var row=rows.FirstOrDefault(r=>r.Id==id);if(row==null)return;row.Name=name;Prefs.SetString(JsonSerializer.Serialize(rows,SaveJson.Default.ListScoreRecord),"original.scores");}
    public static void ClearScores(bool rounds=false){if(rounds){for(int i=0;i<13;i++){Prefs.RemoveObject("original.round."+i);Prefs.RemoveObject("original.roundname."+i);}}else Prefs.RemoveObject("original.scores");for(int i=0;i<4;i++)Prefs.RemoveObject("original.playername."+i);}
    public static string PlayerName(int player)=>Prefs.StringForKey("original.playername."+player)??$"PLR{player+1}";
    public static void SavePlayerName(int player,string name)=>Prefs.SetString(name,"original.playername."+player);
    public static string RoundName(int round)=>Prefs.StringForKey("original.roundname."+round)??"Empty";
    public static int RoundScore(int round)=>(int)Prefs.IntForKey("original.round."+round);
    public static void RecordRound(int round,int score,string name="PLR1"){if(round>=12)return;if(score>RoundScore(round)){Prefs.SetInt(score,"original.round."+round);Prefs.SetString(name,"original.roundname."+round);}}
    public static void Save(GameSession session)
    {
        if(session.IsPractice)return;
        var saved=new SavedGame {Angle=session.shotAngle,Round=session.curRound,Player=session.curPlayer,Shots=session.curShotThisRound,Made=session.curMadeShotsThisRound,Secret=session.secretRoundUnlocked,Players=session.playerData.Cast<PlayerInfoClass>().ToList()};
        Prefs.SetString(JsonSerializer.Serialize(saved,SaveJson.Default.SavedGame),"original.saved");
    }
    public static GameSession? Load()
    {
        try {string? text=Prefs.StringForKey("original.saved");if(text==null)return null;var saved=JsonSerializer.Deserialize(text,SaveJson.Default.SavedGame);if(saved==null||saved.Players.Count<1||saved.Players.Count>4||saved.Round<0||saved.Round>12||saved.Player<0||saved.Player>=saved.Players.Count)return null;
            var s=new GameSession(saved.Players.Count){shotAngle=Math.Clamp(saved.Angle,45,55),curRound=saved.Round,curPlayer=saved.Player,curShotThisRound=saved.Shots,curMadeShotsThisRound=saved.Made,secretRoundUnlocked=saved.Secret};s.playerData.Clear();foreach(var p in saved.Players)s.playerData.Add(p);return s;
        }catch{return null;}
    }
    public static void ClearSaved()=>Prefs.RemoveObject("original.saved");
}
