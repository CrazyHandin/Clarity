using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace ClearSpace {
public sealed class UiTranslationConverter:IValueConverter {
 public object Convert(object value,Type targetType,object parameter,CultureInfo culture){return UiText.T(value as string??System.Convert.ToString(value,culture));}
 public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture){return Binding.DoNothing;}
}
public static class UiText {
 static readonly Dictionary<string,string> english=Load();
 static readonly Regex words=new Regex(String.Join("|",english.Keys.OrderByDescending(k=>k.Length).Select(Regex.Escape)),RegexOptions.Compiled);
 static readonly Regex paths=new Regex(@"(?:[A-Za-z]:\\|\\\\|https?://)[^\r\n；）]*",RegexOptions.IgnoreCase);
 static readonly ConditionalWeakTable<Window,Context> contexts=new ConditionalWeakTable<Window,Context>();
 static readonly List<WeakReference> windows=new List<WeakReference>();
 static string language="zh-CN";
 public static string Language {get{return language;}}
 public static bool English {get{return language=="en";}}
 public static string Normalize(string value){return !String.IsNullOrEmpty(value)&&value.StartsWith("en",StringComparison.OrdinalIgnoreCase)?"en":"zh-CN";}
 static Dictionary<string,string> Load(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ClearSpace.English.json"))using(var reader=new StreamReader(stream)){var map=new JavaScriptSerializer{MaxJsonLength=4*1024*1024}.Deserialize<Dictionary<string,string>>(reader.ReadToEnd());if(!String.IsNullOrEmpty(ReleaseVersion.HighlightsChinese)&&!String.IsNullOrEmpty(ReleaseVersion.HighlightsEnglish))map[ReleaseVersion.HighlightsChinese]=ReleaseVersion.HighlightsEnglish;return map;}}
 public static string T(string value){
  if(!English||String.IsNullOrEmpty(value))return value;
  string exact;if(english.TryGetValue(value,out exact))return exact;
  var parts=new List<string>();int offset=0;
  foreach(Match match in paths.Matches(value)){parts.Add(words.Replace(value.Substring(offset,match.Index-offset),m=>english[m.Value]));parts.Add(match.Value);offset=match.Index+match.Length;}
  parts.Add(words.Replace(value.Substring(offset),m=>english[m.Value]));return String.Concat(parts);
 }
 public static void SetLanguage(string value){language=Normalize(value);foreach(var weak in windows.ToArray()){var window=weak.Target as Window;if(window==null){windows.Remove(weak);continue;}Context context;if(contexts.TryGetValue(window,out context))context.Apply();}}
 public static void Attach(Window window){Context found;if(contexts.TryGetValue(window,out found)){found.Apply();return;}var context=new Context(window);contexts.Add(window,context);windows.Add(new WeakReference(window));context.Attach();}
 public static void Forget(Window window,DependencyObject root){Context context;if(contexts.TryGetValue(window,out context))context.Forget(root);}
 public static void AttachPage(Window window,FrameworkElement page){Context context;if(contexts.TryGetValue(window,out context))context.AttachPage(page);else Attach(window);}
 public static MessageBoxResult Show(Window owner,string message,string caption=null,MessageBoxButton buttons=MessageBoxButton.OK,MessageBoxImage image=MessageBoxImage.None){return English?EnglishDialog.Show(owner,message,buttons):MessageBox.Show(owner,message,App.DisplayName,buttons,image);}
 public static MessageBoxResult Show(string message,string caption=null,MessageBoxButton buttons=MessageBoxButton.OK,MessageBoxImage image=MessageBoxImage.None){return English?EnglishDialog.Show(null,message,buttons):MessageBox.Show(message,App.DisplayName,buttons,image);}
 sealed class TextState {
  public DependencyObject Target;public DependencyProperty Property;public string Source;public bool Writing;public DependencyPropertyDescriptor Descriptor;public EventHandler Handler;
  public void Apply(){if(Source==null)return;Writing=true;try{Target.SetCurrentValue(Property,T(Source));}finally{Writing=false;}}
 }
 sealed class Context {
  readonly Window window;readonly HashSet<DependencyObject> visited=new HashSet<DependencyObject>();readonly List<TextState> states=new List<TextState>();RoutedEventHandler loaded;
  public Context(Window value){window=value;}
  public void Attach(){loaded=(s,e)=>Visit(e.OriginalSource as DependencyObject);window.AddHandler(FrameworkElement.LoadedEvent,loaded,true);window.Closed+=(s,e)=>{window.RemoveHandler(FrameworkElement.LoadedEvent,loaded);foreach(var state in states)state.Descriptor.RemoveValueChanged(state.Target,state.Handler);states.Clear();visited.Clear();contexts.Remove(window);};Visit(window);Apply();}
  void Watch(DependencyObject target,DependencyProperty property){
   if(states.Any(s=>s.Target==target&&s.Property==property))return;
   var value=target.GetValue(property)as string;
   var state=new TextState{Target=target,Property=property,Source=value,Descriptor=DependencyPropertyDescriptor.FromProperty(property,target.GetType())};
   state.Handler=(s,e)=>{if(state.Writing)return;var current=target.GetValue(property)as string;if(current==null)return;state.Source=current;state.Apply();};
   state.Descriptor.AddValueChanged(target,state.Handler);states.Add(state);state.Apply();
  }
  public void Visit(DependencyObject node){Visit(node,new HashSet<DependencyObject>());}
  public void AttachPage(DependencyObject node){if(!visited.Contains(node))Visit(node);}
  public void Forget(DependencyObject root){var nodes=new HashSet<DependencyObject>();Collect(root,nodes);foreach(var state in states.Where(s=>nodes.Contains(s.Target)).ToArray()){state.Descriptor.RemoveValueChanged(state.Target,state.Handler);states.Remove(state);}visited.ExceptWith(nodes);}
  static void Collect(DependencyObject node,HashSet<DependencyObject> nodes){if(node==null||!nodes.Add(node))return;foreach(object child in LogicalTreeHelper.GetChildren(node)){var item=child as DependencyObject;if(item!=null)Collect(item,nodes);}if(node is Visual)for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)Collect(VisualTreeHelper.GetChild(node,i),nodes);}
  void Visit(DependencyObject node,HashSet<DependencyObject> traversal){
   if(node==null||!traversal.Add(node))return;
   if(visited.Add(node)){
    if(node is TextBlock)Watch(node,TextBlock.TextProperty);
    var box=node as TextBox;if(box!=null&&box.IsReadOnly)Watch(node,TextBox.TextProperty);
    if(node is ContentControl)Watch(node,ContentControl.ContentProperty);
    if(node is HeaderedContentControl)Watch(node,HeaderedContentControl.HeaderProperty);
    if(node is FrameworkElement)Watch(node,FrameworkElement.ToolTipProperty);
    var combo=node as ComboBox;if(combo!=null&&combo.Name!="LanguageChoice"){
     var factory=new FrameworkElementFactory(typeof(TextBlock));factory.SetBinding(TextBlock.TextProperty,new Binding{Converter=new UiTranslationConverter()});combo.ItemTemplate=new DataTemplate{VisualTree=factory};
    }
   }
   foreach(object child in LogicalTreeHelper.GetChildren(node)){var dependency=child as DependencyObject;if(dependency!=null)Visit(dependency,traversal);}
   if(node is Visual)for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)Visit(VisualTreeHelper.GetChild(node,i),traversal);
  }
  public void Apply(){
   window.Title=App.DisplayName;window.Language=XmlLanguage.GetLanguage(English?"en-US":"zh-CN");window.FontFamily=new FontFamily(English?"Segoe UI":"Microsoft YaHei UI");Visit(window);
   foreach(var state in states.ToArray()){var expression=BindingOperations.GetBindingExpression(state.Target,state.Property);if(expression!=null)expression.UpdateTarget();state.Apply();}
   var secondary=window.FindName("EnglishBrandName")as FrameworkElement;if(secondary!=null)secondary.Visibility=English?Visibility.Collapsed:Visibility.Visible;
   var entry=window.FindName("RuleCenterButton")as Button;if(entry!=null)System.Windows.Automation.AutomationProperties.SetName(entry,T("设置与备份"));
  }
 }
}
}
