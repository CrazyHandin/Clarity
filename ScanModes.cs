using System.Collections.Generic;
using System.Linq;

namespace ClearSpace {
public static class ScanModes {
 public static List<Rule> QuickRules(){return RuleBook.Create().Where(IsQuickRule).ToList();}
 public static bool IsQuickRule(Rule r){return r.Quick&&!r.Protected&&!r.Heuristic&&!r.ForceBackup&&!r.Privacy&&r.InfoKind!="registry-clean"&&!r.Shortcut;}
}
}
