package local.asiair.home;
import android.app.Activity;
import android.os.Bundle;
import android.location.Location;
import android.location.LocationListener;
import android.location.LocationManager;
import android.widget.TextView;
import java.util.Locale;
public final class LocationCheckActivity extends Activity implements LocationListener {
 private TextView text; private LocationManager manager;
 @Override public void onCreate(Bundle state){super.onCreate(state);text=new TextView(this);text.setText("Waiting for Android GPS");text.setTextSize(25);setContentView(text);manager=(LocationManager)getSystemService(LOCATION_SERVICE);try{manager.requestLocationUpdates(LocationManager.GPS_PROVIDER,0,0,this);}catch(SecurityException e){text.setText("Location permission required");}}
 @Override public void onLocationChanged(Location location){text.setText(String.format(Locale.US,"GPS received: %.4f, %.4f",location.getLatitude(),location.getLongitude()));}
 @Override public void onProviderEnabled(String p){} @Override public void onProviderDisabled(String p){} @Override public void onStatusChanged(String p,int s,Bundle b){}
 @Override public void onDestroy(){super.onDestroy();try{manager.removeUpdates(this);}catch(Exception ignored){}}
}
