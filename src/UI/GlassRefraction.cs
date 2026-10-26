using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ClearSpace {
// Background-only, once per popup snapshot. A single worker and a pixel cap
// bound the cost; the UI opens with its frozen blurred image immediately.
internal static class GlassRefraction {
 internal const int MaxPixels=500000;
 static readonly SemaphoreSlim worker=new SemaphoreSlim(1,1);
 internal static async Task<BitmapSource> RenderAsync(BitmapSource source,double radius,CancellationToken token){
  token.ThrowIfCancellationRequested();if(!worker.Wait(0,token))return source;
  try{return await Task.Run(()=>Apply(source,radius,token),token).ConfigureAwait(false);}finally{worker.Release();}
 }
 internal static BitmapSource Apply(BitmapSource source,double radius,CancellationToken token){
  token.ThrowIfCancellationRequested();int width=source.PixelWidth,height=source.PixelHeight;if(width<4||height<4||(long)width*height>MaxPixels)return source;
  if(source.Format!=PixelFormats.Pbgra32){var converted=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0);converted.Freeze();source=converted;}
  int stride=width*4;var pixels=new byte[stride*height];source.CopyPixels(pixels,stride,0);var output=(byte[])pixels.Clone();double sx=source.DpiX/96,sy=source.DpiY/96;if(sx<=0||sy<=0)return source;
  double halfWidth=width/(2*sx),halfHeight=height/(2*sy),curve=Math.Max(0,Math.Min(radius,Math.Min(halfWidth,halfHeight))),band=Math.Min(9,Math.Min(halfWidth,halfHeight)*0.3),shift=1.7;
  int edgeX=Math.Min(width/2,(int)Math.Ceiling((curve+band+shift)*sx)),edgeY=Math.Min(height/2,(int)Math.Ceiling((curve+band+shift)*sy));
  for(int y=0;y<height;y++){
   token.ThrowIfCancellationRequested();double py=(y+0.5)/sy-halfHeight;
   for(int x=0;x<width;x++){
    if(y>=edgeY&&y<height-edgeY&&x==edgeX)x=width-edgeX;if(x>=width)break;
    double px=(x+0.5)/sx-halfWidth,qx=Math.Abs(px)-(halfWidth-curve),qy=Math.Abs(py)-(halfHeight-curve),ox=Math.Max(qx,0),oy=Math.Max(qy,0),length=Math.Sqrt(ox*ox+oy*oy),distance=curve-length-Math.Min(Math.Max(qx,qy),0);
    if(distance<0||distance>=band)continue;double nx,ny;if(length>0){nx=ox/length*Math.Sign(px);ny=oy/length*Math.Sign(py);}else if(qx>qy){nx=Math.Sign(px);ny=0;}else{nx=0;ny=Math.Sign(py);}
    double t=1-distance/band,amount=shift*t*t*(3-2*t),sampleX=Math.Max(0,Math.Min(width-1,x-nx*amount*sx)),sampleY=Math.Max(0,Math.Min(height-1,y-ny*amount*sy));
    int left=(int)sampleX,top=(int)sampleY,right=Math.Min(left+1,width-1),bottom=Math.Min(top+1,height-1),target=y*stride+x*4;double dx=sampleX-left,dy=sampleY-top;
    for(int channel=0;channel<4;channel++){double a=pixels[top*stride+left*4+channel]*(1-dx)+pixels[top*stride+right*4+channel]*dx,b=pixels[bottom*stride+left*4+channel]*(1-dx)+pixels[bottom*stride+right*4+channel]*dx;output[target+channel]=(byte)Math.Max(0,Math.Min(255,Math.Round(a*(1-dy)+b*dy)));}
    for(int channel=0;channel<3;channel++)output[target+channel]=Math.Min(output[target+channel],output[target+3]);
   }
  }
  token.ThrowIfCancellationRequested();var result=BitmapSource.Create(width,height,source.DpiX,source.DpiY,PixelFormats.Pbgra32,null,output,stride);result.Freeze();return result;
 }
}
}
