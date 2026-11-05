package local.asiair.home;
import android.app.Activity;
import android.os.Bundle;
import android.view.Gravity;
import android.widget.Button;
import android.widget.LinearLayout;
public final class InputCheckActivity extends Activity {
 @Override public void onCreate(Bundle state) {
  super.onCreate(state);
  LinearLayout panel=new LinearLayout(this); panel.setGravity(Gravity.CENTER);
  Button button=new Button(this); button.setText("Tap to verify input");
  button.setOnClickListener(v -> button.setText("Input received"));
  panel.addView(button); setContentView(panel);
 }
}
