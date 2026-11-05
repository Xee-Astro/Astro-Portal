using AsiairContainer;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

static class PhoneChecks
{
 public static string Sentence(string body) { int sum=0;foreach(char c in body)sum^=c;return "$"+body+"*"+sum.ToString("X2"); }
 public static async Task Run(Action<bool,string> check)
 {
  var now=DateTimeOffset.UtcNow;string time=now.ToString("HHmmss",CultureInfo.InvariantCulture),date=now.ToString("ddMMyy",CultureInfo.InvariantCulture);
  string gga=Sentence($"GNGGA,{time},1220.736,N,04540.734,E,1,08,0.9,10,M,0,M,,");
  check(PhoneLocation.TryParse(gga,now,out var fix)&&Math.Abs(fix!.Latitude-12.3456)<0.000001&&Math.Abs(fix.Longitude-45.6789)<0.000001,"phone GGA fix and degrees/minutes conversion");
  string rmc=Sentence($"GPRMC,{time},A,1220.736,S,04540.734,W,0,0,{date},,,A");
  check(PhoneLocation.TryParse(rmc,now,out fix)&&fix!.Latitude<0&&fix.Longitude<0,"phone RMC fix and southern/western hemispheres");
  check(!PhoneLocation.TryParse(gga[..^2]+"XX",now,out _),"phone rejects corrupt checksum");
  check(!PhoneLocation.TryParse(Sentence($"GPGGA,{time},1220.736,N,04540.734,E,0,08,0.9,10,M,0,M,,"),now,out _),"phone rejects no-fix quality");
  check(!PhoneLocation.TryParse(Sentence($"GPRMC,{time},V,1220.736,N,04540.734,E,0,0,{date},,,A"),now,out _),"phone rejects invalid RMC status");
  check(!PhoneLocation.TryParse(gga,now.AddMinutes(10),out _),"phone rejects stale buffered GPS data");
  check(!PhoneLocation.TryParse(Sentence($"GPGGA,{time},1260.736,N,04540.734,E,1,08,0.9,10,M,0,M,,"),now,out _),"phone rejects invalid minutes");
  check(!PhoneLocation.TryParse(Sentence($"GPGGA,{time},9120.736,N,04540.734,E,1,08,0.9,10,M,0,M,,"),now,out _),"phone rejects out-of-range coordinates");
  var midnight=new DateTimeOffset(2026,11,1,0,0,15,TimeSpan.Zero);
  check(PhoneLocation.TryParse(Sentence("GPGGA,235959,1220.736,N,04540.734,E,1,08,0.9,10,M,0,M,,"),midnight,out _),"phone GGA handles UTC midnight");
  using var server=new TcpListener(IPAddress.Loopback,0);server.Start();int port=((IPEndPoint)server.LocalEndpoint).Port;
  var serve=Task.Run(async()=>{using var client=await server.AcceptTcpClientAsync();using var stream=client.GetStream();var bytes=Encoding.ASCII.GetBytes("invalid data\r\n"+gga+"\r\n");await stream.WriteAsync(bytes.AsMemory(0,20));await Task.Delay(20);await stream.WriteAsync(bytes.AsMemory(20));});
  var received=await PhoneLocation.GetAsync("127.0.0.1",port,CancellationToken.None);await serve;
  check(Math.Abs(received.Latitude-12.3456)<0.000001,"phone TCP receives fragmented NMEA after invalid input");
  using var cancel=new CancellationTokenSource();cancel.Cancel();
  try{await PhoneLocation.GetAsync("127.0.0.1",port,cancel.Token);throw new Exception("cancel ignored");}catch(OperationCanceledException){check(true,"phone cancellation is immediate");}
  string root=Path.GetFullPath(Path.Combine("work/container-checks","state-"+Guid.NewGuid().ToString("N")));Directory.CreateDirectory(root);
  var options=new ContainerOptions{AutoStartAndroid=true,PhoneGpsIp="192.0.2.25",PhoneGpsPort=11123,UseWindowsLocation=false,UsePhoneLocation=true};options.Save(root);var restored=ContainerOptions.Load(root);
  check(restored.AutoStartAndroid&&restored.UsePhoneLocation&&restored.PhoneGpsIp==options.PhoneGpsIp&&restored.PhoneGpsPort==11123,"phone endpoint/source and Android auto-start persist");
  try{(options with {UseWindowsLocation=true}).Validate();throw new Exception("conflicting sources accepted");}catch(ArgumentException){check(true,"phone and Windows sources cannot compete");}
 }
}

