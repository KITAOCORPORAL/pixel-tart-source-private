// Copyright (c) 2026 fuyuan9972-svg. MIT. Windows port hqiy0831.
using System.Text.Json.Serialization;
namespace RAWSelectionAssistant.Core.Services.Projects.LightTablePort;
public readonly record struct PointD(double X, double Y)
{
    public static PointD operator +(PointD a, PointD b) => new(a.X + b.X, a.Y + b.Y);
    public static PointD operator -(PointD a, PointD b) => new(a.X - b.X, a.Y - b.Y);
    public static PointD operator *(PointD a, double b) => new(a.X * b, a.Y * b);
}
public readonly record struct SizeD(double Width, double Height);
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    [JsonIgnore] public PointD Center => new(X + Width / 2, Y + Height / 2);
    public bool Contains(PointD p) => p.X >= X && p.Y >= Y && p.X <= X + Width && p.Y <= Y + Height;
    public bool Intersects(RectD b) => X < b.X + b.Width && X + Width > b.X && Y < b.Y + b.Height && Y + Height > b.Y;
    public static RectD Between(PointD a, PointD b) => new(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y));
}

public readonly record struct Placement(PointD Center,double Width,double Height,double Angle)
{
    public RectD Rect=>new(Center.X-Width/2,Center.Y-Height/2,Width,Height);
    public PointD Local(PointD p) => Geometry.Rotate(p-Center,-Angle);
    public bool Contains(PointD p){var q=Local(p);return Math.Abs(q.X)<=Width/2&&Math.Abs(q.Y)<=Height/2;}
    public bool Intersects(RectD r)
    {
        var rad=Angle*Math.PI/180;var c=Math.Cos(rad);var s=Math.Sin(rad);var d=r.Center-Center;
        double rw=r.Width/2,rh=r.Height/2,pw=Width/2,ph=Height/2;
        return Math.Abs(d.X)<=rw+pw*Math.Abs(c)+ph*Math.Abs(s)&&Math.Abs(d.Y)<=rh+pw*Math.Abs(s)+ph*Math.Abs(c)&&
            Math.Abs(d.X*c+d.Y*s)<=pw+rw*Math.Abs(c)+rh*Math.Abs(s)&&Math.Abs(-d.X*s+d.Y*c)<=ph+rw*Math.Abs(s)+rh*Math.Abs(c);
    }
}
public static class Geometry {public static double Finite(double v,double fallback)=>double.IsFinite(v)?v:fallback;public static PointD Rotate(PointD p,double degrees){var a=degrees*Math.PI/180;return new(p.X*Math.Cos(a)-p.Y*Math.Sin(a),p.X*Math.Sin(a)+p.Y*Math.Cos(a));}public static double Smooth(double t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}public static double Quintic(double t){t=Math.Clamp(t,0,1);return Math.Clamp(t*t*t*(t*(t*6-15)+10),0,1);}}