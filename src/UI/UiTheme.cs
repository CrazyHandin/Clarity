using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
namespace ClearSpace {
public sealed class ThemeColorSlot:INotifyPropertyChanged {
 internal SolidColorBrush Brush;internal Color LightColor;internal int Revision;
 public Color Original;Color color;public Color Color{get{return color;}set{if(color==value)return;color=value;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("Color"));}}
 public event PropertyChangedEventHandler PropertyChanged;
}
public static class UiTheme {
 static readonly ResourceDictionary palette=new ResourceDictionary();static readonly Dictionary<string,ThemeColorSlot> slots=new Dictionary<string,ThemeColorSlot>();static readonly HashSet<string> colorResources=new HashSet<string>();
 public static bool Light{get;private set;}
 public static string Normalize(string value){return String.Equals(value,"system",StringComparison.OrdinalIgnoreCase)?"system":String.Equals(value,"light",StringComparison.OrdinalIgnoreCase)?"light":"dark";}
 internal static Func<bool> SystemLight=ReadSystemLight;
 static bool ReadSystemLight(){try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",false))return key!=null&&Convert.ToInt32(key.GetValue("AppsUseLightTheme",0))!=0;}catch{return false;}}
 static Color Parse(string value){return (Color)ColorConverter.ConvertFromString(value);}
 static string Key(Color color){return color.ToString().Substring(1);}
 public static Brush Brush(string value){
  Color color=Parse(value);string key=Key(color);ThemeColorSlot slot;if(!slots.TryGetValue(key,out slot)){
   Color mapped=Map(color,true);slot=new ThemeColorSlot{Original=color,LightColor=mapped,Color=Light?mapped:color};slots[key]=slot;
   var brush=new SolidColorBrush();slot.Brush=brush;BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding("Color"){Source=slot});palette["ThemeBrush"+key]=brush;palette["ThemeColor"+key]=slot.Color;
   slot.PropertyChanged+=(s,e)=>{if(colorResources.Contains(key))palette["ThemeColor"+key]=slot.Color;};
  }return (Brush)palette["ThemeBrush"+key];
 }
 static string Resource(string value,bool color){Brush(value);string key=Key(Parse(value));if(color)colorResources.Add(key);return "{DynamicResource Theme"+(color?"Color":"Brush")+key+"}";}
 public static string Markup(string markup){
  markup=Regex.Replace(markup,@"(?<prefix>\b(?:Background|Foreground|BorderBrush|Stroke|Fill|Color)\s*=\s*)(?<q>['"+"\""+@"])(?<value>\#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?)\k<q>",m=>m.Groups["prefix"].Value+m.Groups["q"].Value+Resource(m.Groups["value"].Value,m.Groups["prefix"].Value.TrimStart().StartsWith("Color"))+m.Groups["q"].Value);
  return Regex.Replace(markup,@"(?<prefix>\bProperty\s*=\s*['"+"\""+@"](?:Background|Foreground|BorderBrush|Stroke|Fill)['"+"\""+@"][^<>]*?\bValue\s*=\s*)(?<q>['"+"\""+@"])(?<value>\#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?)\k<q>",m=>m.Groups["prefix"].Value+m.Groups["q"].Value+Resource(m.Groups["value"].Value,false)+m.Groups["q"].Value);
 }
 public static void Attach(FrameworkElement root){if(!root.Resources.MergedDictionaries.Contains(palette))root.Resources.MergedDictionaries.Add(palette);}
 public static void Set(string value,bool animate){
  bool light=Normalize(value)=="system"?SystemLight():Normalize(value)=="light";if(animate&&light==Light)return;Light=light;
  foreach(var slot in slots.Values.ToArray()){
   var brush=slot.Brush;Color from=brush.Color,to=light?slot.LightColor:slot.Original;int revision=++slot.Revision;
   brush.BeginAnimation(SolidColorBrush.ColorProperty,null);slot.Color=to;
   if(!animate||from==to)continue;
   // Keep resource notifications out of animation frames; WPF animates the shared brush.
   var animation=new ColorAnimation(from,to,TimeSpan.FromMilliseconds(240)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseInOut},FillBehavior=FillBehavior.Stop};
   var current=slot;animation.Completed+=(s,e)=>{if(current.Revision==revision)brush.BeginAnimation(SolidColorBrush.ColorProperty,null);};
   brush.BeginAnimation(SolidColorBrush.ColorProperty,animation,HandoffBehavior.SnapshotAndReplace);
  }
 }
 // Light surfaces, borders and green typography follow the beta0.02 palette.
 static readonly Dictionary<string,string> lightColors=new Dictionary<string,string>{
  {"#161D25","#F3F8F8"},{"#1B2731","#FFFFFF"},{"#1D2933","#FFFFFF"},{"#202D37","#FFFFFF"},{"#24313D","#FFFFFF"},{"#24333E","#EAF5F2"},{"#34434F","#EAF5F2"},{"#607681","#9ABBB7"},{"#344651","#D9E8E7"},{"#39C5BB","#0D857A"},{"#DFEAF2","#173E3D"},{"#CFDFEB","#245A57"},{"#EDF3F7","#173E3D"},
  {"#083F3A","#FFFFFF"},{"#083E3A","#FFFFFF"},{"#F2B868","#925B00"},{"#E4BE81","#805500"},{"#FFADA2","#B23D34"},{"#F27777","#C63838"},{"#87B5DF","#356C9C"},{"#F69DB5","#A33363"},{"#49312F","#FDE8E5"},{"#27394C","#E8F0FA"},{"#402C3B","#FCE8F0"},{"#1B3B3B","#E1F3EF"},{"#41372A","#FFF1D6"},{"#302D28","#FFF5DF"},{"#6C5940","#D1AE6D"},{"#254B49","#DDF3ED"},{"#213C40","#DFEDE9"},{"#1E3038","#EAF5F2"},{"#FFFFFF","#FFFFFF"},{"#000000","#000000"},
  {"#203D31","#E7F6ED"},{"#56C98B","#27804D"},{"#C9F4DB","#235A3B"},{"#7FDBAC","#27804D"},{"#3B3323","#FFF6DF"},{"#E8BE58","#A87812"},{"#FFE6A3","#6E5010"},{"#F2CB68","#996D0D"}
 };
 static Color Map(Color color,bool light){
  if(!light||color.A==0)return color;string hex="#"+color.R.ToString("X2")+color.G.ToString("X2")+color.B.ToString("X2"),mapped;Color result;
  if(lightColors.TryGetValue(hex,out mapped))result=Parse(mapped);
  else{int max=Math.Max(color.R,Math.Max(color.G,color.B));result=Parse(max<70?"#F5FAF9":max<140?"#B8CECA":max<200?"#567670":"#24514C");}
  result.A=color.A;return result;
 }
}
}
