// Copyright (c) 2026 fuyuan9972-svg. MIT. Windows synthesis hqiy0831. No recorded samples.
using System.IO;using System.Media;
namespace RAWSelectionAssistant.Views;
sealed class R55MechanicalSound:System.IDisposable
{
readonly string file;SoundPlayer? player;
public R55MechanicalSound(){file=Path.Combine(Path.GetTempPath(),"guangjing-viewing-"+System.Guid.NewGuid().ToString("N")+".wav");Synthesize(file,false);try{player=new SoundPlayer(file);player.Load();}catch(System.Exception e)when(e is System.InvalidOperationException or IOException){}}
public void Play(){try{player?.Play();}catch(System.InvalidOperationException){}}
public void Dispose(){player?.Stop();player?.Dispose();try{File.Delete(file);}catch(IOException){}}
    static void Synthesize(string path,bool first)
    {
        const int rate=48000;var n=(int)(rate*.38);var baseWave=new double[n];var rng=new Random(first?172:171);
        for(int i=0;i<n;i++)
        {
            var t=(double)i/rate;var click=0d;
            foreach(var (start,gain) in first?new[]{(0d,.65),(.04,.30)}:new[]{(0d,.70),(.045,.45),(.105,.35)})
            {
                var q=t-start;if(q>=0&&q<.08)click+=gain*Math.Exp(-q*95)*((rng.NextDouble()*2-1)*.7+Math.Sin(q*2*Math.PI*920)*.3);
            }
            baseWave[i]=i==0?0:click;
        }
        using var file=File.Create(path);using var w=new BinaryWriter(file);
        w.Write("RIFF"u8);w.Write(36+n*4);w.Write("WAVEfmt "u8);w.Write(16);w.Write((short)1);w.Write((short)2);w.Write(rate);w.Write(rate*4);w.Write((short)4);w.Write((short)16);w.Write("data"u8);w.Write(n*4);
        for(int i=0;i<n;i++)
        {
            double Channel(bool right)
            {
                var v=baseWave[i];foreach(var (delay,gain) in new[]{(.018,.18),(.041,.10),(.073,.06),(.131,.03)}){int k=i-(int)((delay+(right?.0015:0))*rate);if(k>=0)v+=baseWave[k]*gain;}
                return v;
            }
            w.Write((short)(Math.Clamp(Channel(false),-1,1)*26000));w.Write((short)(Math.Clamp(Channel(true),-1,1)*26000));
        }
    }
}
