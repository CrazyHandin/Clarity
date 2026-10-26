using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ClearSpace {
// Frozen, half-resolution owner pixels are processed off the dispatcher.
// Moving a dialog only changes an ImageBrush viewbox over this shared image.
internal static class GlassBackground {
 static readonly SemaphoreSlim worker=new SemaphoreSlim(1,1);
 internal static async Task<BitmapSource> BlurAsync(BitmapSource image,double radius,CancellationToken token){
  token.ThrowIfCancellationRequested();if(!worker.Wait(0,token))return null;
  try{return await Task.Run(()=>Blur(image,radius,token),token).ConfigureAwait(false);}finally{worker.Release();}
 }
 internal static BitmapSource Blur(BitmapSource image,double radius,CancellationToken token){
  token.ThrowIfCancellationRequested();int width=image.PixelWidth,height=image.PixelHeight;if(width<1||height<1||(long)width*height>GlassRefraction.MaxPixels)return null;
  if(image.Format!=PixelFormats.Pbgra32){var converted=new FormatConvertedBitmap(image,PixelFormats.Pbgra32,null,0);converted.Freeze();image=converted;}
  int stride=width*4;var pixels=new byte[stride*height];var scratch=new byte[pixels.Length];image.CopyPixels(pixels,stride,0);
  int rx=Math.Max(1,Math.Min(24,(int)Math.Round(radius*image.DpiX/96*0.6))),ry=Math.Max(1,Math.Min(24,(int)Math.Round(radius*image.DpiY/96*0.6)));
  for(int pass=0;pass<3;pass++){Box(pixels,scratch,width,height,rx,true,token);Box(scratch,pixels,width,height,ry,false,token);}
  token.ThrowIfCancellationRequested();var result=BitmapSource.Create(width,height,image.DpiX,image.DpiY,PixelFormats.Pbgra32,null,pixels,stride);result.Freeze();return result;
 }
 static void Box(byte[] source,byte[] target,int width,int height,int radius,bool horizontal,CancellationToken token){
  int lines=horizontal?height:width,length=horizontal?width:height,step=horizontal?4:width*4,count=radius*2+1;
  for(int line=0;line<lines;line++){
   token.ThrowIfCancellationRequested();int origin=horizontal?line*width*4:line*4;
   for(int channel=0;channel<4;channel++){
    int sum=0;for(int i=-radius;i<=radius;i++)sum+=source[origin+Math.Max(0,Math.Min(length-1,i))*step+channel];
    for(int i=0;i<length;i++){target[origin+i*step+channel]=(byte)((sum+count/2)/count);sum+=source[origin+Math.Min(length-1,i+radius+1)*step+channel]-source[origin+Math.Max(0,i-radius)*step+channel];}
   }
  }
 }
 internal static BitmapSource Crop(BitmapSource image,Rect region,CancellationToken token){
  token.ThrowIfCancellationRequested();double sx=image.DpiX/96,sy=image.DpiY/96;int width=Math.Max(1,(int)Math.Ceiling(region.Width*sx)),height=Math.Max(1,(int)Math.Ceiling(region.Height*sy));if((long)width*height>GlassRefraction.MaxPixels)return null;
  int x=(int)Math.Floor(region.X*sx),y=(int)Math.Floor(region.Y*sy),left=Math.Max(0,x),top=Math.Max(0,y),right=Math.Min(image.PixelWidth,x+width),bottom=Math.Min(image.PixelHeight,y+height),stride=width*4;var pixels=new byte[stride*height];
  if(right>left&&bottom>top)image.CopyPixels(new Int32Rect(left,top,right-left,bottom-top),pixels,stride,(top-y)*stride+(left-x)*4);
  var result=BitmapSource.Create(width,height,image.DpiX,image.DpiY,PixelFormats.Pbgra32,null,pixels,stride);result.Freeze();return result;
 }
}
}
