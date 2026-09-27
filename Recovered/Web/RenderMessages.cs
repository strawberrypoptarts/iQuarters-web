using System.Text.Json.Serialization;
using System.Runtime.Versioning;
[assembly:SupportedOSPlatform("browser")]
namespace IQuarters.Web;
internal sealed record MaterialMessage(string key,bool unlit,bool depth,double alpha,string? texture,float[]? color,float offset);
internal sealed record CameraMessage(bool ortho,double half,double fov,double near,double far);
internal sealed record NodeMessage(int id,int parent,string? name,int source,uint mask,bool hidden,float alpha,float[] p,float[] q,float[] s,string? mesh,MaterialMessage[]? mats,CameraMessage? camera);
internal sealed record RootMessage(int id,int camera);
internal sealed record LabelMessage(int id,string text,double x,double y,double w,double h,double size,string align,float[]? background,float[] color);
[JsonSerializable(typeof(NodeMessage))]
[JsonSerializable(typeof(RootMessage[]))]
[JsonSerializable(typeof(LabelMessage[]))]
internal partial class RenderJson:JsonSerializerContext {}
