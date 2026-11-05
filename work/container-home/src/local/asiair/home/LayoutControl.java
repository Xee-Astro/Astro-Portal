package local.asiair.home;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ResolveInfo;
import org.json.JSONArray;
import org.json.JSONObject;
import java.util.List;
import java.util.Collections;
import java.util.HashSet;
public final class LayoutControl {
 public static void main(String[] args) throws Exception {
  if(args.length==1 && (args[0].equals("apps") || args[0].equals("gps"))) {
   if(android.os.Looper.myLooper()==null) android.os.Looper.prepare();
   Class<?> type=Class.forName("android.app.ActivityThread");
   Object thread=type.getMethod("systemMain").invoke(null);
   Context context=(Context)type.getMethod("getSystemContext").invoke(thread);
   if(args[0].equals("gps")) {
    Context shell=context.createPackageContext("com.android.shell",0);
    android.location.LocationManager manager=(android.location.LocationManager)shell.getSystemService(Context.LOCATION_SERVICE);
    final android.os.Looper looper=android.os.Looper.myLooper();
    android.location.LocationListener listener=new android.location.LocationListener(){
     public void onLocationChanged(android.location.Location fix){System.out.println("GPS_FIX="+fix.getLatitude()+","+fix.getLongitude());looper.quitSafely();}
     public void onProviderEnabled(String provider){} public void onProviderDisabled(String provider){}
     public void onStatusChanged(String provider,int status,android.os.Bundle extras){}
    };
    manager.requestLocationUpdates("gps",0,0,listener,looper);
    new android.os.Handler(looper).postDelayed(()->{System.out.println("GPS_FIX=timeout");looper.quitSafely();},15000);
    android.os.Looper.loop();manager.removeUpdates(listener);return;
   }
   Intent intent=new Intent(Intent.ACTION_MAIN);intent.addCategory(Intent.CATEGORY_LAUNCHER);
   List<ResolveInfo> apps=context.getPackageManager().queryIntentActivities(intent,0);
   Collections.sort(apps,(a,b)->a.loadLabel(context.getPackageManager()).toString().compareToIgnoreCase(b.loadLabel(context.getPackageManager()).toString()));
   JSONArray result=new JSONArray();HashSet<String> seen=new HashSet<String>();
   for(ResolveInfo app:apps) if(seen.add(app.activityInfo.packageName)) {
    JSONObject item=new JSONObject();item.put("Package",app.activityInfo.packageName);item.put("Label",app.loadLabel(context.getPackageManager()).toString());result.put(item);
   }
   System.out.println("ASTRO_APPS="+result.toString());return;
  }
  if(args.length!=2) throw new IllegalArgumentException("taskId windowingMode required");
  Object service=Class.forName("android.app.ActivityTaskManager").getMethod("getService").invoke(null);
  Object result=Class.forName("android.app.IActivityTaskManager").getMethod("setTaskWindowingMode",int.class,int.class,boolean.class).invoke(service,Integer.parseInt(args[0]),Integer.parseInt(args[1]),true);
  System.out.println("Layout applied: "+result);
 }
}
