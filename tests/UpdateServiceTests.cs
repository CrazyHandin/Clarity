using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
namespace ClearSpace {
public static class UpdateServiceTests {
 public static string Sign(string payload,RSACryptoServiceProvider rsa){var json=new JavaScriptSerializer();var data=json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(payload);byte[] bytes=Encoding.UTF8.GetBytes(payload);data["SignedPayload"]=Convert.ToBase64String(bytes);data["Signature"]=Convert.ToBase64String(rsa.SignData(bytes,CryptoConfig.MapNameToOID("SHA256")));return json.Serialize(data);}
 public static void Verify(string root,Action<bool,string> check){
  Func<Action,bool> rejects=action=>{try{action();return false;}catch{return true;}};var json=new JavaScriptSerializer();
  using(var rsa=new RSACryptoServiceProvider(3072,new CspParameters{ProviderType=24,Flags=CspProviderFlags.CreateEphemeralKey})){
   rsa.PersistKeyInCsp=false;const string payload="{\"Schema\":1,\"Version\":\"1.0.6\",\"Build\":4,\"Artifacts\":[]}";string signed=Sign(payload,rsa),key=rsa.ToXmlString(false);
   check(UpdateTrust.Verify(signed,key)==payload,"A valid RSA-3072 publisher signature authenticates the exact update payload");
   check(rejects(()=>UpdateTrust.Verify(payload,key)),"Unsigned update feeds cannot reach download or installation");
   var changed=json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(signed);changed["Build"]=5;check(rejects(()=>UpdateTrust.Verify(json.Serialize(changed),key)),"Changing visible manifest fields without resigning is rejected");
   changed=json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(signed);changed["SignedPayload"]=Convert.ToBase64String(Encoding.UTF8.GetBytes(payload.Replace("\"Build\":4","\"Build\":5")));check(rejects(()=>UpdateTrust.Verify(json.Serialize(changed),key)),"A changed signed payload is rejected before parsing packages");
   check(rejects(()=>UpdateTrust.Verify(signed,UpdateTrust.PublicKey)),"A different signing key cannot authorize an official update");
  }
  foreach(string id in new[]{"../outside","",new string('a',31),new string('g',32),"C:\\Windows"})check(rejects(()=>UpdateServiceProtocol.JobPath(id)),"Service job paths reject traversal or malformed IDs: "+id);
  using(var memory=new MemoryStream(Encoding.UTF8.GetBytes(new string('x',8193)+"\n")))using(var reader=new StreamReader(memory))check(rejects(()=>UpdateServiceProtocol.Read<ServiceUpdateRequest>(reader)),"Oversized pipe messages are rejected without unbounded allocation");
  using(var memory=new MemoryStream(Encoding.UTF8.GetBytes("{\"Version\":\"1.0.6\",\"Build\":4,\"ParentId\":123,\"ParentStarted\":1,\"Command\":\"untrusted.exe\",\"Target\":\"C:\\\\Windows\"}\n")))using(var reader=new StreamReader(memory)){var request=UpdateServiceProtocol.Read<ServiceUpdateRequest>(reader);check(request.ParentId==123&&typeof(ServiceUpdateRequest).GetFields().Length==4,"The update service protocol has no user-supplied executable, target, or download URL");check(rejects(()=>Maintenance.MaintenanceService.ValidateRequest(request,124,root)),"A caller cannot request an update on behalf of a different process");}
  check(!UpdateServiceProtocol.ProtectedInstallation(root),"User-writable and portable locations cannot host the privileged update service");
  string future=Path.Combine(root,"future-service","job");check(UpdatePackage.SafeFutureParents(Path.Combine(future,"probe"))&&!Directory.Exists(future),"First-use service directories can be validated before they exist");
  string untrusted=Path.Combine(root,"service-plan.dat");File.WriteAllText(untrusted,"untrusted plan");check(ServiceUpdateClient.Complete(untrusted)==1,"An untrusted completion plan cannot launch a program or write a ready marker");
  string pipeName="ClarityFixture-"+Guid.NewGuid().ToString("N");using(var server=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,8192,8192,Maintenance.MaintenanceService.PipePermissions()))using(var client=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut)){
   var operation=System.Threading.Tasks.Task.Run(()=>{server.WaitForConnection();using(var reader=new StreamReader(server,Encoding.UTF8,false,4096,true))return UpdateServiceControl.Peer(server.SafePipeHandle,false)==System.Diagnostics.Process.GetCurrentProcess().Id&&UpdateServiceProtocol.Read<ServiceUpdateRequest>(reader).Build==4;});client.Connect(3000);using(var writer=new StreamWriter(client,new UTF8Encoding(false),4096,true))UpdateServiceProtocol.Write(writer,new ServiceUpdateRequest{Version="1.0.6",Build=4});check(operation.Wait(3000)&&operation.Result&&UpdateServiceControl.Peer(client.SafePipeHandle,true)==System.Diagnostics.Process.GetCurrentProcess().Id,"Restricted local pipe permissions allow an interactive client and authenticate both peer process IDs");
  }
 }
}
}
