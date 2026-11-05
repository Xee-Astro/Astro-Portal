package local.asiair.home;
import android.app.Activity;
import android.os.Bundle;
import android.content.Intent;
import android.net.Uri;
import android.content.pm.ResolveInfo;
import android.graphics.Color;
import android.view.Gravity;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import java.util.HashSet;
import java.util.List;
import java.util.Collections;

public class HomeActivity extends Activity {
 @Override public void onCreate(Bundle state) { super.onCreate(state); }
 @Override public void onResume() { super.onResume(); showApps(); }
 protected boolean othersOnly() { return false; }
 private void showApps() {
  LinearLayout layout=new LinearLayout(this);layout.setOrientation(LinearLayout.VERTICAL);layout.setPadding(40,24,40,24);layout.setBackgroundColor(Color.rgb(16,21,30));
  TextView title=new TextView(this);title.setText(othersOnly() ? "Other apps" : "Astro Home");title.setTextSize(30);title.setTextColor(Color.WHITE);layout.addView(title);
  TextView hint=new TextView(this);hint.setText(othersOnly() ? "Open an app here. Return to ASIAIR or Stellavita using the Windows buttons." : "ASIAIR, Stellavita and your Android apps");hint.setTextColor(Color.LTGRAY);layout.addView(hint);
  if (!othersOnly()) { add(layout,"ASIAIR","com.zwoasi.asiair");add(layout,"Stellavita","com.touptek.astrostation"); }
  Button store=new Button(this);store.setText("Google Play — install and update apps");store.setOnClickListener(v -> store("com.touptek.astrostation"));layout.addView(store);
  TextView other=new TextView(this);other.setText("Installed apps");other.setTextSize(18);other.setTextColor(Color.WHITE);other.setPadding(0,20,0,8);layout.addView(other);
  Intent query=new Intent(Intent.ACTION_MAIN);query.addCategory(Intent.CATEGORY_LAUNCHER);
  List<ResolveInfo> apps=getPackageManager().queryIntentActivities(query,0);
  Collections.sort(apps,(a,b)->a.loadLabel(getPackageManager()).toString().compareToIgnoreCase(b.loadLabel(getPackageManager()).toString()));
  HashSet<String> seen=new HashSet<String>();seen.add("com.zwoasi.asiair");seen.add("com.touptek.astrostation");seen.add("local.asiair.home");
  for(ResolveInfo app:apps) if(seen.add(app.activityInfo.packageName)) add(layout,app.loadLabel(getPackageManager()).toString(),app.activityInfo.packageName);
  ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);scroll.addView(layout);setContentView(scroll);
 }
 private void add(LinearLayout parent,String label,String pkg) { Button b=new Button(this);b.setText(label);b.setOnClickListener(v -> open(pkg));parent.addView(b); }
 private void open(String pkg) { Intent i=getPackageManager().getLaunchIntentForPackage(pkg);if(i==null){store(pkg);return;}i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);try{startActivity(i);}catch(RuntimeException ignored){} }
 private void store(String pkg) { try{Intent i=new Intent(Intent.ACTION_VIEW,Uri.parse("market://details?id="+pkg));i.setPackage("com.android.vending");startActivity(i);}catch(RuntimeException ignored){new android.app.AlertDialog.Builder(this).setMessage("Google Play is available in the Play Store profile. Start that profile from Windows.").setPositiveButton("OK",null).show();} }
}
