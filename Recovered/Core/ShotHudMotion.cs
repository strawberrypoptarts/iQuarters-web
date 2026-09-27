namespace IQuarters.Core;

// Transcribed from Android CoinHolder / InGameAngleIcon / RoundIndicator.
// Clock-driven motion keeps authored speeds at 30, 60, 120 and 144 Hz.
public sealed class ShotHudMotion
{
    public float Holder { get; private set; }
    public float Angle { get; private set; }
    public float SpinRadians { get; private set; }
    public float CoinsLeft { get; private set; }
    public float PressScale { get; private set; }=1;
    bool holderOn,angleOn,spinning;
    float coinClock=-1,pressClock=-1;
    public void Reset(){Holder=Angle=CoinsLeft=SpinRadians=0;PressScale=1;holderOn=angleOn=spinning=false;coinClock=pressClock=-1;}
    public void Show(bool holder=true,bool angle=true){holderOn=holder;angleOn=angle;}
    public void HolderVisible(bool value)=>holderOn=value;
    public void Hide(){holderOn=angleOn=false;spinning=false;}
    public void StartSpin(){SpinRadians=0;spinning=true;}
    public void ResetSpin(){SpinRadians=0;spinning=false;}
    public void StopSpin()=>spinning=false;
    public void PulseCoinsLeft()=>coinClock=0;
    public void PressAngle()=>pressClock=0;
    static float Toward(float x,float target,float delta)=>x<target?Math.Min(target,x+delta):Math.Max(target,x-delta);
    public void Tick(float dt)
    {
        if(!float.IsFinite(dt)||dt<=0)return;
        Holder=Toward(Holder,holderOn?1:0,dt/.3f);Angle=Toward(Angle,angleOn?1:0,dt/.3f);
        if(spinning)SpinRadians=(SpinRadians-dt*100*MathF.PI/180)%(2*MathF.PI);
        if(coinClock>=0){coinClock+=dt;CoinsLeft=coinClock<.5f?coinClock/.5f:coinClock<1?1:Math.Max(0,1-(coinClock-1)/.5f);if(coinClock>=1.5f)coinClock=-1;}
        if(pressClock>=0){pressClock+=dt;PressScale=pressClock<.05f?1-.01f*pressClock/.05f:Math.Min(1,.99f+.01f*(pressClock-.05f)/.05f);if(pressClock>=.1f)pressClock=-1;}
    }
}
