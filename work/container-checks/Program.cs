using AsiairContainer;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Text.Json;
int checks=0;
void Assert(bool condition,string name){if(!condition)throw new Exception("FAILED: "+name);Console.WriteLine("PASS "+name);checks++;}
string html=File.ReadAllText(args.Length > 0 && args[0] != "--live" ? args[0] : "work/container-checks/official-release-fixture.html");
Assert(UpdateChecker.ParseOfficialPage(html).Version=="3.0","current official page parses as 3.0");
Assert(UpdateChecker.ParseVersion("3.0")==UpdateChecker.ParseVersion("3.0.0"),"version normalization");
Assert(UpdateChecker.ParseVersion("3.10.0")>UpdateChecker.ParseVersion("3.9.9"),"numeric version comparison");
string Card(string platform,string version,string label="Google Driver Download")=>$"<div class=\"zwo-download-card\"><div class=\"zwo-platform\">{platform}</div><div>{label}</div><strong>Version:</strong> V{version}</div>";
Assert(UpdateChecker.ParseOfficialPage(Card("iOS","99.0")+Card("Android","3.1")+Card("Android","9.0","Beta")+Card("Android","7.0","Version for the ASIAIR Edition 1")+Card("Android","3.2")).Version=="3.2","ignore iOS, beta and first-edition releases; select latest Android");
try{UpdateChecker.ParseOfficialPage("<html>Maintenance</html>");throw new Exception("invalid page accepted");}catch(FormatException){Assert(true,"unknown page fails without claiming no update");}
string root=Path.GetFullPath(Path.Combine("work","container-checks","state-"+Guid.NewGuid().ToString("N")));
var fake=new FakeHandler(html);
using(var checker=new UpdateChecker(root,fake)){
 var first=await checker.CheckAsync(false,CancellationToken.None);Assert(first.Error==null&&first.Release?.Version=="3.0","successful check saves official release");
 await checker.CheckAsync(false,CancellationToken.None);Assert(fake.Calls==1,"automatic checks use six-hour cache");
 await checker.CheckAsync(true,CancellationToken.None);Assert(fake.Calls==2,"manual check bypasses cache");
 fake.Offline=true;var offline=await checker.CheckAsync(true,CancellationToken.None);Assert(offline.Error!=null&&offline.Release?.Version=="3.0"&&offline.LastSuccess!=null,"offline failure preserves last successful result and reports failure");
 var calls=fake.Calls;await checker.CheckAsync(false,CancellationToken.None);Assert(fake.Calls==calls,"failed automatic checks back off");
 fake.Offline=false;await checker.CheckAsync(true,CancellationToken.None);Assert(checker.Snapshot?.Error==null,"manual retry recovers after offline failure");
}
using(var reload=new UpdateChecker(root,new FakeHandler(html))){Assert(reload.Snapshot?.Release?.Version=="3.0","cache survives restart");}
string stella = """
{"StellaVita_2.0.32.20261031.apk":[],"StellaVita_2.0.33.20261101.apk":[],"AstroStation_99.0.20261101.aspkg":[],"StellaVita_88.0-beta.20261101.apk":[],"StellaVita_77.0.20261101.ipa":[]}
""";
Assert(UpdateChecker.ParseStellavitaFeed(stella).Version=="2.0.33","Stellavita selects latest stable APK excluding firmware, beta and iOS");
Assert(UpdateChecker.ParseVersion("2.0.33")>UpdateChecker.ParseVersion("2.0.32"),"Stellavita patch update comparison");
foreach(string invalid in new[]{"{}","[]","{\"AstroStation_99.0.20261101.aspkg\":[]}"}){
 try{UpdateChecker.ParseStellavitaFeed(invalid);throw new Exception("invalid Stellavita feed accepted");}catch(FormatException){Assert(true,"unrecognized Stellavita feed fails explicitly");}
}
var stellaFake=new FakeHandler(stella);
using(var checker=new UpdateChecker(root,stellaFake,stellavita:true)){
 var first=await checker.CheckAsync(false,CancellationToken.None);Assert(first.Error==null&&first.Release?.Version=="2.0.33","Stellavita uses independent cache and official host");
 await checker.CheckAsync(false,CancellationToken.None);Assert(stellaFake.Calls==1,"Stellavita automatic checks use cache");
 stellaFake.Offline=true;var failed=await checker.CheckAsync(true,CancellationToken.None);Assert(failed.Error!=null&&failed.Release?.Version=="2.0.33","Stellavita offline check preserves last result");
}
using(var asiair=new UpdateChecker(root,new FakeHandler(html)))Assert(asiair.Snapshot?.Release?.Version=="3.0"&&asiair.Snapshot.Error==null,"Stellavita failures leave ASIAIR cache intact");
using(var reload=new UpdateChecker(root,new FakeHandler(stella),stellavita:true))Assert(reload.Snapshot?.Release?.Version=="2.0.33","Stellavita cache survives restart");
string officialStella=File.ReadAllText("work/container-checks/stellavita-official.json");
Assert(UpdateChecker.ParseStellavitaFeed(officialStella).Version=="2.0.31","official Stellavita feed parses exact app version");
if(args.Contains("--live")){
 using var live=new UpdateChecker(root,stellavita:true);var result=await live.CheckAsync(true,CancellationToken.None);
 Assert(result.Error==null&&result.Release!=null,"live official Stellavita endpoint verified");Console.WriteLine("Live Stellavita: "+result.Release!.Version);
}
foreach(var point in new[]{(double.NaN,0d),(91d,0d),(0d,181d),(double.PositiveInfinity,0d)}){
 try{new ContainerOptions{Latitude=point.Item1,Longitude=point.Item2}.Validate();throw new Exception("invalid coordinates accepted");}catch(ArgumentException){checks++;}
}
new ContainerOptions{Latitude=-90,Longitude=180}.Validate();Assert(true,"coordinate boundaries and non-finite rejection");
string taskListing = """
Stack id=81 bounds=[0,0][635,800] displayId=0 userId=0
 configuration={winConfig={mWindowingMode=split-screen-primary}}
  taskId=52: com.zwoasi.asiair/.Splash bounds=[0,0][635,800] userId=0 visible=true topActivity=ComponentInfo{com.zwoasi.asiair/.Camera}
Stack id=82 bounds=[645,0][1280,800] displayId=0 userId=0
 configuration={winConfig={mWindowingMode=split-screen-secondary}}
  taskId=53: com.touptek.astrostation/.Splash bounds=[645,0][1280,800] userId=0 visible=true topActivity=ComponentInfo{com.touptek.astrostation/.MainCamera}
Stack id=83 bounds=[0,0][1280,800] displayId=0 userId=0
 configuration={winConfig={mWindowingMode=fullscreen}}
  taskId=83: com.zwoasi.asiair/.Old bounds=[0,0][1280,800] userId=0 visible=false
""";
var taskState=RuntimeController.ParseTasks(taskListing);
Assert(taskState.Count==2&&taskState[0].Id==52&&taskState[0].Stack==81&&taskState[0].Mode=="split-screen-primary"&&taskState[1].Visible&&taskState[1].Mode=="split-screen-secondary","dynamic Android task IDs, split modes and empty-stack filtering");
await PhoneChecks.Run(Assert);
PortalChecks.Run(Assert,root);
File.WriteAllText(Path.Combine(root,"result.json"),JsonSerializer.Serialize(new{passed=true,checks,time=DateTimeOffset.UtcNow}));Console.WriteLine($"{checks} checks passed");
sealed class FakeHandler(string html):HttpMessageHandler{
 public int Calls;public bool Offline;
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;if(Offline)throw new HttpRequestException("Offline test");return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(html),RequestMessage=request});}
}

