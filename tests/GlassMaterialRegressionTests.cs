using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ClearSpace {
public sealed partial class Controller {
 async Task VerifyGlassMaterialRegression(Action<bool,string> check){
  string theme=UiTheme.Light?"light":"dark";var available=UiGlass.OpticsAvailable;
  try{
   UiGlass.OpticsAvailable=()=>true;
   foreach(string appearance in new[]{"light","dark"}){
    UiTheme.Set(appearance,false);
    foreach(string effect in new[]{"light","enhanced"}){
     bool continuous=true;
     foreach(var size in new[]{new Size(240,44),new Size(190,40),new Size(176,50),new Size(560,120)}){
      var panel=new GlassPanel{Width=size.Width,Height=size.Height,CornerRadius=new CornerRadius(16),IsLens=true,Background=new SolidColorBrush(UiTheme.Light?Color.FromRgb(245,249,250):Color.FromRgb(22,29,37)),Backdrop=new SolidColorBrush(Color.FromRgb(200,200,200))};
      UiGlass.SetMode(panel,"internal");UiGlass.SetEffect(panel,effect);panel.Measure(size);panel.Arrange(new Rect(size));panel.UpdateLayout();
      foreach(int dpi in new[]{96,120,144,192}){
       var image=new RenderTargetBitmap((int)(size.Width*dpi/96),(int)(size.Height*dpi/96),dpi,dpi,PixelFormats.Pbgra32);var drawing=new DrawingVisual();using(var context=drawing.RenderOpen())context.DrawRectangle(new VisualBrush(panel){Stretch=Stretch.Fill},null,new Rect(size));image.Render(drawing);
       var center=new byte[4];var innerEdge=new byte[4];image.CopyPixels(new Int32Rect(image.PixelWidth/2,image.PixelHeight/2,1,1),center,4,0);image.CopyPixels(new Int32Rect(image.PixelWidth/2,5*dpi/96,1,1),innerEdge,4,0);
       continuous&=center[3]==255&&innerEdge[3]==255&&Enumerable.Range(0,3).All(c=>Math.Abs(center[c]-innerEdge[c])<=14);
      }
     }
     check(continuous,"Uniform "+appearance+" "+effect+" controls retain a continuous backdrop without center bands at 100–200 percent scaling");
    }
   }
   const int width=320,height=180,stride=width*4;var pixels=new byte[stride*height];for(int y=0;y<height;y++)for(int x=0;x<width;x++){int p=y*stride+x*4;byte alpha=(byte)(128+x*127/(width-1));pixels[p]=(byte)((x+y)%128);pixels[p+1]=(byte)(y*127/(height-1));pixels[p+2]=(byte)(x*127/(width-1));pixels[p+3]=alpha;}
   var source=BitmapSource.Create(width,height,96,96,PixelFormats.Pbgra32,null,pixels,stride);source.Freeze();var result=await GlassRefraction.RenderAsync(source,18,CancellationToken.None);var sampled=new byte[pixels.Length];result.CopyPixels(sampled,stride,0);var unchanged=new byte[pixels.Length];source.CopyPixels(unchanged,stride,0);
   check(result.IsFrozen&&result.PixelWidth==width&&result.PixelHeight==height&&!ReferenceEquals(source,result),"Background refraction returns a frozen image without changing snapshot dimensions");
   check(pixels.SequenceEqual(unchanged)&&Enumerable.Range(0,4).All(c=>sampled[(height/2)*stride+(width/2)*4+c]==pixels[(height/2)*stride+(width/2)*4+c]),"Refraction preserves its source and the undistorted center of the background");
   check(!pixels.SequenceEqual(sampled)&&Enumerable.Range(0,width*height).All(p=>sampled[p*4]<=sampled[p*4+3]&&sampled[p*4+1]<=sampled[p*4+3]&&sampled[p*4+2]<=sampled[p*4+3]),"Smooth edge sampling changes the background while retaining valid premultiplied transparency");
   var large=BitmapSource.Create(1000,501,96,96,PixelFormats.Pbgra32,null,new byte[1000*501*4],4000);large.Freeze();check(ReferenceEquals(large,await GlassRefraction.RenderAsync(large,18,CancellationToken.None)),"Oversized snapshots skip extra sampling rather than exceeding the half-million-pixel budget");
   bool canceled=false;try{await GlassRefraction.RenderAsync(source,18,new CancellationToken(true));}catch(OperationCanceledException){canceled=true;}check(canceled,"Canceled popup work stops before processing a background");
  }finally{UiGlass.OpticsAvailable=available;UiTheme.Set(theme,false);}
 }
}
}
