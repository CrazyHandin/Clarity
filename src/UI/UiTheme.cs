using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
namespace ClearSpace {
public sealed class ThemeColorSlot:INotifyPropertyChanged {
 public Color Original;Color color;public Color Color{get{return color;}set{if(color==value)return;color=value;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("Color"));}}
 public event PropertyChangedEventHandler PropertyChanged;
}
public static class UiTheme {
 static readonly ResourceDictionary palette=new ResourceDictionary();static readonly Dictionary<string,ThemeColorSlot> slots=new Dictionary<string,ThemeColorSlot>();static DispatcherTimer transition;
 public static bool Light{get;private set;}
 public static string Normalize(string value){return String.Equals(value,"system",StringComparison.OrdinalIgnoreCase)?"system":String.Equals(value,"light",StringComparison.OrdinalIgnoreCase)?"light":"dark";}
 internal static Func<bool> SystemLight=ReadSystemLight;
 static bool ReadSystemLight(){try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",false))return key!=null&&Convert.ToInt32(key.GetValue("AppsUseLightTheme",0))!=0;}catch{return false;}}
 static Color Parse(string value){return (Color)ColorConverter.ConvertFromString(value);}
 static string Key(Color color){return color.ToString().Substring(1);}
 public static Brush Brush(string value){
  Color color=Parse(value);string key=Key(color);ThemeColorSlot slot;if(!slots.TryGetValue(key,out slot)){
   slot=new ThemeColorSlot{Original=color,Color=Map(color,Light)};slots[key]=slot;
   var brush=new SolidColorBrush();BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding("Color"){Source=slot});palette["ThemeBrush"+key]=brush;palette["ThemeColor"+key]=slot.Color;
   slot.PropertyChanged+=(s,e)=>palette["ThemeColor"+key]=slot.Color;
  }return (Brush)palette["ThemeBrush"+key];
 }
 static string Resource(string value,bool color){Brush(value);return "{DynamicResource Theme"+(color?"Color":"Brush")+Key(Parse(value))+"}";}
 public static string Markup(string markup){
  markup=Regex.Replace(markup,@"(?<prefix>\b(?:Background|Foreground|BorderBrush|Stroke|Fill|Color)\s*=\s*)(?<q>['"+"\""+@"])(?<value>\#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?)\k<q>",m=>m.Groups["prefix"].Value+m.Groups["q"].Value+Resource(m.Groups["value"].Value,m.Groups["prefix"].Value.TrimStart().StartsWith("Color"))+m.Groups["q"].Value);
  return Regex.Replace(markup,@"(?<prefix>\bProperty\s*=\s*['"+"\""+@"](?:Background|Foreground|BorderBrush|Stroke|Fill)['"+"\""+@"][^<>]*?\bValue\s*=\s*)(?<q>['"+"\""+@"])(?<value>\#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?)\k<q>",m=>m.Groups["prefix"].Value+m.Groups["q"].Value+Resource(m.Groups["value"].Value,false)+m.Groups["q"].Value);
 }
 public static void Attach(FrameworkElement root){if(!root.Resources.MergedDictionaries.Contains(palette))root.Resources.MergedDictionaries.Add(palette);}
 public static void Set(string value,bool animate){
  bool light=Normalize(value)=="system"?SystemLight():Normalize(value)=="light";if(transition!=null){transition.Stop();transition=null;}Light=light;
  var start=slots.Values.ToDictionary(s=>s,s=>s.Color);var targets=start.Keys.ToDictionary(s=>s,s=>Map(s.Original,light));
  if(!animate){foreach(var item in targets)item.Key.Color=item.Value;return;}
  DateTime began=DateTime.UtcNow;transition=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};
  transition.Tick+=(s,e)=>{double p=Math.Min(1,(DateTime.UtcNow-began).TotalMilliseconds/240);double eased=p*p*(3-2*p);foreach(var item in targets){Color from=start[item.Key],to=item.Value;item.Key.Color=Color.FromArgb(Blend(from.A,to.A,eased),Blend(from.R,to.R,eased),Blend(from.G,to.G,eased),Blend(from.B,to.B,eased));}if(p>=1){((DispatcherTimer)s).Stop();if(ReferenceEquals(transition,s))transition=null;}};transition.Start();
 }
 static byte Blend(byte a,byte b,double p){return (byte)Math.Round(a+(b-a)*p);}
 // Light surfaces, borders and green typography follow the beta0.02 palette.
 static readonly Dictionary<string,string> lightColors=new Dictionary<string,string>{
  {"#161D25","#F3F8F8"},{"#1B2731","#FFFFFF"},{"#1D2933","#FFFFFF"},{"#202D37","#FFFFFF"},{"#24313D","#FFFFFF"},{"#24333E","#EAF5F2"},{"#34434F","#EAF5F2"},{"#607681","#9ABBB7"},{"#344651","#D9E8E7"},{"#39C5BB","#0D857A"},{"#DFEAF2","#173E3D"},{"#CFDFEB","#245A57"},{"#EDF3F7","#173E3D"},
  {"#083F3A","#FFFFFF"},{"#083E3A","#FFFFFF"},{"#F2B868","#925B00"},{"#E4BE81","#805500"},{"#FFADA2","#B23D34"},{"#F27777","#C63838"},{"#87B5DF","#356C9C"},{"#F69DB5","#A33363"},{"#49312F","#FDE8E5"},{"#27394C","#E8F0FA"},{"#402C3B","#FCE8F0"},{"#1B3B3B","#E1F3EF"},{"#41372A","#FFF1D6"},{"#302D28","#FFF5DF"},{"#6C5940","#D1AE6D"},{"#254B49","#DDF3ED"},{"#213C40","#DFEDE9"},{"#1E3038","#EAF5F2"},{"#FFFFFF","#FFFFFF"},{"#000000","#000000"}
 };
 static Color Map(Color color,bool light){
  if(!light||color.A==0)return color;string hex="#"+color.R.ToString("X2")+color.G.ToString("X2")+color.B.ToString("X2"),mapped;Color result;
  if(lightColors.TryGetValue(hex,out mapped))result=Parse(mapped);
  else{int max=Math.Max(color.R,Math.Max(color.G,color.B));result=Parse(max<70?"#F5FAF9":max<140?"#B8CECA":max<200?"#567670":"#24514C");}
  result.A=color.A;return result;
 }
}
}
