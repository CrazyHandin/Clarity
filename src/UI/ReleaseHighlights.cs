namespace ClearSpace {
public static class ReleaseHighlights {
 public const string Chinese="新增软件在线更新与内部构建识别；清理记录改为自动保存，并完善备份恢复与规则管理。";
 public const string English="Added online software updates and internal build detection, automatic cleanup history, and improvements to backup recovery and rule management.";
 public static string Current{get{return UiText.English?English:Chinese;}}
}
}
