using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
namespace ClearSpace {
// Inspect the shipped assembly without executing it or invoking its attributes.
public static class ProductionArtifactTests {
 static string Hash(Stream stream){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream));}
 static IEnumerable<string> Strings(MethodBase method){
  var body=method.GetMethodBody();if(body==null)yield break;var il=body.GetILAsByteArray();var codes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(c=>(ushort)c.Value);
  for(int p=0;p<il.Length;){ushort value=il[p++];if(value==0xfe)value=(ushort)(0xfe00|il[p++]);OpCode code;if(!codes.TryGetValue(value,out code))throw new InvalidDataException("Unknown IL opcode");int size;
   switch(code.OperandType){case OperandType.InlineNone:size=0;break;case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:size=1;break;case OperandType.InlineVar:size=2;break;case OperandType.InlineI8:case OperandType.InlineR:size=8;break;case OperandType.InlineSwitch:size=4+4*BitConverter.ToInt32(il,p);break;default:size=4;break;}
   if(p+size>il.Length)throw new InvalidDataException("Truncated IL operand");if(code.OperandType==OperandType.InlineString)yield return method.Module.ResolveString(BitConverter.ToInt32(il,p));p+=size;
  }
 }
 public static int Run(string executable,string output){
  Directory.CreateDirectory(output);var checks=new List<string>();Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("FAILED: "+label);checks.Add("PASS: "+label);};
  ResolveEventHandler resolve=(sender,args)=>Assembly.ReflectionOnlyLoad(args.Name);AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve+=resolve;
  try{
   var production=Assembly.ReflectionOnlyLoadFrom(Path.GetFullPath(executable));var types=production.GetTypes();const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
   check(production.EntryPoint!=null&&production.EntryPoint.DeclaringType.FullName=="ClearSpace.App"&&production.EntryPoint.Name=="Main","Production keeps the normal Clarity entry point");
   check(Path.GetFileName(executable)=="Clarity.exe","The shipped executable filename is Clarity.exe");
   check(FileVersionInfo.GetVersionInfo(executable).ProductVersion==ReleaseVersion.FullVersion,"Production and test host have the same release version");
   check(!types.Any(t=>t.Name.EndsWith("Tests",StringComparison.Ordinal)||t.Name=="SelfTest"||t.Name=="ProductionArtifactTests"),"Production contains no test suite types");
   check(!types.SelectMany(t=>t.GetMethods(flags)).Any(m=>m.Name.StartsWith("Verify",StringComparison.Ordinal)&&m.DeclaringType.FullName=="ClearSpace.Controller"),"Production contains no UI verification methods");
   var methods=types.SelectMany(t=>t.GetMethods(flags).Cast<MethodBase>().Concat(t.GetConstructors(flags))).ToArray();var strings=new HashSet<string>(methods.SelectMany(Strings),StringComparer.Ordinal);
   check(!strings.Contains("powershell.exe"),"Production contains no test PowerShell launcher");
   check(!strings.Contains("ClearSpace.Tests"),"Production has no test-only registry allowlist");
   check(!strings.Any(s=>s.StartsWith("--preview",StringComparison.Ordinal)||s.EndsWith("self-test",StringComparison.Ordinal)),"Production has no preview or self-test command handlers");
   check(!strings.Contains("--scan-report")&&!strings.Contains("--discovery-report")&&!strings.Contains("--winapp2-update-test"),"Diagnostic command handlers are confined to the test host");
   check(strings.Contains("--apply-update"),"Production retains its validated portable-update entry point");
   check(!methods.Any(m=>m.Name=="CreateHardLink"),"Production has no test hardlink creation import");
   check(types.Any(t=>t.FullName=="ClearSpace.Engine")&&types.Any(t=>t.FullName=="ClearSpace.FileRecovery")&&types.Any(t=>t.FullName=="ClearSpace.SoftwareUpdates"),"Cleaning, recovery and update components remain present");
   var expected=Assembly.GetExecutingAssembly().GetManifestResourceNames();check(production.GetManifestResourceNames().OrderBy(n=>n).SequenceEqual(expected.OrderBy(n=>n)),"Production and test host contain the same application resources");
   check(!expected.Contains("ClearSpace.Winapp2.ini"),"The rule database is distributed as data rather than an executable resource");
   string pinned=(string)production.GetType("ClearSpace.ReleaseVersion").GetField("Winapp2SHA256").GetRawConstantValue();check(pinned==ReleaseVersion.Winapp2SHA256,"Production and test host pin the same bundled rule content");
   using(var data=File.OpenRead(Path.Combine(Path.GetDirectoryName(executable),"rules","Winapp2.ini")))check(Hash(data).Replace("-","").Equals(pinned,StringComparison.OrdinalIgnoreCase),"Production bundled rule file matches its pinned checksum");
   foreach(string name in expected)using(var a=production.GetManifestResourceStream(name))using(var b=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))check(Hash(a)==Hash(b),"Production resource matches test host: "+name);
   using(var stream=production.GetManifestResourceStream("ClearSpace.License.txt"))using(var reader=new StreamReader(stream)){string license=reader.ReadToEnd();check(license.Contains("Clarity 个人使用与源码公开许可协议 1.0")&&license.Contains("Clarity Personal Use and Source Available License 1.0")&&license.Contains("every Clarity version and internal build")&&license.Contains("CC BY-SA 4.0")&&!license.Contains("Permission is hereby granted"),"Production embeds the bilingual personal-use license with historical and third-party exceptions");}
   using(var stream=production.GetManifestResourceStream("ClearSpace.Usage.txt"))using(var reader=new StreamReader(stream)){string guide=reader.ReadToEnd();int chinese=guide.IndexOf("Clarity 个人使用与源码公开许可协议 1.0",StringComparison.Ordinal),english=guide.IndexOf("English User Guide",StringComparison.Ordinal);check(chinese>=0&&chinese<english&&guide.IndexOf("Clarity Personal Use and Source Available License 1.0",StringComparison.Ordinal)>english&&!guide.Contains("Permission is hereby granted"),"The single bundled TXT guide carries Chinese and English license sections in the correct order without the obsolete MIT grant");}
   check(production.GetReferencedAssemblies().All(a=>new[]{"mscorlib","System","System.Core","System.Xaml","System.Security","System.Web.Extensions","System.Windows.Forms","System.IO.Compression","System.IO.Compression.FileSystem","PresentationCore","PresentationFramework","WindowsBase"}.Contains(a.Name)),"Production only references the expected framework assemblies");
   var worker=Assembly.ReflectionOnlyLoadFrom(Path.Combine(Path.GetDirectoryName(executable),UpdateServiceProtocol.ExecutableName));check(worker.EntryPoint.DeclaringType.FullName=="ClearSpace.Maintenance.Program"&&FileVersionInfo.GetVersionInfo(worker.Location).ProductVersion==ReleaseVersion.FullVersion,"The separate update service has the expected entry point and release version");check(!worker.GetManifestResourceNames().Any(n=>n.EndsWith(".ini",StringComparison.OrdinalIgnoreCase))&&!types.Any(t=>t.FullName=="ClearSpace.Maintenance.MaintenanceService"),"The service is separate from the UI and contains no embedded rule database");
   File.WriteAllLines(Path.Combine(output,"production-test-results.txt"),checks.Concat(new[]{"ALL "+checks.Count+" CHECKS PASSED","Production metadata was inspected without executing the target assembly."}));return 0;
  }catch(Exception ex){File.WriteAllLines(Path.Combine(output,"production-test-results.txt"),checks.Concat(new[]{ex.ToString()}));return 1;}
  finally{AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve-=resolve;}
 }
}
}
