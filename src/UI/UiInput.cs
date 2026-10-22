using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
namespace ClearSpace {
public static class UiInput {
 public static readonly DependencyProperty PreventWheelSelectionProperty=DependencyProperty.RegisterAttached("PreventWheelSelection",typeof(bool),typeof(UiInput),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.Inherits));
 public static bool GetPreventWheelSelection(DependencyObject element){return (bool)element.GetValue(PreventWheelSelectionProperty);}
 public static void SetPreventWheelSelection(DependencyObject element,bool value){element.SetValue(PreventWheelSelectionProperty,value);}
 static UiInput(){EventManager.RegisterClassHandler(typeof(ComboBox),UIElement.PreviewMouseWheelEvent,new MouseWheelEventHandler(PreviewWheel),true);}
 static ScrollViewer PopupScroll(DependencyObject node){if(node==null)return null;var scroller=node as ScrollViewer;if(scroller!=null)return scroller;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++){var child=PopupScroll(VisualTreeHelper.GetChild(node,i));if(child!=null)return child;}return null;}
 static void PreviewWheel(object sender,MouseWheelEventArgs args){
  var choice=(ComboBox)sender;
  // An open list still scrolls normally; a closed selector must never save a
  // different language, theme or rule preference just because the page scrolls.
  if(!GetPreventWheelSelection(choice))return;
  args.Handled=true;
  if(choice.IsDropDownOpen){var popup=choice.Template.FindName("PART_Popup",choice)as Popup;var scroller=popup==null?null:PopupScroll(popup.Child);if(scroller!=null)ForwardWheel(scroller,choice,args);return;}
  for(DependencyObject parent=VisualTreeHelper.GetParent(choice);parent!=null;parent=VisualTreeHelper.GetParent(parent)){
   var scroller=parent as ScrollViewer;if(scroller==null)continue;
   ForwardWheel(scroller,choice,args);break;
  }
 }
 static void ForwardWheel(ScrollViewer scroller,ComboBox choice,MouseWheelEventArgs args){scroller.RaiseEvent(new MouseWheelEventArgs(args.MouseDevice,args.Timestamp,args.Delta){RoutedEvent=UIElement.MouseWheelEvent,Source=choice});}
}
}
