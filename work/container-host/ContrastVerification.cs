using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AsiairContainer;
public static class ContrastVerification
{
 public static Window Create(string root)
 {
  var window=new Window{Title="Launcher contrast check",Width=1100,Height=900,Background=Color("#10151E"),Foreground=Color("#EEF3FA"),FontSize=14,WindowStartupLocation=WindowStartupLocation.CenterScreen};
  window.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/AsiairContainer;component/Theme.xaml",UriKind.Relative)});
  var grid=new StackPanel{Margin=new Thickness(20)};window.Content=grid;
  TextBox? selectedInput=null;
  var results=new List<object>();int count=0;double minimum=21;
  void Check(string name,Brush foreground,Brush background,double threshold=4.5){double ratio=Ratio(((SolidColorBrush)foreground).Color,((SolidColorBrush)background).Color);if(ratio<threshold)throw new InvalidOperationException($"{name}: {ratio:0.00}:1");minimum=Math.Min(minimum,ratio);results.Add(new{name,ratio,threshold});count++;}
  void Add(FrameworkElement control,string label){var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,9)};row.Children.Add(new TextBlock{Text=label,Width=210,VerticalAlignment=VerticalAlignment.Center});row.Children.Add(control);grid.Children.Add(row);}
  foreach(string state in new[]{"normal","accent","hover","pressed","focus","disabled","accent hover","accent pressed","accent disabled","active","active hover","active pressed","active disabled","asiair","asiair hover","asiair pressed","asiair disabled","stellavita","stellavita hover","stellavita pressed","stellavita disabled"}){
   var button=new Button{Content="Start / switch app",Padding=new Thickness(14,7,14,7),MinWidth=220,Tag=state.Contains("asiair")?"ASIAIR":state.Contains("stellavita")?"Stellavita":state.Contains("active")?"Active":state.Contains("accent")?"Accent":null};Add(button,"Button: "+state);button.ApplyTemplate();
   State(button,state);
   var surface=(Border)button.Template.FindName("Surface",button);var label=(ContentPresenter)button.Template.FindName("Label",button);
   Check("button "+state,TextElement.GetForeground(label),surface.Background);
  }
  foreach(string state in new[]{"normal","checked","hover","disabled","checked disabled"}){
   var box=new CheckBox{Content="Automatic checks / startup",IsChecked=state.Contains("checked")};Add(box,"Checkbox: "+state);box.ApplyTemplate();State(box,state);
   Check("checkbox "+state,TextElement.GetForeground((ContentPresenter)box.Template.FindName("Label",box)),window.Background);
  }
  foreach(string state in new[]{"normal","focus","disabled","selected"}){
   var box=new TextBox{Text="Phone IP / location",Width=300,Padding=new Thickness(7,4,7,4)};Add(box,"Text field: "+state);box.ApplyTemplate();State(box,state);
   Check("text field "+state,box.Foreground,box.Background);
   if(state=="selected"){selectedInput=box;box.SelectAll();Check("selected text",box.SelectionTextBrush,box.SelectionBrush);}
  }
  Check("ASIAIR note selection",Brushes.Black,Color("#FFA0A0"));
  Check("Stellavita note selection",Brushes.Black,Color("#79B8FF"));
  Check("ASIAIR labels",Color("#FFA0A0"),window.Background);
  Check("Stellavita labels",Color("#79B8FF"),window.Background);
  Check("status/location/update text",Color("#A4B6CD"),window.Background);
  Check("drawer and labels",window.Foreground,window.Background);
  Check("accent heading",Color("#FFB58B"),window.Background);
  Check("placeholder",Color("#6D829C"),Brushes.Black);
  window.Loaded+=(_,_)=>window.Dispatcher.BeginInvoke(new Action(()=>{
   try{
    selectedInput?.Focus();selectedInput?.SelectAll();window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(root,"Contrast-preview.png")))png.Save(stream);
    File.WriteAllText(Path.Combine(root,"Contrast-verification.json"),JsonSerializer.Serialize(new{passed=true,checks=count,minimumRatio=minimum,normalTextThreshold=4.5,states=results,time=DateTimeOffset.UtcNow},new JsonSerializerOptions{WriteIndented=true}));
   }catch(Exception error){Environment.ExitCode=1;File.WriteAllText(Path.Combine(root,"Contrast-verification.json"),JsonSerializer.Serialize(new{passed=false,error=error.ToString()}));}finally{window.Close();}
  }));
  return window;
 }
 private static void State(Control control,string state){if(state.Contains("hover"))SetKey(control,"IsMouseOverPropertyKey");if(state.Contains("pressed"))SetKey(control,"IsPressedPropertyKey");if(state.Contains("focus"))SetKey(control,control is TextBox?"IsKeyboardFocusWithinPropertyKey":"IsKeyboardFocusedPropertyKey");if(state.Contains("disabled"))control.IsEnabled=false;}
 private static void SetKey(Control control,string name){for(Type? type=control.GetType();type!=null;type=type.BaseType){var field=type.GetField(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);if(field?.GetValue(null) is DependencyPropertyKey key){control.SetValue(key,true);return;}}throw new InvalidOperationException("WPF state key unavailable: "+name);}
 private static SolidColorBrush Color(string value)=>new((Color)ColorConverter.ConvertFromString(value));
 private static double Ratio(Color a,Color b){double L(Color c){double F(byte n){double s=n/255d;return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4);}return .2126*F(c.R)+.7152*F(c.G)+.0722*F(c.B);}double x=L(a),y=L(b);return(Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
}
